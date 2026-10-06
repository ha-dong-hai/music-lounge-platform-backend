using System.Security.Cryptography;
using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Complaints.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Complaints.Commands.CreateComplaint;

internal sealed class CreateComplaintCommandHandler
    : IRequestHandler<CreateComplaintCommand, ComplaintCreatedDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;

    public CreateComplaintCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ISystemConfigService config)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<ComplaintCreatedDto> Handle(CreateComplaintCommand request, CancellationToken ct)
    {
        // D17: guest reporters (no account) must leave a contact phone so Admin can verify identity.
        if (!_currentUser.IsAuthenticated && string.IsNullOrWhiteSpace(request.ContactPhone))
            throw new DomainException("Vui lòng để lại số điện thoại liên hệ nếu không đăng nhập.");

        var now = DateTimeOffset.UtcNow;
        var slaHours = await _config.GetIntAsync(ConfigKeys.ComplaintSlaHours, 72, ct);

        // MLACP-690 (chủ dự án 06/10/2026: "tôi gửi khiếu nại như không thấy mã để tra cứu"): MỌI khiếu nại đều có mã, kể
        // cả người đã đăng nhập. Bản trước chỉ cấp cho khách vãng lai (lý do: người có tài khoản xem được qua
        // GET /complaints/my) — nhưng người dùng quen với "số hồ sơ" và đi tìm nó, và mã còn để nói với hỗ trợ / tra trên máy
        // khác. Tra bằng mã không lộ thêm gì: ComplaintLookupDto không có dữ liệu cá nhân. Khiếu nại cũ được bù mã bằng
        // migration MLACP690_BackfillComplaintLookupReference.
        // Chuỗi ngẫu nhiên bằng RandomNumberGenerator chứ không phải Guid tuần tự hay id tăng dần — endpoint tra cứu là
        // công khai, nên mã đoán được nghĩa là đọc được khiếu nại của người khác.
        var lookupReference = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

        // MLACP-680: khiếu nại "livestream" có thể mang mã BUỔI DIỄN (dán từ đường dẫn /livestream/<mã buổi diễn>) —
        // validator đã nhận. Lưu về Livestream.Id để mọi nơi đọc TargetId (ReferenceNames, hàng đợi Admin) hiểu đúng
        // một nghĩa duy nhất của loại này.
        var targetId = request.TargetId;
        if (request.TargetType == "livestream")
        {
            var theoBuoi = await _uow.Repository<Livestream, Guid>()
                .FindAsync(l => l.LoungeShowId == request.TargetId, ct);
            if (theoBuoi.Count > 0) targetId = theoBuoi[0].Id;
        }

        var complaint = new Complaint
        {
            ComplainantUserId = _currentUser.IsAuthenticated ? _currentUser.UserId : null,
            LookupReference = lookupReference,
            TargetType = request.TargetType,
            TargetId = targetId,
            Category = Enum.Parse<ComplaintCategory>(request.Category, ignoreCase: true),
            Description = request.Description,
            EvidenceUrls = request.EvidenceUrls,
            ContactPhone = request.ContactPhone,
            Status = ComplaintStatus.Open,
            CreatedAt = now,
            SlaDeadline = now.AddHours(slaHours)
        };

        _uow.Repository<Complaint, Guid>().Add(complaint);
        await _uow.SaveChangesAsync(ct);
        return new ComplaintCreatedDto(complaint.Id, lookupReference);
    }
}
