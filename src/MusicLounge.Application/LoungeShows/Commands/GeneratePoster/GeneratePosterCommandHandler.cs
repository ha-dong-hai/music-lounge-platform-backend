using MediatR;
using MusicLounge.Application.Common;
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

        // MLACP-419: chi dem lan TAO DUOC POSTER. Truoc day dem ca lan that bai, nen khi nha cung cap hong hoac chua
        // cau hinh (dung tinh trang Azure 16/09), chu phong tra bam 5 lan la khoa vinh vien tinh nang cho buoi dien do —
        // ma chua nhan duoc tam poster nao, trong khi day la tinh nang trong goi ho da tra tien. Chong lam dung van con:
        // han muc thang theo goi (dem ben duoi) va bo gioi han so request cua API.
        var maxAttemptsPerShow = await _config.GetIntAsync(ConfigKeys.AiPosterMaxAttemptsPerShow, 5, ct);
        var attemptsForShow = await genRepo.CountAsync(
            g => g.ShowId == request.ShowId && g.Status == AiPosterGenerationStatus.Succeeded, ct);
        if (attemptsForShow >= maxAttemptsPerShow)
            throw new DomainException(
                $"Show này đã đạt giới hạn {maxAttemptsPerShow} lần tạo poster AI. Vui lòng liên hệ hỗ trợ nếu cần thêm.");

        // Billing quota: only Succeeded attempts this calendar month count. Filtered server-side on
        // the simple equality predicates, then CreatedAt client-side — same recurring SQLite-
        // translation limitation documented throughout this codebase: combining an equality check
        // with a DateTimeOffset comparison in one Where clause fails to translate under the test
        // provider.
        //
        // MLACP-458: đơn ĐANG CHỜ cũng phải tính. Quy tắc "chỉ lần thành công mới trừ lượt" đúng với đường gọi thẳng, vì
        // lúc trả lời thì đã biết thành hay bại. Với hàng đợi thì giữa lúc bấm và lúc có ảnh là hàng phút, nên nếu chỉ
        // đếm lần thành công, chủ phòng trà còn 1 lượt vẫn bấm được 10 lần liên tiếp và hệ thống nhận cả 10. Đơn đang chờ
        // được coi là GIỮ CHỖ; đơn hỏng (Failed/Expired) thì không tính nữa, tức là tự trả lại lượt — giữ nguyên tinh thần
        // "lỗi của nhà cung cấp thì không được tính vào tiền người ta đã trả" của MLACP-419.
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var ownerCounted = await genRepo.FindAsync(
            g => g.OwnerId == lounge.OwnerId
                && (g.Status == AiPosterGenerationStatus.Succeeded
                    || g.Status == AiPosterGenerationStatus.Queued
                    || g.Status == AiPosterGenerationStatus.Rendering), ct);
        var succeededThisMonth = ownerCounted.Count(g => g.CreatedAt >= monthStart);
        if (succeededThisMonth >= activeSub.MaxAiPostersPerMonthSnapshot)
            throw new DomainException(
                $"Bạn đã dùng hết {activeSub.MaxAiPostersPerMonthSnapshot} poster AI trong tháng này. " +
                "Hạn mức sẽ làm mới vào đầu tháng sau.");

        var prompt = await BuildPromptAsync(show, lounge, request.StyleHint, _aiImage.IsDeferred, ct);

        // MLACP-458: nhà cung cấp không trả ảnh trong cùng lượt gọi (máy trạm chạy Google Flow) — ghi đơn rồi trả lời ngay.
        if (_aiImage.IsDeferred)
            return await QueueJobAsync(show, lounge.OwnerId, prompt, activeSub.MaxAiPostersPerMonthSnapshot,
                succeededThisMonth, now, ct);

        byte[] imageBytes;
        string tenFile;
        try
        {
            imageBytes = await _aiImage.GenerateImageAsync(prompt, ct);

            // MLACP-421: dat ten file theo DINH DANG THAT cua anh. Truoc day luon la "poster.png", nhung Cloudflare
            // Workers AI (nha cung cap mien phi them o MLACP-418) tra ve JPEG — ma UploadContentRules doi chieu phan mo
            // rong voi chu ky file (chong doi duoi de lach kiem duyet), nen anh bi tu choi ngay o buoc luu, sau khi da
            // tieu mot luot goi nha cung cap.
            tenFile = ImageMimeTypeHelper.FromContent(imageBytes) switch
            {
                "image/png" => "poster.png",
                "image/jpeg" => "poster.jpg",
                "image/webp" => "poster.webp",
                "image/gif" => "poster.gif",
                _ => throw new ExternalServiceException(
                    "AiImage", "Nhà cung cấp trả về dữ liệu không phải ảnh nhận dạng được.")
            };
        }
        catch (ExternalServiceException ex)
        {
            genRepo.Add(new AiPosterGeneration
            {
                ShowId = show.Id,
                OwnerId = lounge.OwnerId,
                Status = AiPosterGenerationStatus.Failed,
                Prompt = prompt,
                Provider = _aiImage.ProviderName,
                ErrorMessage = ex.Message,
                CreatedAt = now
            });
            await _uow.SaveChangesAsync(ct);
            throw;
        }

        string imageUrl;
        await using (var stream = new MemoryStream(imageBytes))
        {
            imageUrl = await _fileStorage.SaveImageAsync(stream, tenFile, ct);
        }

        genRepo.Add(new AiPosterGeneration
        {
            ShowId = show.Id,
            OwnerId = lounge.OwnerId,
            Status = AiPosterGenerationStatus.Succeeded,
            Prompt = prompt,
            Provider = _aiImage.ProviderName,
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

    /// <summary>
    /// MLACP-458. Ghi một đơn <c>Queued</c> và trả lời ngay, thay vì giữ người dùng chờ 50–90 giây.
    ///
    /// Mỗi buổi hòa nhạc chỉ được có MỘT đơn đang chờ: bấm nhiều lần trong lúc chờ không tạo thêm đơn, vì mỗi đơn là một
    /// lượt hạn mức Google thật, và người dùng bấm lại thường vì họ tưởng lần trước chưa ăn chứ không phải muốn hai poster.
    /// </summary>
    private async Task<PosterGenerationResultDto> QueueJobAsync(
        LoungeShow show, int ownerId, string prompt, int monthlyQuota, int usedThisMonth,
        DateTimeOffset now, CancellationToken ct)
    {
        var genRepo = _uow.Repository<AiPosterGeneration, int>();

        var dangCho = await genRepo.AnyAsync(
            g => g.ShowId == show.Id
                && (g.Status == AiPosterGenerationStatus.Queued
                    || g.Status == AiPosterGenerationStatus.Rendering), ct);
        if (dangCho)
            throw new ConflictException(
                "Buổi hòa nhạc này đang có một poster được tạo. Vui lòng đợi kết quả trước khi tạo thêm.");

        var job = new AiPosterGeneration
        {
            ShowId = show.Id,
            OwnerId = ownerId,
            Status = AiPosterGenerationStatus.Queued,
            Prompt = prompt,
            Provider = _aiImage.ProviderName,
            CreatedAt = now
        };
        genRepo.Add(job);
        await _uow.SaveChangesAsync(ct);

        return new PosterGenerationResultDto(
            null,
            Math.Max(0, monthlyQuota - (usedThisMonth + 1)),
            nameof(AiPosterGenerationStatus.Queued),
            job.Id);
    }

    private async Task<string> BuildPromptAsync(
        LoungeShow show, MusicLoungeEntity lounge, string? styleHint, bool anhNenKhongChu, CancellationToken ct)
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
            $"Ngày diễn: {VietnamTime.Format(show.ScheduledStart)}. " +
            $"Thể loại/không khí: {tagLine}. " +
            "Phong cách: chuyên nghiệp, hấp dẫn, phù hợp đăng mạng xã hội, bố cục rõ ràng có chỗ cho tiêu đề.";

        if (!string.IsNullOrWhiteSpace(styleHint))
            prompt += $" Yêu cầu thêm từ chủ buổi diễn: {styleHint}.";

        // MLACP-458: ở chế độ hàng đợi, ảnh lấy từ Google Flow là ẢNH NỀN — chữ tiếng Việt sẽ được in bằng font ở bước
        // sau, không để mô hình tự vẽ. Lý do: mô hình sinh ảnh viết tiếng Việt sai dấu, mà poster sai tên buổi diễn thì
        // không dùng được. Thử ngày 19/09 cho thấy Flow TUÂN THỦ câu cấm này (FLUX trước đây thì không nghe lệnh phủ
        // định). Lớp in chữ bằng font là phần việc riêng, chưa làm trong task này.
        if (anhNenKhongChu)
            prompt +=
                " Yêu cầu bắt buộc: đây là ẢNH NỀN, tuyệt đối KHÔNG chứa chữ, không chữ cái, không con số, không logo, " +
                "không watermark. Chừa một phần ba phía trên thoáng, ít chi tiết, để chỗ in tiêu đề sau.";

        return prompt;
    }
}
