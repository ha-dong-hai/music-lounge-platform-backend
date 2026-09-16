using MusicLounge.Application.Common.Configuration;

namespace MusicLounge.Application.Common.Interfaces;

/// <summary>
/// MLACP-420: tra loi cau hoi "he thong dang thieu cau hinh gi va hau qua ra sao".
///
/// Lop ma nay duoc viet theo huong "thieu thi bo qua" o rat nhieu cho — tot cho khoi dong, te cho van hanh: thieu
/// Firebase:ProjectId lam dang nhap Google hong voi MOI nguoi dung suot nhieu tuan ma khong ai biet (15/09), thieu
/// Mux:WebhookSecret lam mat bang xem lai livestream (16/09). Bang kiem nay bien cai "im lang" do thanh mot danh sach
/// doc duoc: log luc khoi dong va mot API cho Admin.
/// </summary>
public interface IConfigurationAudit
{
    IReadOnlyList<ConfigurationGap> Inspect();
}
