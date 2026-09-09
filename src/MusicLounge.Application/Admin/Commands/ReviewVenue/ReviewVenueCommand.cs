using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Admin.Commands.ReviewVenue;

/// <param name="Decision">'Approved' hoặc 'Rejected'.</param>
/// <param name="ReviewNote">
/// Bắt buộc khi từ chối. Owner đã nộp giấy phép kinh doanh và chờ; nói với họ đúng một chữ "bị từ
/// chối" là bắt họ đoán xem thiếu gì.
/// </param>
public sealed record ReviewVenueCommand(
    int LoungeId,
    string Decision,
    string? ReviewNote) : ICommand;
