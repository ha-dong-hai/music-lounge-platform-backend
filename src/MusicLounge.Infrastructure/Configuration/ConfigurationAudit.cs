using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Configuration;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Configuration;

/// <summary>
/// MLACP-420. Moi muc o day deu la mot su co da xay ra that, khong phai canh bao phong xa:
/// Firebase:ProjectId (dang nhap Google hong nhieu tuan), Mux:WebhookSecret (mat chuyen trang thai livestream tu dong), Gemini:ApiKey
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
    private readonly EmailSettings _email;
    private readonly PanoramaStitcherSettings _stitcher;
    private readonly PosterWorkerSettings _posterWorker;
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
        IOptions<EmailSettings> email,
        IOptions<PanoramaStitcherSettings> stitcher,
        IOptions<PosterWorkerSettings> posterWorker,
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
        _email = email.Value;
        _stitcher = stitcher.Value;
        _posterWorker = posterWorker.Value;
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
                // MLACP-428: truoc day cau nay noi "khong co ban xem lai" — nhung livestream khong co tinh nang xem lai
                // (chu xac nhan 16/09). Webhook Mux chi lo chuyen trang thai tu dong.
                "Mọi thông báo từ Mux bị từ chối: encoder rớt thì hệ thống không tự báo 'đang kết nối lại', và buổi "
                + "phát không tự kết thúc khi ngừng phát — chủ phòng trà phải tự bấm kết thúc.",
                ConfigurationGapSeverity.Broken));

        if (string.IsNullOrWhiteSpace(_gemini.ApiKey))
            gaps.Add(new ConfigurationGap(
                "Kiểm duyệt nội dung", "Gemini:ApiKey",
                "Hàng đợi duyệt của quản trị viên không có điểm AI, và ảnh tải lên KHÔNG được kiểm duyệt — mọi ảnh đều "
                + "được cho qua.",
                ConfigurationGapSeverity.Broken));

        // MLACP-479: chế độ hàng đợi (máy trạm chạy Google Flow) là hướng CHÍNH của tính năng poster, nhưng bảng
        // kiểm này chưa bao giờ soi nó — nó chỉ soi đường gọi thẳng. Hỏng kiểu im lặng: bật cờ mà quên khoá thì hệ
        // thống lặng lẽ quay về nhà cung cấp gọi thẳng, còn máy trạm gọi /poster-jobs/claim nhận 401, và người vận
        // hành không có chỗ nào nhìn ra hai việc đó liên quan tới nhau.
        // Dùng ĐÚNG hàm quyết định chế độ (AiImageProvider.UseDeferredQueue) chứ không chép lại luật — chép lại là
        // để hai nơi trôi ra khỏi nhau.
        var dungHangDoi = AiImageProvider.UseDeferredQueue(_posterWorker);

        if (_posterWorker.Enabled && string.IsNullOrWhiteSpace(_posterWorker.ApiKey))
            gaps.Add(new ConfigurationGap(
                "Tạo poster AI", "PosterWorker:ApiKey",
                "Đã bật chế độ hàng đợi nhưng thiếu khoá, nên hệ thống lặng lẽ quay về nhà cung cấp gọi thẳng và máy "
                + "trạm bị từ chối với lỗi 401 — nhìn từ ngoài giống hệt lỗi lập trình.",
                ConfigurationGapSeverity.Broken));

        var coCloudflare = !string.IsNullOrWhiteSpace(_cloudflare.AccountId)
                           && !string.IsNullOrWhiteSpace(_cloudflare.ApiToken);
        // Chỉ báo thiếu nhà cung cấp gọi thẳng khi KHÔNG chạy hàng đợi — chạy hàng đợi thì không cần tới họ, và báo
        // thừa ở đây sẽ dạy người vận hành bỏ qua bảng kiểm.
        if (!dungHangDoi && !coCloudflare && string.IsNullOrWhiteSpace(_openAi.ApiKey))
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

        // MLACP-428. Truoc day bang kiem khong soi SMTP: thieu thi ba loai email quan trong am tham khong gui.
        if (string.IsNullOrWhiteSpace(_email.Host))
            gaps.Add(new ConfigurationGap(
                "Email", "Email:Host",
                "Không gửi được email đặt lại mật khẩu, mã xác minh email và email mời nghệ sĩ tự xác nhận đã nhận "
                + "tiền — người dùng quên mật khẩu không tự lấy lại được tài khoản.",
                ConfigurationGapSeverity.Broken));

        // MLACP-428. Dang thieu that tren Azure (ra soat 17/09). Suy giam chu chua chet han: van tai duoc anh 360 dung san.
        // Soi du ca ba thiet lap ma HttpPanoramaStitchingService.IsConfiguredFor doi — thieu bat ky cai nao thi chu phong
        // tra bam ghep deu bi bao "tam ngung", nen chi soi BaseUrl la bao xanh sai:
        //   - ApiKey (MLACP-431): dich vu ghep anh tu choi moi yeu cau khong kem khoa.
        //   - PublicBaseUrl: chi can khi anh luu tren dia cuc bo (duong dan tuong doi phai noi thanh URL day du de dich vu
        //     tai ve). Kho Firebase tra URL tuyet doi nen khong can.
        var thieuGhepAnh = new List<string>();
        if (string.IsNullOrWhiteSpace(_stitcher.BaseUrl)) thieuGhepAnh.Add("PanoramaStitcher:BaseUrl");
        if (string.IsNullOrWhiteSpace(_stitcher.ApiKey)) thieuGhepAnh.Add("PanoramaStitcher:ApiKey");
        if (!FileStorageSelector.UseFirebase(_firebase) && string.IsNullOrWhiteSpace(_stitcher.PublicBaseUrl))
            thieuGhepAnh.Add("PanoramaStitcher:PublicBaseUrl");
        if (thieuGhepAnh.Count > 0)
            gaps.Add(new ConfigurationGap(
                "Tour 360° phòng trà", string.Join(" + ", thieuGhepAnh),
                "Không ghép được nhiều ảnh thường thành ảnh 360; chủ phòng trà chỉ tải lên được ảnh 360 đã dựng sẵn.",
                ConfigurationGapSeverity.Degraded));

        // MLACP-484: khai đường dẫn mà file không có cũng là thiếu — và là ca NGUY HIỂM HƠN ca bỏ
        // trống. Bỏ trống thì bảng kiểm báo thiếu và ai cũng thấy; khai rồi mà file biến mất thì bảng
        // kiểm báo "ổn" trong khi FcmService.TryEnsureInitialized (:110) chỉ ghi một dòng cảnh báo
        // rồi im lặng không gửi gì nữa. Đúng trạng thái hệ thống rơi vào ngày 23/09/2026 sau khi dời
        // vùng: /home bị xoá cùng app nên khoá mất, còn app setting thì được nạp lại nguyên vẹn.
        //
        // Tách hai thông điệp vì hai ca cần hai hành động khác hẳn nhau: một bên là khai báo còn
        // thiếu, bên kia là khai đúng nhưng file đã mất và phải tải lại.
        var khoaFirebase = string.IsNullOrWhiteSpace(_firebase.CredentialsPath)
            ? "Firebase:CredentialsPath"
            : File.Exists(_firebase.CredentialsPath) ? null
            : $"Firebase:CredentialsPath (đã khai \"{_firebase.CredentialsPath}\" nhưng không có file ở đó)";
        if (khoaFirebase is not null)
            gaps.Add(new ConfigurationGap(
                "Thông báo đẩy", khoaFirebase,
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
