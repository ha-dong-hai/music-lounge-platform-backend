using Hangfire;
using MusicLounge.Application.Auth.Jobs;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// MLACP-426: lop boc chi de dat gioi han thu lai cho job gui SMS xac minh. Tang Application khong tham chieu Hangfire nen
/// thuoc tinh khong dat duoc len SendPhoneVerificationCodeJob.
///
/// Vi sao 3 lan: ma OTP song 10 phut. Cong thuc gian cach trong ma nguon Hangfire la
/// (attempt - 1)^4 + 15 + random(0..29) * attempt giay; truong hop xau nhat cua 3 lan thu lai, cong moi lan cho tron
/// timeout HTTP 30 giay, la khoang 5,9 phut — ma van con han khi toi tay nguoi dung. Mac dinh 10 lan keo dai nhieu gio,
/// gui ma da het han va ton tien SMS. Test PhoneVerificationSmsFlowTests tinh lai rang buoc nay tu thoi han that.
///
/// Het luot thi job nam o danh sach Failed cua Hangfire (mac dinh), khong xoa — de con dau vet ma xem.
/// </summary>
[AutomaticRetry(Attempts = 3)]
public sealed class PhoneVerificationSmsJob
{
    private readonly SendPhoneVerificationCodeJob _inner;

    public PhoneVerificationSmsJob(SendPhoneVerificationCodeJob inner) => _inner = inner;

    public Task ExecuteAsync(string toPhone, string protectedCode, CancellationToken ct = default)
        => _inner.ExecuteAsync(toPhone, protectedCode, ct);
}
