using Asp.Versioning;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MusicLounge.Api.Authorization;
using MusicLounge.Api.Middleware;
using MusicLounge.Application;
using MusicLounge.Infrastructure;
using MusicLounge.Infrastructure.Hubs;
using MusicLounge.Infrastructure.Persistence;
using Serilog;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // .gitignore already documents "real values belong in appsettings.*.Local.json" as the
    // intended way to keep secrets out of tracked files, but nothing ever actually loaded such a
    // file — appsettings.json and appsettings.Development.json had real credentials (SQL password,
    // VNPay sandbox secret, Mux API token, JWT signing key) committed directly because the
    // documented escape hatch didn't exist. Wiring it up here makes that pattern actually work.
    builder.Configuration.AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.Local.json", optional: true, reloadOnChange: true);

    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services));

    builder.Services.AddControllers(opt => opt.Filters.Add(new MusicLounge.Api.Filters.ClampPaginationActionFilter()))
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
        // [ApiController] tu short-circuit truoc khi vao MediatR handler khi ModelState invalid
        // (thieu field bat buoc — Nullable=enable khien moi property string khong-null la required
        // ngam; enum sai trong query/route; JSON malformed) va mac dinh tra ve RFC7807
        // ValidationProblemDetails {type,title,status,errors,traceId} — khac hoan toan shape
        // {success,message,errors} ma GlobalExceptionHandler dung cho MOI loi nghiep vu khac. FE
        // viet 1 interceptor chung doc res.data.success se vo tinh huong nay. Ep ve cung 1 shape.
        .ConfigureApiBehaviorOptions(opt =>
        {
            opt.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(kv => kv.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

                return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new
                {
                    success = false,
                    message = "One or more validation errors occurred.",
                    errors
                });
            };
        });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opt =>
    {
        // Nut "Authorize" cho JWT — thieu doan nay thi Swagger UI khong co o nhap token,
        // moi request qua "Try it out" se luon di khong header Authorization.
        opt.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Dan token lay tu /api/v1/auth/login (khong can go chu 'Bearer ' phia truoc)."
        });
        // Gan khoa theo tung endpoint dua vao [Authorize]/[AllowAnonymous] thuc te, thay vi ap
        // security requirement toan cuc (se khoa nham ca endpoint public).
        opt.OperationFilter<MusicLounge.Api.Swagger.AuthorizeCheckOperationFilter>();
    });
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // JSON API responses (analytics/list endpoints in particular) compress well — cuts bytes over
    // the wire for mobile clients on the FE's data plan. EnableForHttps is safe here: this is a
    // pure JSON REST API, not an HTML page reflecting user input alongside an embedded secret
    // (anti-forgery token etc.) in the same response, which is the actual precondition for a
    // BREACH-style attack that HTTPS compression advice usually warns about.
    builder.Services.AddResponseCompression(opt =>
    {
        opt.EnableForHttps = true;
        opt.Providers.Add<BrotliCompressionProvider>();
        opt.Providers.Add<GzipCompressionProvider>();
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(opt => opt.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(opt => opt.Level = CompressionLevel.Fastest);

    builder.Services.AddApiVersioning(opt =>
    {
        opt.DefaultApiVersion = new ApiVersion(1, 0);
        opt.AssumeDefaultVersionWhenUnspecified = true;
        opt.ReportApiVersions = true;
        opt.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(opt =>
    {
        opt.GroupNameFormat = "'v'VVV";
        opt.SubstituteApiVersionInUrl = true;
    });

    var jwtSection = builder.Configuration.GetSection("Jwt");

    // Fail fast, not open. HMACSHA256 doesn't reject a short/empty key outright — it would just
    // sign and validate tokens with a trivially weak (or, if empty, effectively no) secret, which
    // is silently catastrophic rather than a startup crash: anyone could forge a valid admin JWT.
    // This became a live risk this session — appsettings.json's Jwt:Secret was moved to a
    // gitignored *.Local.json override (see master-backend-techlead review), so a deployment that
    // forgets to set that override, an environment variable, or a secret manager entry now starts
    // with an empty secret by default instead of a real-looking-but-committed one. Catching that
    // at startup, loudly, is strictly better than either outcome.
    var jwtSecret = jwtSection["Secret"];
    if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
        throw new InvalidOperationException(
            "Jwt:Secret is missing or shorter than 32 bytes (required for HS256 to be secure). " +
            "Set it via appsettings.{Environment}.Local.json (gitignored), an environment " +
            "variable, or a secret manager — never a real value in the tracked appsettings.json.");

    // Same fail-fast reasoning as Jwt:Secret above. These six used to default to a hardcoded
    // "https://musiclounge.vn/..." URL baked into BusinessSettings — a deployment that forgot to
    // configure them would silently send real users/VNPay redirects to that URL instead of
    // failing loudly, and a future domain change would need a code change instead of a config one.
    var businessSection = builder.Configuration.GetSection("Business");
    string[] requiredBusinessUrlKeys =
    [
        "TicketPaymentReturnUrl", "DonationPaymentReturnUrl", "SubscriptionPaymentReturnUrl",
        "PaymentSuccessUrl", "PaymentFailedUrl", "PasswordResetUrl"
    ];
    var missingBusinessUrls = requiredBusinessUrlKeys
        .Where(key => string.IsNullOrWhiteSpace(businessSection[key]))
        .ToArray();
    if (missingBusinessUrls.Length > 0)
        throw new InvalidOperationException(
            $"Business:{{{string.Join(", ", missingBusinessUrls)}}} is missing. Set them via " +
            "appsettings.{Environment}.json or .Local.json for this environment.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opt =>
        {
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidAudience = jwtSection["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSection["Secret"]!)),
                // Library default is 5 minutes if left unset — silently extends every token's
                // effective lifetime by ~8% on top of AccessTokenExpiryMinutes (60). Set explicitly
                // so it's a deliberate, documented choice rather than an implicit one.
                ClockSkew = TimeSpan.FromMinutes(1)
            };
            // SignalR sends JWT via query string for WebSocket connections
            opt.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken) &&
                        context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                },
                // JWTs are stateless and otherwise stay valid until they naturally expire — without
                // this check, ResetPassword (or an Admin deactivating a bad-actor account) had no
                // way to actually cut off a token an attacker already holds; the user could reset
                // their password and the stolen session would keep working for up to
                // Jwt:AccessTokenExpiryMinutes more. Re-checking against the DB on every request
                // costs one indexed PK lookup, projected down to just the two columns needed.
                OnTokenValidated = async context =>
                {
                    var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var stampClaim = context.Principal?.FindFirstValue("sec_stamp");
                    if (!int.TryParse(userIdClaim, out var userId) || string.IsNullOrEmpty(stampClaim))
                    {
                        context.Fail("Token không hợp lệ.");
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                    var current = await db.Users
                        .Where(u => u.Id == userId)
                        .Select(u => new { u.SecurityStamp, u.IsActive })
                        .FirstOrDefaultAsync();

                    if (current is null || !current.IsActive ||
                        !current.SecurityStamp.ToString().Equals(stampClaim, StringComparison.OrdinalIgnoreCase))
                    {
                        context.Fail("Token đã bị thu hồi — vui lòng đăng nhập lại.");
                    }
                }
            };
        });

    builder.Services.AddAuthorization(opt =>
    {
        opt.AddPolicy(Policies.RequireAuthenticated, p => p.RequireAuthenticatedUser());
        opt.AddPolicy(Policies.RequireStaff, p => p.RequireRole("Staff", "Admin"));
        opt.AddPolicy(Policies.RequireVenueOperator, p => p.RequireRole("Staff", "Owner", "Admin"));
        opt.AddPolicy(Policies.RequireOwner, p => p.RequireRole("Owner", "Admin"));
        opt.AddPolicy(Policies.RequireAdmin, p => p.RequireRole("Admin"));
    });

    builder.Services.AddSignalR();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddCors(opt =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173"];
        opt.AddPolicy("Default", p => p
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    });

    builder.Services.AddRateLimiter(opt =>
    {
        opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        // Mac dinh RateLimiter middleware chi set status code, KHONG ghi body — FE nhan 429 rong
        // hoan toan, khac voi moi response loi khac cua API (luon co {success,message,errors}) va
        // khong biet bao gio duoc goi lai. Ghi cung shape + header Retry-After chuan.
        opt.OnRejected = async (context, ct) =>
        {
            context.HttpContext.Response.ContentType = "application/json";
            if (context.Lease.TryGetMetadata(
                    System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers["Retry-After"] =
                    ((int)retryAfter.TotalSeconds).ToString();
            }

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "Bạn đã gửi quá nhiều yêu cầu, vui lòng thử lại sau.",
                errors = (object?)null
            }, ct);
        };
        // Mac dinh: 100 request/phut theo IP, ap dung toan API. Cac endpoint dung tien
        // (purchase/subscribe) da tu co idempotency/hold-based guard rieng o tang Application.
        opt.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        // Rieng auth (login/register/google) can chan brute-force/credential-stuffing chat hon
        // nhieu so voi gioi han chung — 10 request/phut/IP, cong don voi GlobalLimiter o tren.
        opt.AddPolicy("auth", ctx =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });

    builder.Services.AddHealthChecks()
        .AddCheck<MusicLounge.Api.Health.DatabaseHealthCheck>("database");

    var app = builder.Build();

    // wwwroot phai ton tai TRUOC khi UseStaticFiles() khoi tao WebRootFileProvider, neu khong file
    // upload sau nay se van 404 du duong dan dung (ASP.NET Core bind file provider 1 lan luc startup).
    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads"));

    // Rate limiter (o duoi) key theo ctx.Connection.RemoteIpAddress — sau reverse proxy (nginx/LB,
    // gan nhu chac chan co khi len production that) dia chi nay luon la IP cua proxy cho MOI
    // request, khien rate limit gop chung tat ca user lam 1 "IP" duy nhat thay vi tach theo tung
    // nguoi that. Dat middleware nay SOM NHAT co the, truoc moi thu doc RemoteIpAddress. Chi tin
    // header X-Forwarded-* tu KnownNetworks/KnownProxies mac dinh (loopback) — an toan cho kieu
    // trien khai pho bien "reverse proxy tren cung may/mang noi bo voi Kestrel"; neu topology that
    // khac (vd LB cloud rieng biet), can cau hinh them KnownProxies/KnownNetworks tuong ung.
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    // As close to the front as possible (right after ForwardedHeaders, which has to be earlier
    // still — see its own comment) so it wraps every other middleware below, including Swagger/
    // HSTS/the header-appending middleware. Registered later, an exception thrown inside any of
    // those would bypass GlobalExceptionHandler entirely and return a raw non-JSON 500, breaking
    // the {success,message,errors} shape this API otherwise guarantees for every error.
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        app.UseHsts();

    app.Use(async (ctx, next) =>
    {
        ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        ctx.Response.Headers.Append("X-Frame-Options", "DENY");
        ctx.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        // Every real response here is JSON — no script/style/image/frame is ever legitimately
        // needed, so deny all of it outright rather than trying to enumerate an allow-list.
        // frame-ancestors duplicates X-Frame-Options above (CSP's modern equivalent) — kept both
        // since browser support differs. Skipped under /swagger: Swagger UI's own bundled HTML
        // ships inline scripts/styles that this policy would break, and it's Development-only
        // tooling, never a real API response a browser needs protecting on.
        if (!ctx.Request.Path.StartsWithSegments("/swagger"))
            ctx.Response.Headers.Append(
                "Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");
        await next();
    });

    app.UseSerilogRequestLogging();
    app.UseResponseCompression();
    app.UseHttpsRedirection();
    // .glb/.gltf khong nam trong FileExtensionContentTypeProvider mac dinh — khong khai bao thi
    // StaticFileMiddleware tra 404 cho moi file model 3D du duong dan/file deu dung (ServeUnknownFileTypes
    // mac dinh la false).
    var uploadContentTypeProvider = new FileExtensionContentTypeProvider();
    uploadContentTypeProvider.Mappings[".glb"] = "model/gltf-binary";
    uploadContentTypeProvider.Mappings[".gltf"] = "model/gltf+json";
    // UseCors PHAI truoc UseStaticFiles — thu tu nguoc (nhu truoc day) khien moi response tinh
    // (/uploads/...: poster, avatar, gallery, panorama...) khong bao gio co header CORS, du policy
    // "Default" ben duoi co cho phep origin nao di nua. Khong lo ngay o API JSON binh thuong (JS
    // frontend goi bang fetch/XHR van bi CORS chan dung cach du thieu header), nhung anh tai qua
    // <img>/canvas/WebGL-texture tu 1 domain khac domain backend se luon that bai truoc lay du
    // lieu pixel (vd Pannellum ve panorama 360 bang WebGL) — phat hien khi test tinh nang tour 360.
    app.UseCors("Default");
    app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = uploadContentTypeProvider });
    // Testing host chay hang loat request lien tuc trong 1 suite (khong phai traffic that) —
    // rate limiter theo IP se tu chan chinh no. Chi bat o Development/Production.
    if (!app.Environment.IsEnvironment("Testing"))
        app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<LivestreamHub>("/hubs/livestream");
    app.MapHealthChecks("/health");

    // Configured (appsettings.json DashboardPath) but never actually mounted before this fix — see
    // HangfireDashboardAdminFilter for why it's gated to Admin-only rather than left unauthenticated.
    // Must come after UseAuthentication/UseAuthorization above so HttpContext.User is populated when
    // the filter runs.
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAdminFilter()]
    });

    // Can't call this any earlier — JobStorage.Current (which RecurringJob.AddOrUpdate needs)
    // isn't actually initialized until Hangfire Server's hosted service starts, which only
    // happens once the host itself starts (inside app.Run()), confirmed by trying to move it
    // earlier: that throws "Current JobStorage instance has not been initialized yet." The
    // ApplicationStarted race this used to worry about (a job signature changed in code racing a
    // stale definition already in SQL Server storage) is Hangfire's own concern to self-heal —
    // it already retries a failed recurring-job trigger automatically, and this call rewrites the
    // stored definition with the current signature within that same retry window.
    app.Lifetime.ApplicationStarted.Register(
        MusicLounge.Infrastructure.DependencyInjection.ConfigureRecurringJobs);

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Expose internal Program class to integration test project
public partial class Program { }
