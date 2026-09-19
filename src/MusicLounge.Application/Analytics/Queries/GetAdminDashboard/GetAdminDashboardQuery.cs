using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetAdminDashboard;

/// <summary>
/// MLACP-463. Dữ liệu cho trang tổng quan của Admin.
/// </summary>
/// <param name="From">Bỏ trống = 6 tháng gần nhất (tính theo giờ Việt Nam). Áp cho top buổi hòa nhạc và thể loại.</param>
/// <param name="To">Bỏ trống = bây giờ.</param>
/// <param name="Limit">Số buổi hòa nhạc trong bảng xếp hạng. Mặc định 10, tối đa 50.</param>
public sealed record GetAdminDashboardQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Limit = 10) : IQuery<AdminDashboardDto>;
