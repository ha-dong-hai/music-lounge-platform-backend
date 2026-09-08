using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Livestreams.Commands.SendChatMessage;

internal sealed class SendChatMessageCommandHandler : IRequestHandler<SendChatMessageCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ILivestreamHubService _hubService;
    private readonly IChatRateLimiter _rateLimiter;

    public SendChatMessageCommandHandler(
        IUnitOfWork uow, ILivestreamHubService hubService, IChatRateLimiter rateLimiter)
    {
        _uow = uow;
        _hubService = hubService;
        _rateLimiter = rateLimiter;
    }

    public async Task<int> Handle(SendChatMessageCommand request, CancellationToken ct)
    {
        var livestream = await _uow.Repository<Livestream, int>().GetByIdAsync(request.LivestreamId, ct)
            ?? throw new NotFoundException(nameof(Livestream), request.LivestreamId);

        if (livestream.Status != LivestreamStatus.Live)
            throw new DomainException("Chat chỉ khả dụng khi livestream đang phát sóng.");

        // MLACP-119: Owner co the tat chat bat ky luc nao (SetChatEnabledCommand) — kiem tra lai
        // gia tri that trong DB moi lan gui, khong cache, nen "tat chat co hieu luc ngay" tu nhien
        // dung: tin nhan gui ngay sau khi tat se bi chan tu lan goi ke tiep.
        if (!livestream.ChatEnabled)
            throw new DomainException("Chat đã bị tắt cho livestream này.");

        // §6.10 — 1 tin nhắn / 2 giây per user. Checked here (not in the SignalR hub) so it's
        // enforced regardless of transport and covered by the same test harness as everything else.
        if (!_rateLimiter.TryAcquire(request.UserId))
            throw new DomainException("Bạn đang gửi tin nhắn quá nhanh. Vui lòng đợi vài giây rồi thử lại.");

        var user = await _uow.Repository<User, int>().GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        var chatMessage = new LivestreamChatMessage
        {
            LivestreamId = request.LivestreamId,
            UserId = request.UserId,
            Message = request.Message,
            SentAt = DateTimeOffset.UtcNow
        };

        _uow.Repository<LivestreamChatMessage, int>().Add(chatMessage);
        await _uow.SaveChangesAsync(ct);

        await _hubService.BroadcastChatMessageAsync(
            request.LivestreamId,
            new ChatMessageDto(chatMessage.Id, request.UserId, user.FullName, request.Message, chatMessage.SentAt),
            ct);

        return chatMessage.Id;
    }
}
