using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Complaints.Commands.CreateComplaint;

internal sealed class CreateComplaintCommandHandler : IRequestHandler<CreateComplaintCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateComplaintCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateComplaintCommand request, CancellationToken ct)
    {
        // D17: guest reporters (no account) must leave a contact phone so Admin can verify identity.
        if (!_currentUser.IsAuthenticated && string.IsNullOrWhiteSpace(request.ContactPhone))
            throw new DomainException("Vui lòng để lại số điện thoại liên hệ nếu không đăng nhập.");

        var complaint = new Complaint
        {
            ComplainantUserId = _currentUser.IsAuthenticated ? _currentUser.UserId : null,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Category = Enum.Parse<ComplaintCategory>(request.Category, ignoreCase: true),
            Description = request.Description,
            EvidenceUrls = request.EvidenceUrls,
            ContactPhone = request.ContactPhone,
            Status = ComplaintStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _uow.Repository<Complaint, int>().Add(complaint);
        await _uow.SaveChangesAsync(ct);
        return complaint.Id;
    }
}
