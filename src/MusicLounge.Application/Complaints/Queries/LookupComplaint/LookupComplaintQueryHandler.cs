using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Complaints.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Complaints.Queries.LookupComplaint;

/// <summary>
/// Cách duy nhất để một người khiếu nại không có tài khoản biết việc của mình đã được xử lý ra sao.
/// Trước đây họ gửi khiếu nại xong nhận về mỗi một số id: GET /complaints/my đòi đăng nhập, không có
/// endpoint tra cứu nào, và không có SMS báo kết quả. Khiếu nại gửi vào khoảng không.
/// </summary>
internal sealed class LookupComplaintQueryHandler
    : IRequestHandler<LookupComplaintQuery, ComplaintLookupDto>
{
    private readonly IUnitOfWork _uow;

    public LookupComplaintQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<ComplaintLookupDto> Handle(LookupComplaintQuery request, CancellationToken ct)
    {
        var matches = await _uow.Repository<Complaint, int>()
            .FindAsync(c => c.LookupReference == request.Reference, ct);

        // Cùng một thông báo cho "mã sai" và "mã không tồn tại" — mã tra cứu chính là thứ chứng minh
        // quyền xem, nên phân biệt hai trường hợp sẽ biến endpoint công khai này thành công cụ dò mã.
        var complaint = matches.FirstOrDefault()
            ?? throw new NotFoundException("Complaint", "mã tra cứu đã cung cấp");

        return new ComplaintLookupDto(
            complaint.Id,
            complaint.TargetType,
            complaint.Category,
            complaint.Status,
            complaint.Resolution,
            complaint.ResolvedAction,
            complaint.CreatedAt,
            complaint.ResolvedAt,
            complaint.SlaDeadline);
    }
}
