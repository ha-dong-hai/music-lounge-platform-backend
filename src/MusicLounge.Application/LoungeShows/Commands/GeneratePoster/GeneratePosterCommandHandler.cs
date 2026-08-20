using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.GeneratePoster;

// W02: AI poster generation, gated by the Owner's subscription (HasAiPosterSnapshot). Two
// independent limits apply, on purpose:
//   - MaxAiPostersPerMonth (on the subscription, tier-differentiated): a billing quota — only
//     Succeeded attempts count against it, because a Failed one is Gemini's fault, not the
//     Owner's, and charging them for it would break the "trách nhiệm với khách hàng" promise.
//   - ai_poster_max_attempts_per_show (system_config, same for every tier): an anti-abuse rate
//     limit — counts EVERY attempt (success + failure) so a broken prompt can't loop indefinitely
//     against one show and rack up vendor cost even though it never succeeds.
internal sealed class GeneratePosterCommandHandler
    : IRequestHandler<GeneratePosterCommand, PosterGenerationResultDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;
    private readonly IAiImageGenerationService _aiImage;
    private readonly IFileStorageService _fileStorage;

    public GeneratePosterCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ISystemConfigService config,
        IAiImageGenerationService aiImage, IFileStorageService fileStorage)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
        _aiImage = aiImage;
        _fileStorage = fileStorage;
    }

    public async Task<PosterGenerationResultDto> Handle(GeneratePosterCommand request, CancellationToken ct)
    {
        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);
        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền tạo poster cho show này.");

        var now = DateTimeOffset.UtcNow;
        var activeSubs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
            s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active, ct);
        var activeSub = activeSubs.Where(s => s.ExpiresAt > now).OrderByDescending(s => s.StartedAt).FirstOrDefault();

        if (activeSub is null || !activeSub.HasAiPosterSnapshot)
            throw new DomainException(
                "Gói subscription hiện tại của bạn không bao gồm tính năng tạo poster AI.");

        var genRepo = _uow.Repository<AiPosterGeneration, int>();

        // Anti-abuse: every attempt (success or failure) on THIS show counts.
        var maxAttemptsPerShow = await _config.GetIntAsync(ConfigKeys.AiPosterMaxAttemptsPerShow, 5, ct);
        var attemptsForShow = await genRepo.CountAsync(g => g.ShowId == request.ShowId, ct);
        if (attemptsForShow >= maxAttemptsPerShow)
            throw new DomainException(
                $"Show này đã đạt giới hạn {maxAttemptsPerShow} lần tạo poster AI. Vui lòng liên hệ hỗ trợ nếu cần thêm.");

        // Billing quota: only Succeeded attempts this calendar month count. Filtered server-side on
        // the simple equality predicates, then CreatedAt client-side — same recurring SQLite-
        // translation limitation documented throughout this codebase: combining an equality check
        // with a DateTimeOffset comparison in one Where clause fails to translate under the test
        // provider.
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var ownerSucceeded = await genRepo.FindAsync(
            g => g.OwnerId == lounge.OwnerId && g.Status == AiPosterGenerationStatus.Succeeded, ct);
        var succeededThisMonth = ownerSucceeded.Count(g => g.CreatedAt >= monthStart);
        if (succeededThisMonth >= activeSub.MaxAiPostersPerMonthSnapshot)
            throw new DomainException(
                $"Bạn đã dùng hết {activeSub.MaxAiPostersPerMonthSnapshot} poster AI trong tháng này. " +
                "Hạn mức sẽ làm mới vào đầu tháng sau.");

        var prompt = await BuildPromptAsync(show, lounge, request.StyleHint, ct);

        byte[] imageBytes;
        try
        {
            imageBytes = await _aiImage.GenerateImageAsync(prompt, ct);
        }
        catch (ExternalServiceException ex)
        {
            genRepo.Add(new AiPosterGeneration
            {
                ShowId = show.Id,
                OwnerId = lounge.OwnerId,
                Status = AiPosterGenerationStatus.Failed,
                Prompt = prompt,
                ErrorMessage = ex.Message,
                CreatedAt = now
            });
            await _uow.SaveChangesAsync(ct);
            throw;
        }

        string imageUrl;
        await using (var stream = new MemoryStream(imageBytes))
        {
            imageUrl = await _fileStorage.SaveImageAsync(stream, "poster.png", ct);
        }

        genRepo.Add(new AiPosterGeneration
        {
            ShowId = show.Id,
            OwnerId = lounge.OwnerId,
            Status = AiPosterGenerationStatus.Succeeded,
            Prompt = prompt,
            ImageUrl = imageUrl,
            CreatedAt = now
        });

        show.PosterUrl = imageUrl;
        show.PosterByAi = true;
        showRepo.Update(show);

        await _uow.SaveChangesAsync(ct);

        var remaining = Math.Max(0, activeSub.MaxAiPostersPerMonthSnapshot - (succeededThisMonth + 1));
        return new PosterGenerationResultDto(imageUrl, remaining);
    }

    private async Task<string> BuildPromptAsync(
        LoungeShow show, MusicLoungeEntity lounge, string? styleHint, CancellationToken ct)
    {
        // The generic repository never eager-loads navigation properties (no .Include anywhere in
        // Repository<T,TKey>), so a Genre/Mood/Atmosphere nav on these join rows would always come
        // back null — look up the linked ids first, then resolve names in a second query, same
        // pattern PerformerDtoMapper already uses for the same reason.
        var genreIds = (await _uow.Repository<LoungeShowGenre, int>().FindAsync(
            g => g.LoungeShowId == show.Id, ct)).Select(g => g.GenreId).ToList();
        var moodIds = (await _uow.Repository<LoungeShowMood, int>().FindAsync(
            m => m.LoungeShowId == show.Id, ct)).Select(m => m.MoodId).ToList();
        var atmosphereIds = (await _uow.Repository<LoungeShowAtmosphere, int>().FindAsync(
            a => a.LoungeShowId == show.Id, ct)).Select(a => a.AtmosphereId).ToList();

        var genreNames = genreIds.Count == 0
            ? []
            : (await _uow.Repository<MusicGenre, int>().FindAsync(g => genreIds.Contains(g.Id), ct))
                .Select(g => g.Name);
        var moodNames = moodIds.Count == 0
            ? []
            : (await _uow.Repository<Mood, int>().FindAsync(m => moodIds.Contains(m.Id), ct))
                .Select(m => m.Name);
        var atmosphereNames = atmosphereIds.Count == 0
            ? []
            : (await _uow.Repository<VenueAtmosphere, int>().FindAsync(a => atmosphereIds.Contains(a.Id), ct))
                .Select(a => a.Name);

        var tags = genreNames.Concat(moodNames).Concat(atmosphereNames).ToList();
        var tagLine = tags.Count > 0 ? string.Join(", ", tags) : "nhạc sống";

        var prompt =
            $"Thiết kế poster quảng cáo cho một buổi diễn nhạc sống tại Việt Nam. " +
            $"Tên chương trình: \"{show.Name}\". Địa điểm: \"{lounge.Name}\". " +
            $"Ngày diễn: {show.ScheduledStart:dd/MM/yyyy HH:mm}. " +
            $"Thể loại/không khí: {tagLine}. " +
            "Phong cách: chuyên nghiệp, hấp dẫn, phù hợp đăng mạng xã hội, bố cục rõ ràng có chỗ cho tiêu đề.";

        if (!string.IsNullOrWhiteSpace(styleHint))
            prompt += $" Yêu cầu thêm từ chủ sự kiện: {styleHint}.";

        return prompt;
    }
}
