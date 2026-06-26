using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MusicLounge.Application.Common;
using MusicLounge.Application.DTOs.Auth;
using MusicLounge.Application.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Services;

public class S_Auth : IS_Auth
{
    private const string DefaultRole = "Audience";
    private const string FirebaseProvider = "Firebase";
    private const int VerificationExpiredMinutes = 10;

    private readonly IAuthRepository _authRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IEmailSenderService _emailSenderService;

    public S_Auth(
        IAuthRepository authRepository,
        IJwtTokenService jwtTokenService,
        IPasswordHasherService passwordHasherService,
        IEmailSenderService emailSenderService)
    {
        _authRepository = authRepository;
        _jwtTokenService = jwtTokenService;
        _passwordHasherService = passwordHasherService;
        _emailSenderService = emailSenderService;
    }

    public async Task<ResponseData<MRes_Register>> Register(MReq_Register request)
    {
        var res = new ResponseData<MRes_Register>();
        try
        {
            if (request == null)
            {
                return BadRequest(res, "Dữ liệu không hợp lệ");
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var password = request.Password.Trim();
            var fullName = request.FullName.Trim();
            var phone = request.Phone?.Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
            {
                return BadRequest(res, "Vui lòng nhập đầy đủ thông tin đăng ký");
            }

            var existingUser = await _authRepository.GetByEmail(email);
            var verificationCode = GenerateVerificationCode();
            var now = DateTime.UtcNow;
            var expiredAt = now.AddMinutes(VerificationExpiredMinutes);

            if (existingUser != null)
            {
                if (existingUser.IsActive || existingUser.EmailVerifiedAt.HasValue)
                {
                    return BadRequest(res, "Email đã được đăng ký");
                }

                existingUser.PasswordHash = _passwordHasherService.HashPassword(password);
                existingUser.FullName = fullName;
                existingUser.Phone = phone;
                existingUser.EmailVerificationCode = verificationCode;
                existingUser.EmailVerificationCodeExpiredAt = expiredAt;
                existingUser.UpdatedAt = now;

                await _authRepository.SaveChanges();
                await _emailSenderService.SendVerificationCode(email, fullName, verificationCode, VerificationExpiredMinutes);

                res.data = MapRegisterResponse(existingUser);
                res.result = 1;
                res.error.code = 200;
                res.error.message = "Tài khoản chưa xác thực, đã gửi lại mã xác thực";
                return res;
            }

            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasherService.HashPassword(password),
                FullName = fullName,
                Phone = phone,
                Role = DefaultRole,
                AuthProvider = "Local",
                EmailVerificationCode = verificationCode,
                EmailVerificationCodeExpiredAt = expiredAt,
                IsActive = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _authRepository.Add(user);
            await _authRepository.SaveChanges();
            await _emailSenderService.SendVerificationCode(email, fullName, verificationCode, VerificationExpiredMinutes);

            res.data = MapRegisterResponse(user);
            res.result = 1;
            res.error.code = 201;
            res.error.message = "Đăng ký thành công, vui lòng kiểm tra email để xác thực tài khoản";
            return res;
        }
        catch (Exception ex)
        {
            return CatchException(res, ex);
        }
    }

    public async Task<ResponseData<MRes_Auth>> VerifyEmail(MReq_VerifyEmail request)
    {
        var res = new ResponseData<MRes_Auth>();
        try
        {
            if (request == null)
            {
                return BadRequest(res, "Dữ liệu không hợp lệ");
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var verificationCode = request.VerificationCode.Trim();
            var user = await _authRepository.GetByEmail(email);

            if (user == null)
            {
                return BadRequest(res, "Không tìm thấy tài khoản");
            }

            if (user.IsActive && user.EmailVerifiedAt.HasValue)
            {
                return BadRequest(res, "Tài khoản đã được xác thực trước đó");
            }

            if (string.IsNullOrWhiteSpace(user.EmailVerificationCode) || user.EmailVerificationCode != verificationCode)
            {
                return BadRequest(res, "Mã xác thực không đúng");
            }

            if (!user.EmailVerificationCodeExpiredAt.HasValue || user.EmailVerificationCodeExpiredAt.Value < DateTime.UtcNow)
            {
                return BadRequest(res, "Mã xác thực đã hết hạn");
            }

            user.IsActive = true;
            user.EmailVerifiedAt = DateTime.UtcNow;
            user.EmailVerificationCode = null;
            user.EmailVerificationCodeExpiredAt = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _authRepository.SaveChanges();

            res.data = MapAuthResponse(user);
            res.result = 1;
            res.error.code = 200;
            res.error.message = "Xác thực email thành công";
            return res;
        }
        catch (Exception ex)
        {
            return CatchException(res, ex);
        }
    }

    public async Task<ResponseData<object>> ResendVerificationCode(MReq_ResendVerificationCode request)
    {
        var res = new ResponseData<object>();
        try
        {
            if (request == null)
            {
                return BadRequest(res, "Dữ liệu không hợp lệ");
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _authRepository.GetByEmail(email);
            if (user == null)
            {
                return BadRequest(res, "Không tìm thấy tài khoản");
            }

            if (user.IsActive || user.EmailVerifiedAt.HasValue)
            {
                return BadRequest(res, "Tài khoản đã được xác thực");
            }

            user.EmailVerificationCode = GenerateVerificationCode();
            user.EmailVerificationCodeExpiredAt = DateTime.UtcNow.AddMinutes(VerificationExpiredMinutes);
            user.UpdatedAt = DateTime.UtcNow;
            await _authRepository.SaveChanges();
            await _emailSenderService.SendVerificationCode(user.Email, user.FullName, user.EmailVerificationCode, VerificationExpiredMinutes);

            res.result = 1;
            res.error.code = 200;
            res.error.message = "Đã gửi lại mã xác thực qua email";
            return res;
        }
        catch (Exception ex)
        {
            return CatchException(res, ex);
        }
    }

    public async Task<ResponseData<MRes_Auth>> Login(MReq_Login request)
    {
        var res = new ResponseData<MRes_Auth>();
        try
        {
            if (request == null)
            {
                return BadRequest(res, "Dữ liệu không hợp lệ");
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var password = request.Password.Trim();

            var user = await _authRepository.GetByEmail(email);
            if (user == null)
            {
                return BadRequest(res, "Email hoặc mật khẩu không đúng");
            }

            if (!user.IsActive || !user.EmailVerifiedAt.HasValue)
            {
                return BadRequest(res, "Tài khoản chưa xác thực email");
            }

            var isValidPassword = _passwordHasherService.VerifyPassword(password, user.PasswordHash);
            if (!isValidPassword)
            {
                return BadRequest(res, "Email hoặc mật khẩu không đúng");
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _authRepository.SaveChanges();

            res.data = MapAuthResponse(user);
            res.result = 1;
            res.error.code = 200;
            res.error.message = "Đăng nhập thành công";
            return res;
        }
        catch (Exception ex)
        {
            return CatchException(res, ex);
        }
    }

    public async Task<ResponseData<MRes_UserProfile>> GetProfile(int userId)
    {
        var res = new ResponseData<MRes_UserProfile>();
        try
        {
            var user = await _authRepository.GetByIdAsNoTracking(userId);
            if (user == null)
            {
                return BadRequest(res, "Không tìm thấy tài khoản");
            }

            res.data = MapProfileResponse(user);
            res.result = 1;
            res.error.code = 200;
            return res;
        }
        catch (Exception ex)
        {
            return CatchException(res, ex);
        }
    }

    public async Task<ResponseData<MRes_UserProfile>> UpdateProfile(int userId, MReq_UpdateProfile request)
    {
        var res = new ResponseData<MRes_UserProfile>();
        try
        {
            if (request == null)
            {
                return BadRequest(res, "Dữ liệu không hợp lệ");
            }

            var user = await _authRepository.GetById(userId);
            if (user == null)
            {
                return BadRequest(res, "Không tìm thấy tài khoản");
            }

            user.FullName = request.FullName.Trim();
            user.Phone = request.Phone?.Trim();
            user.AvatarUrl = request.AvatarUrl?.Trim();
            user.UpdatedAt = DateTime.UtcNow;

            await _authRepository.SaveChanges();

            res.data = MapProfileResponse(user);
            res.result = 1;
            res.error.code = 200;
            res.error.message = "Cập nhật hồ sơ thành công";
            return res;
        }
        catch (Exception ex)
        {
            return CatchException(res, ex);
        }
    }

    public async Task<ResponseData<MRes_UserProfile>> UpdateCitizenCard(int userId, MReq_UpdateCitizenCard request)
    {
        var res = new ResponseData<MRes_UserProfile>();
        try
        {
            if (request == null)
            {
                return BadRequest(res, "Dữ liệu không hợp lệ");
            }

            var citizenCardNumber = request.CitizenCardNumber.Trim();
            var frontImageUrl = request.CitizenCardFrontImageUrl.Trim();
            var backImageUrl = request.CitizenCardBackImageUrl.Trim();
            var storageProvider = string.IsNullOrWhiteSpace(request.StorageProvider)
                ? FirebaseProvider
                : request.StorageProvider.Trim();

            var duplicateCitizenCard = await _authRepository.IsCitizenCardNumberExists(userId, citizenCardNumber);
            if (duplicateCitizenCard)
            {
                return BadRequest(res, "Số CCCD đã được sử dụng");
            }

            var user = await _authRepository.GetById(userId);
            if (user == null)
            {
                return BadRequest(res, "Không tìm thấy tài khoản");
            }

            user.CitizenCardNumber = citizenCardNumber;
            user.CitizenCardFrontImageUrl = frontImageUrl;
            user.CitizenCardBackImageUrl = backImageUrl;
            user.CitizenCardStorageProvider = storageProvider;
            user.CitizenCardUpdatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            await _authRepository.SaveChanges();

            res.data = MapProfileResponse(user);
            res.result = 1;
            res.error.code = 200;
            res.error.message = "Cập nhật CCCD thành công";
            return res;
        }
        catch (Exception ex)
        {
            return CatchException(res, ex);
        }
    }

    public async Task<ResponseData<object>> DeleteAccount(int userId)
    {
        var res = new ResponseData<object>();
        try
        {
            var user = await _authRepository.GetById(userId);
            if (user == null)
            {
                return BadRequest(res, "Không tìm thấy tài khoản");
            }

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _authRepository.SaveChanges();

            res.result = 1;
            res.error.code = 200;
            res.error.message = "Xóa tài khoản thành công";
            return res;
        }
        catch (Exception ex)
        {
            return CatchException(res, ex);
        }
    }

    private static string GenerateVerificationCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1000000);
        return value.ToString("D6");
    }

    private static MRes_Register MapRegisterResponse(User user)
    {
        return new MRes_Register
        {
            Email = user.Email,
            FullName = user.FullName,
            IsActive = user.IsActive,
            IsVerificationRequired = true,
            VerificationCodeExpiredAt = user.EmailVerificationCodeExpiredAt
        };
    }

    private MRes_Auth MapAuthResponse(User user)
    {
        var response = new MRes_Auth
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            IsEmailVerified = user.EmailVerifiedAt.HasValue,
            EmailVerifiedAt = user.EmailVerifiedAt,
            CitizenCardNumber = user.CitizenCardNumber,
            CitizenCardFrontImageUrl = user.CitizenCardFrontImageUrl,
            CitizenCardBackImageUrl = user.CitizenCardBackImageUrl,
            CitizenCardStorageProvider = user.CitizenCardStorageProvider,
            CitizenCardUpdatedAt = user.CitizenCardUpdatedAt,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            AccessToken = _jwtTokenService.GenerateToken(user),
            ExpiresAtUtc = _jwtTokenService.GetTokenExpiryUtc()
        };

        return response;
    }

    private static MRes_UserProfile MapProfileResponse(User user)
    {
        return new MRes_UserProfile
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            IsEmailVerified = user.EmailVerifiedAt.HasValue,
            EmailVerifiedAt = user.EmailVerifiedAt,
            CitizenCardNumber = user.CitizenCardNumber,
            CitizenCardFrontImageUrl = user.CitizenCardFrontImageUrl,
            CitizenCardBackImageUrl = user.CitizenCardBackImageUrl,
            CitizenCardStorageProvider = user.CitizenCardStorageProvider,
            CitizenCardUpdatedAt = user.CitizenCardUpdatedAt,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private static ResponseData<T> BadRequest<T>(ResponseData<T> res, string message)
    {
        res.result = 0;
        res.error.code = 400;
        res.error.message = message;
        return res;
    }

    private static ResponseData<T> CatchException<T>(ResponseData<T> res, Exception ex)
    {
        res.result = -1;
        res.error.code = 500;
        res.error.message = ex.Message;
        return res;
    }
}
