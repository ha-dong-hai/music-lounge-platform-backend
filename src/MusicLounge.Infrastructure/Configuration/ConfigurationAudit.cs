using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Configuration;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Configuration;

/// <summary>
/// MLACP-420. Moi muc o day deu la mot su co da xay ra that, khong phai canh bao phong xa:
/// Firebase:ProjectId (dang nhap Google hong nhieu tuan), Mux:WebhookSecret (mat ban xem lai), Gemini:ApiKey
/// (anh tai len khong duoc kiem), OpenAi/Cloudflare (poster AI luon hong), Business:*Url (khach roi vao trang sai).
/// </summary>
internal sealed class ConfigurationAudit : IConfigurationAudit
{
    private readonly FirebaseSettings _firebase;
    private readonly MuxSettings _mux;
    private readonly LivestreamSettings _livestream;
    private readonly GeminiSettings _gemini;
    private readonly OpenAiSettings _openAi;
    private readonly CloudflareSettings _cloudflare;
    private readonly BusinessSettings _business;
    private readonly SmsSettings _sms;
    private readonly IHostEnvironment _env;

    public ConfigurationAudit(
        IOptions<FirebaseSettings> firebase,
        IOptions<MuxSettings> mux,
        IOptions<LivestreamSettings> livestream,
        IOptions<GeminiSettings> gemini,
        IOptions<OpenAiSettings> openAi,
        IOptions<CloudflareSettings> cloudflare,
        IOptions<BusinessSettings> business,
        IOptions<SmsSettings> sms,
        IHostEnvironment env)
    {
        _firebase = firebase.Value;
        _mux = mux.Value;
        _livestream = livestream.Value;
        _gemini = gemini.Value;
        _openAi = openAi.Value;
        _cloudflare = cloudflare.Value;
        _business = business.Value;
        _sms = sms.Value;
        _env = env;
    }

    public IReadOnlyList<ConfigurationGap> Inspect()
    {
        var gaps = new List<ConfigurationGap>();
        var chayThat = !_env.IsDevelopment() && !_env.IsEnvironment("Testing");

        if (string.IsNullOrWhiteSpace(_firebase.ProjectId))
            gaps.Add(new ConfigurationGap(
                "Đăng nhập Google", "Firebase:ProjectId",
                "Mọi lần đăng nhập bằng Google đều bị từ chối, kể cả tài khoản hợp lệ.",
                ConfigurationGapSeverity.Broken));

        if (_livestream.Provider.Equals("mux", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(_mux.WebhookSecret))
            gaps.Add(new ConfigurationGap(
                "Livestream", "Mux:WebhookSecret",
                "Mọi thông báo từ Mux bị từ chối: buổi diễn không có bản xem lại, encoder rớt thì hệ thống không tự "
                + "báo 'đang kết nối lại' và không tự kết thúc.",
                ConfigurationGapSeverity.Broken));

        if (string.IsNullOrWhiteSpace(_gemini.ApiKey))
            gaps.Add(new ConfigurationGap(
                "Kiểm duyệt nội dung", "Gemini:ApiKey",
                "Hàng đợi duyệt của quản trị viên không có điểm AI, và ảnh tải lên KHÔNG được kiểm duyệt — mọi ảnh đều "
                + "được cho qua.",
                ConfigurationGapSeverity.Broken));

        var coCloudflare = !string.IsNullOrWhiteSpace(_cloudflare.AccountId)
                           && !string.IsNullOrWhiteSpace(_cloudflare.ApiToken);
        if (!coCloudflare && string.IsNullOrWhiteSpace(_openAi.ApiKey))
            gaps.Add(new ConfigurationGap(
                "Tạo poster AI", "Cloudflare:AccountId + Cloudflare:ApiToken (hoặc OpenAi:ApiKey)",
                "Chủ phòng trà đã mua gói có tính năng này nhưng mọi lần tạo poster đều thất bại.",
                ConfigurationGapSeverity.Broken));

        // MLACP-426. Truoc day bang kiem khong soi SMS, nen ra soat Azure 16/09 khong phat hien luong xac minh so dien
        // thoai chua bao gio gui duoc tin nao.
        if (string.IsNullOrWhiteSpace(_sms.AccountSid)
            || string.IsNullOrWhiteSpace(_sms.AuthToken)
            || string.IsNullOrWhiteSpace(_sms.FromNumber))
            gaps.Add(new ConfigurationGap(
                "Xác minh số điện thoại", "Sms:AccountSid + Sms:AuthToken + Sms:FromNumber",
                "Người dùng bấm gửi mã xác minh nhưng không bao giờ nhận được tin, nên không ai xác minh được số điện thoại.",
                ConfigurationGapSeverity.Broken));

        if (string.IsNullOrWhiteSpace(_firebase.CredentialsPath))
            gaps.Add(new ConfigurationGap(
                "Thông báo đẩy", "Firebase:CredentialsPath",
                "Chỉ còn thông báo trong ứng dụng; không có thông báo đẩy về điện thoại.",
                ConfigurationGapSeverity.Degraded));

        if (string.IsNullOrWhiteSpace(_business.PerformerConfirmationUrl))
            gaps.Add(new ConfigurationGap(
                "Donate cho nghệ sĩ", "Business:PerformerConfirmationUrl",
                "Email mời nghệ sĩ tự xác nhận đã nhận tiền không bao giờ được gửi, nên sao kê chỉ còn lời khai một phía.",
                ConfigurationGapSeverity.Degraded));

        if (string.IsNullOrWhiteSpace(_business.PaymentProcessingUrl))
            gaps.Add(new ConfigurationGap(
                "Thanh toán", "Business:PaymentProcessingUrl",
                "Khi VNPay chưa trả kết quả, khách bị đưa về trang báo thất bại và dễ trả tiền lần thứ hai.",
                ConfigurationGapSeverity.Degraded));

        // Chay that ma van tro ve may dev: link trong email va buoc quay ve sau thanh toan deu hong voi nguoi dung that.
        if (chayThat)
            foreach (var (key, url) in new[]
                     {
                         ("Business:PaymentSuccessUrl", _business.PaymentSuccessUrl),
                         ("Business:PaymentFailedUrl", _business.PaymentFailedUrl),
                         ("Business:PasswordResetUrl", _business.PasswordResetUrl)
                     })
                if (url.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                    gaps.Add(new ConfigurationGap(
                        "Đường dẫn cho người dùng", key,
                        "Đang trỏ về localhost nên chỉ đúng khi mở trên máy lập trình viên; người dùng thật sẽ vào "
                        + "trang không tồn tại.",
                        ConfigurationGapSeverity.Broken));

        return gaps;
    }
}
