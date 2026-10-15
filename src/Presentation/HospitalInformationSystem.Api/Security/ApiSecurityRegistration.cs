using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

namespace HospitalInformationSystem.Api.Security;

public static class ApiSecurityRegistration
{
    public const long MaxJsonBodyBytes = 64 * 1024;
    public const long MaxUploadBodyBytes = 1024 * 1024 + 64 * 1024;

    public static IServiceCollection AddApiSecurity(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var local = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        var loginPermitLimit = configuration.GetValue<int?>("Security:LoginRateLimit:PermitLimit") ?? 10;
        if (loginPermitLimit is < 1 or > 1000)
            throw new InvalidOperationException("Security:LoginRateLimit:PermitLimit must be between 1 and 1000.");
        var origins = (configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .Select(origin => NormalizeOrigin(origin, local)).Distinct(StringComparer.Ordinal).ToArray();
        services.AddHttpContextAccessor();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = local ? "MedSoft.Session" : "__Host-MedSoft.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = local ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            options.Events.OnValidatePrincipal = async context =>
            {
                if (context.Principal?.Identity?.IsAuthenticated != true)
                    return;
                var requestServices = context.HttpContext.RequestServices;
                var signInManager = requestServices.GetRequiredService<SignInManager<HospitalUser>>();
                if (await signInManager.ValidateSecurityStampAsync(context.Principal) is not null &&
                    Guid.TryParse(context.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                {
                    var accountService = requestServices.GetRequiredService<IAccountService>();
                    var session = await accountService.GetSessionAsync(userId, context.HttpContext.RequestAborted);
                    if (session is not null && MatchesSession(context.Principal, session))
                        return;
                }
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            };
        });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy(ApiSecurityPolicies.Administrator, policy =>
                policy.RequireRole(ApplicationRoles.Administrator));
            options.AddPolicy(ApiSecurityPolicies.Clinical, policy =>
                policy.RequireRole(ApplicationRoles.MedicalWorker));
            options.AddPolicy(ApiSecurityPolicies.CatalogRead, policy =>
                policy.RequireRole(ApplicationRoles.Administrator, ApplicationRoles.MedicalWorker));
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = local ? "MedSoft.Antiforgery" : "__Host-MedSoft.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = local ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(origins)
            .WithMethods("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .WithHeaders("Content-Type", "X-CSRF-TOKEN", "X-Medical-Worker-Id")
            .AllowCredentials()));
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = MaxUploadBodyBytes;
            options.MultipartHeadersLengthLimit = 4096;
            options.KeyLengthLimit = 128;
            options.ValueLengthLimit = 1024;
            options.ValueCountLimit = 16;
        });
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(180);
            options.IncludeSubDomains = false;
        });
        services.AddSingleton<FailedLoginPasswordVerifier>();
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/api")
                    ? RateLimitPartition.GetConcurrencyLimiter("api", _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = 32, QueueLimit = 0
                    })
                    : RateLimitPartition.GetNoLimiter("other"));
            options.AddPolicy(ApiSecurityPolicies.LoginRateLimit, context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = loginPermitLimit,
                        Window = TimeSpan.FromMinutes(5),
                        SegmentsPerWindow = 5,
                        QueueLimit = 0
                    }));
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Слишком много запросов.", detail: "Повторите попытку позднее.")
                    .ExecuteAsync(context.HttpContext);
            };
        });
        return services;
    }

    private static string NormalizeOrigin(string origin, bool allowHttp)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps && (!allowHttp || uri.Scheme != Uri.UriSchemeHttp) ||
            uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            uri.Host.Contains('*'))
            throw new InvalidOperationException("Cors:AllowedOrigins must contain explicit HTTPS origins (HTTP is allowed locally).");
        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static bool MatchesSession(ClaimsPrincipal principal, SessionDto session)
    {
        var roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        var workerClaims = principal.FindAll(AuthClaimTypes.MedicalWorkerId).Select(claim => claim.Value).ToArray();
        return roles.Length == 1 && roles[0] == session.Role &&
               (session.MedicalWorkerId is { } id
                   ? workerClaims.Length == 1 && Guid.TryParse(workerClaims[0], out var workerId) && workerId == id
                   : workerClaims.Length == 0);
    }
}
