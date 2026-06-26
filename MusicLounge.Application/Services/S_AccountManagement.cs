using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common;
using MusicLounge.Application.DTOs.AccountManagement;
using MusicLounge.Application.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Services;

public class S_AccountManagement : IS_AccountManagement
{
    private const string AdminRole = "Admin";

    private readonly IAccountManagementRepository _accountManagementRepository;
    private readonly IPasswordHasherService _passwordHasherService;

    public S_AccountManagement(
        IAccountManagementRepository accountManagementRepository,
        IPasswordHasherService passwordHasherService)
    {
        _accountManagementRepository = accountManagementRepository;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<ResponseData<MRes_AccountManagement>> Create(MReq_AccountManagementCreate request)
    {
        var res = new ResponseData<MRes_AccountManagement>();
        try
        {
            if (request == null)
            {
                return BadRequest(res, "Dữ liệu không hợp lệ");
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var isEmailExists = await _accountManagementRepository.IsEmailExists(email);
            if (isEmailExists)
            {
                return BadRequest(res, "Email đã tồn tại");
            }

            var now = DateTime.UtcNow;
            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasherService.HashPassword(request.Password.Trim()),
                FullName = request.FullName.Trim(),
                Phone = request.Phone?.Trim(),
                AvatarUrl = request.AvatarUrl?.Trim(),
                Role = request.Role.Trim(),
                AuthProvider = "AdminCreated",
                IsActive = request.IsActive,
                EmailVerifiedAt = request.IsActive ? now : null,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _accountManagementRepository.Add(user);
            await _accountManagementRepository.SaveChanges();

            res.data = MapResponse(user);
            res.result = 1;
            res.error.code = 201;
            res.error.message = "Tạo tài khoản thành công";
            return res;
        }
        catch (Exception ex)
        {
            return CatchException(res, ex);
        }
    }

    public async Task<ResponseData<List<MRes_AccountManagement>>> GetAll(MReq_AccountManagementGetAll request)
    {
        var res = new ResponseData<List<MRes_AccountManagement>>();
        try
        {
            request ??= new MReq_AccountManagementGetAll();
            var page = request.Page <= 0 ? 1 : request.Page;
            var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

            var query = _accountManagementRepository.GetQueryable()
                .AsNoTracking()
                .Where(x => x.Role != AdminRole);

            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                var searchText = request.SearchText.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    x.Email.ToLower().Contains(searchText)
                    || x.FullName.ToLower().Contains(searchText)
                    || (x.Phone != null && x.Phone.Contains(searchText)));
            }

            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                var role = request.Role.Trim().ToLowerInvariant();
                query = query.Where(x => x.Role.ToLower() == role);
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(x => x.IsActive == request.IsActive.Value);
            }

            if (request.IsEmailVerified.HasValue)
            {
                if (request.IsEmailVerified.Value)
                {
                    query = query.Where(x => x.EmailVerifiedAt.HasValue);
                }
                else
                {
                    query = query.Where(x => !x.EmailVerifiedAt.HasValue);
                }
            }

            var totalRecords = await query.CountAsync();
            var data = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new MRes_AccountManagement
                {
                    Id = x.Id,
                    Email = x.Email,
                    FullName = x.FullName,
                    Phone = x.Phone,
                    AvatarUrl = x.AvatarUrl,
                    Role = x.Role,
                    IsActive = x.IsActive,
                    IsEmailVerified = x.EmailVerifiedAt.HasValue,
                    EmailVerifiedAt = x.EmailVerifiedAt,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();

            res.data = data;
            res.data2nd = new
            {
                page,
                pageSize,
                totalRecords,
                totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
            };
            res.result = 1;
            res.error.code = 200;
            return res;
        }
        catch (Exception ex)
        {
            return CatchException(res, ex);
        }
    }

    public async Task<ResponseData<MRes_AccountManagement>> GetDetail(int id)
    {
        var res = new ResponseData<MRes_AccountManagement>();
        try
        {
            var user = await _accountManagementRepository.GetById(id);
            if (user == null)
            {
                return BadRequest(res, "Không tìm thấy tài khoản");
            }

            res.data = MapResponse(user);
            res.result = 1;
            res.error.code = 200;
            return res;
        }
        catch (Exception ex)
        {
            return CatchException(res, ex);
        }
    }

    public async Task<ResponseData<MRes_AccountManagement>> Update(MReq_AccountManagementUpdate request)
    {
        var res = new ResponseData<MRes_AccountManagement>();
        try
        {
            if (request == null)
            {
                return BadRequest(res, "Dữ liệu không hợp lệ");
            }

            var user = await _accountManagementRepository.GetById(request.Id);
            if (user == null)
            {
                return BadRequest(res, "Không tìm thấy tài khoản");
            }

            user.FullName = request.FullName.Trim();
            user.Phone = request.Phone?.Trim();
            user.AvatarUrl = request.AvatarUrl?.Trim();
            user.Role = request.Role.Trim();
            user.IsActive = request.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            if (!request.IsActive)
            {
                user.EmailVerificationCode = null;
                user.EmailVerificationCodeExpiredAt = null;
            }

            if (request.IsActive && !user.EmailVerifiedAt.HasValue)
            {
                user.EmailVerifiedAt = DateTime.UtcNow;
            }

            await _accountManagementRepository.SaveChanges();

            res.data = MapResponse(user);
            res.result = 1;
            res.error.code = 200;
            res.error.message = "Cập nhật tài khoản thành công";
            return res;
        }
        catch (Exception ex)
        {
            return CatchException(res, ex);
        }
    }

    public async Task<ResponseData<object>> Delete(int id)
    {
        var res = new ResponseData<object>();
        try
        {
            var user = await _accountManagementRepository.GetById(id);
            if (user == null)
            {
                return BadRequest(res, "Không tìm thấy tài khoản");
            }

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _accountManagementRepository.SaveChanges();

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

    private static MRes_AccountManagement MapResponse(User user)
    {
        return new MRes_AccountManagement
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            IsActive = user.IsActive,
            IsEmailVerified = user.EmailVerifiedAt.HasValue,
            EmailVerifiedAt = user.EmailVerifiedAt,
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
