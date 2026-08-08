using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Exceptions;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Api.Middleware;

internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, message, errors) = ex switch
        {
            NotFoundException e      => (StatusCodes.Status404NotFound,           e.Message,  (object?)null),
            UnauthorizedException e  => (StatusCodes.Status401Unauthorized,        e.Message,  null),
            ForbiddenException e     => (StatusCodes.Status403Forbidden,           e.Message,  null),
            ConflictException e      => (StatusCodes.Status409Conflict,            e.Message,  null),
            DomainException e        => (StatusCodes.Status422UnprocessableEntity, e.Message,  null),
            ValidationException e    => (StatusCodes.Status400BadRequest,          "One or more validation errors occurred.", (object?)e.Errors),
            ExternalServiceException e => (StatusCodes.Status503ServiceUnavailable, e.Message, null),
            // Race condition hiem (2 request dong thoi cung vi pham 1 unique constraint, vd trung
            // email luc dang ky) — DB da chan dung du lieu, chi can tra ve 409 sach thay vi de lot
            // thanh 500 tho gay hieu lam la loi server.
            DbUpdateException           => (StatusCodes.Status409Conflict, "Dữ liệu đã tồn tại hoặc xung đột, vui lòng thử lại.", null),
            _                        => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null)
        };

        // DbUpdateException tra 409 than thien cho nguoi dung, nhung van la 1 loi DB khong luong
        // truoc duoc (co the la unique-constraint vo hai nhu trung email, cung co the la FK
        // violation/deadlock/timeout that su) — log ERROR de operator van thay, du response
        // khong can bao dong voi nguoi dung.
        if (status >= 500 || ex is DbUpdateException)
            _logger.LogError(ex, "Unhandled server error: {Message}", ex.Message);
        else
            _logger.LogWarning("Business exception {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);

        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode = status;

        await ctx.Response.WriteAsJsonAsync(
            new { success = false, message, errors },
            ct);

        return true;
    }
}
