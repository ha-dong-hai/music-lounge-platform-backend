using System.Threading.Tasks;

namespace MusicLounge.Application.Interfaces;

public interface IEmailSenderService
{
    Task SendVerificationCode(string toEmail, string fullName, string verificationCode, int expiredMinutes);
}
