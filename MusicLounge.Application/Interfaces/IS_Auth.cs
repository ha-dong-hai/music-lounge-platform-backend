using System.Threading.Tasks;
using MusicLounge.Application.Common;
using MusicLounge.Application.DTOs.Auth;

namespace MusicLounge.Application.Interfaces;

public interface IS_Auth
{
    Task<ResponseData<MRes_Register>> Register(MReq_Register request);

    Task<ResponseData<MRes_Auth>> VerifyEmail(MReq_VerifyEmail request);

    Task<ResponseData<object>> ResendVerificationCode(MReq_ResendVerificationCode request);

    Task<ResponseData<MRes_Auth>> Login(MReq_Login request);

    Task<ResponseData<MRes_UserProfile>> GetProfile(int userId);

    Task<ResponseData<MRes_UserProfile>> UpdateProfile(int userId, MReq_UpdateProfile request);

    Task<ResponseData<MRes_UserProfile>> UpdateCitizenCard(int userId, MReq_UpdateCitizenCard request);

    Task<ResponseData<object>> DeleteAccount(int userId);
}
