using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MusicLounge.Api.Swagger;

// Chi gan icon khoa (yeu cau Bearer) cho dung nhung endpoint co [Authorize] va khong bi
// [AllowAnonymous] o cap action ghi de — neu dung mot security requirement toan cuc thi Swagger UI
// se hien khoa tren ca endpoint public (vd /auth/login, GET /lounge-shows), gay hieu lam cho FE.
public sealed class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorize =
            context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() == true
            || context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();
        var hasAllowAnonymous =
            context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any();
        // Endpoint AllowAnonymous nhung handler doc ICurrentUserService de ca nhan hoa/gate 1 nhanh
        // khi co token (vd ?mine=true) — can security scheme de nut Authorize cua Swagger UI thuc
        // su gui token cho no, xem SwaggerOptionalAuthAttribute.
        var hasOptionalAuth =
            context.MethodInfo.GetCustomAttributes(true).OfType<SwaggerOptionalAuthAttribute>().Any();

        var requiresAuth = hasAuthorize && !hasAllowAnonymous;
        if (!requiresAuth && !hasOptionalAuth)
            return;

        // Chi endpoint thuc su bat buoc dang nhap moi tai lieu 401/403 — endpoint optional-auth
        // van hoat dong binh thuong khi anonymous, no chi ca nhan hoa khac di khi co token.
        if (requiresAuth)
        {
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                }] = []
            }
        ];
    }
}
