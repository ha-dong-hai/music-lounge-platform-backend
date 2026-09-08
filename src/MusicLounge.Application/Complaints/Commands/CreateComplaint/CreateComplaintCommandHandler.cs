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

        // Người có tài khoản xem lại được qua GET /complaints/my, nên chỉ khách vãng lai mới cần mã
        // tra cứu. Chuỗi ngẫu nhiên bằng RandomNumberGenerator chứ không phải Guid tuần tự hay id
        // tăng dần — endpoint tra cứu là công khai, nên mã đoán được nghĩa là đọc được khiếu nại của
        // người khác.
        var lookupReference = _currentUser.IsAuthenticated
            ? null
            : Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

        var complaint = new Complaint
        {
            ComplainantUserId = _currentUser.IsAuthenticated ? _currentUser.UserId : null,
            LookupReference = lookupReference,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Category = Enum.Parse<ComplaintCategory>(request.Category, ignoreCase: true),
            Description = request.Description,
            EvidenceUrls = request.EvidenceUrls,
            ContactPhone = request.ContactPhone,
            Status = ComplaintStatus.Open,
            CreatedAt = now,
            SlaDeadline = now.AddHours(slaHours)
        };

        _uow.Repository<Complaint, int>().Add(complaint);
        await _uow.SaveChangesAsync(ct);
        return new ComplaintCreatedDto(complaint.Id, lookupReference);
    }
}
