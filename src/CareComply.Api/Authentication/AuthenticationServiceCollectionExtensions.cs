using System.Threading.RateLimiting;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.Authentication;
using CareComply.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CareComply.Api.Authentication;

public static class CareComplyAuthenticationServiceCollectionExtensions
{
    public const string LoginRateLimitPolicy = "authentication-login";
    public const string MfaRateLimitPolicy = "authentication-mfa";
    public const string MfaEmailResendRateLimitPolicy = "authentication-mfa-email-resend";
    public const string ForgotPasswordRateLimitPolicy = "authentication-password-forgot";
    public const string ResetPasswordRateLimitPolicy = "authentication-password-reset";

    private const int DefaultLoginPermitLimit = 10;
    private const int DefaultMfaPermitLimit = 5;
    private const int DefaultMfaEmailResendPermitLimit = 3;
    private const int DefaultForgotPasswordPermitLimit = 5;
    private const int DefaultResetPasswordPermitLimit = 10;

    public static IServiceCollection AddCareComplyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, HttpContextCurrentActor>();
        services.AddScoped<CareComplyCookiePrincipalValidator>();
        services.AddScoped<CareComplyAntiforgeryFilter>();

        var authentication = services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            });
        authentication.AddIdentityCookies();
        authentication.AddCookie(MfaAuthenticationDefaults.FlowScheme);

        new IdentityBuilder(typeof(ApplicationUser), typeof(ApplicationRole), services)
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<CookieAuthenticationOptions>(
            IdentityConstants.ApplicationScheme,
            options =>
            {
                options.Cookie.Name = "__Host-CareComply.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
                options.Cookie.Domain = null;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
                options.SlidingExpiration = true;
                options.EventsType = typeof(CareComplyCookiePrincipalValidator);
            });

        ConfigureTemporaryCookie(
            services,
            IdentityConstants.TwoFactorUserIdScheme,
            "__Host-CareComply.Mfa");
        ConfigureTemporaryCookie(
            services,
            MfaAuthenticationDefaults.FlowScheme,
            "__Host-CareComply.MfaContext");

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "__Host-CareComply.XSRF";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });

        var loginPermitLimit = configuration.GetValue<int?>(
            "Authentication:LoginRateLimit:PermitLimit") ?? DefaultLoginPermitLimit;
        var mfaPermitLimit = configuration.GetValue<int?>(
            "Authentication:Mfa:VerificationRateLimitPermitLimit") ?? DefaultMfaPermitLimit;
        var mfaEmailResendPermitLimit = configuration.GetValue<int?>(
            "Authentication:Mfa:EmailResendRateLimitPermitLimit")
            ?? DefaultMfaEmailResendPermitLimit;
        var forgotPasswordPermitLimit = configuration.GetValue<int?>(
            "Authentication:PasswordRecovery:ForgotRateLimitPermitLimit")
            ?? DefaultForgotPasswordPermitLimit;
        var resetPasswordPermitLimit = configuration.GetValue<int?>(
            "Authentication:PasswordRecovery:ResetRateLimitPermitLimit")
            ?? DefaultResetPasswordPermitLimit;

        if (loginPermitLimit <= 0)
        {
            throw new InvalidOperationException(
                "Authentication login rate-limit permit count must be greater than zero.");
        }

        if (mfaPermitLimit <= 0 || mfaEmailResendPermitLimit <= 0)
        {
            throw new InvalidOperationException(
                "Authentication MFA rate-limit permit count must be greater than zero.");
        }

        if (forgotPasswordPermitLimit <= 0 || resetPasswordPermitLimit <= 0)
        {
            throw new InvalidOperationException(
                "Authentication password-recovery rate-limit permit counts must be greater than zero.");
        }

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = loginPermitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    }));
            options.AddPolicy(MfaRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = mfaPermitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    }));
            options.AddPolicy(MfaEmailResendRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = mfaEmailResendPermitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    }));
            options.AddPolicy(ForgotPasswordRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = forgotPasswordPermitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    }));
            options.AddPolicy(ResetPasswordRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = resetPasswordPermitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var isMfaRequest = context.HttpContext.Request.Path.StartsWithSegments(
                    "/api/auth/mfa",
                    StringComparison.OrdinalIgnoreCase);
                var isPasswordRecoveryRequest = context.HttpContext.Request.Path.StartsWithSegments(
                    "/api/auth/password",
                    StringComparison.OrdinalIgnoreCase);
                var problemDetailsService = context.HttpContext.RequestServices
                    .GetRequiredService<IProblemDetailsService>();
                await problemDetailsService.TryWriteAsync(
                    new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = isMfaRequest
                                ? "Too many verification attempts."
                                : isPasswordRecoveryRequest
                                    ? "Too many password recovery attempts."
                                    : "Too many authentication attempts.",
                            Type = isMfaRequest
                                ? ProblemTypes.MfaRateLimit
                                : isPasswordRecoveryRequest
                                    ? ProblemTypes.PasswordRecoveryRateLimit
                                    : ProblemTypes.RateLimit,
                        },
                    });
            };
        });

        return services;
    }

    private static void ConfigureTemporaryCookie(
        IServiceCollection services,
        string scheme,
        string name) =>
        services.Configure<CookieAuthenticationOptions>(scheme, options =>
        {
            options.Cookie.Name = name;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
            options.Cookie.Domain = null;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            options.SlidingExpiration = false;
        });
}
