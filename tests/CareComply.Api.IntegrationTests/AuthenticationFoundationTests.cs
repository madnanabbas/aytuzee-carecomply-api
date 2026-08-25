using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using CareComply.Api.Authentication;
using CareComply.Api.Contracts.Authentication;
using CareComply.Application.Abstractions.Identity;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CareComply.Api.IntegrationTests;

public sealed class AuthenticationFoundationTests
{
    private static readonly JsonSerializerOptions WebJsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void LoginRequestSupportsJsonBindingAndRedactsStringFormatting()
    {
        var password = $"credential-{Guid.NewGuid():N}";
        var json = JsonSerializer.Serialize(new { email = "person@example.test", password });

        var request = JsonSerializer.Deserialize<LoginRequest>(
            json,
            WebJsonOptions);

        Assert.NotNull(request);
        Assert.Equal("person@example.test", request.Email);
        Assert.Equal(password, request.Password);
        Assert.DoesNotContain(password, request.ToString(), StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", request.ToString(), StringComparison.Ordinal);

        var debuggerDisplay = Assert.Single(
            typeof(LoginRequest).GetCustomAttributes(typeof(DebuggerDisplayAttribute), false)
                .Cast<DebuggerDisplayAttribute>());
        Assert.DoesNotContain(password, debuggerDisplay.Value, StringComparison.Ordinal);
        Assert.Contains("Password=[REDACTED]", debuggerDisplay.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationCookieUsesApprovedSecurityAndSlidingExpirationOptions()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddCareComplyAuthentication(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal("__Host-CareComply.Auth", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Strict, options.Cookie.SameSite);
        Assert.Equal("/", options.Cookie.Path);
        Assert.Null(options.Cookie.Domain);
        Assert.Equal(TimeSpan.FromMinutes(15), options.ExpireTimeSpan);
        Assert.True(options.SlidingExpiration);
        using var validatorProvider = CreateValidatorServices().BuildServiceProvider();
        Assert.IsType<CareComplyCookiePrincipalValidator>(
            ActivatorUtilities.CreateInstance<CareComplyCookiePrincipalValidator>(
                validatorProvider));
    }

    [Fact]
    public void MfaCookiesAreShortLivedSecureNonPersistentSessionCookies()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Mfa:Issuer"] = "CareComply",
                ["Authentication:Mfa:RequiredPermissions:0"] = "User.Manage",
            })
            .Build();
        services.AddCareComplyAuthentication(configuration);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();

        AssertTemporaryCookie(
            options.Get(IdentityConstants.TwoFactorUserIdScheme),
            "__Host-CareComply.Mfa");
        AssertTemporaryCookie(
            options.Get(MfaAuthenticationDefaults.FlowScheme),
            "__Host-CareComply.MfaContext");
    }

    [Fact]
    public void UnknownConfiguredMfaPermissionFailsClosed()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CareComply"] =
                    "Server=localhost;Database=CareComply;User Id=test;Password=test;Encrypt=true",
                ["Authentication:Mfa:Issuer"] = "CareComply",
                ["Authentication:Mfa:RequiredPermissions:0"] = "Unknown.Manage",
            })
            .Build();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<MfaPolicyOptions>>().Value);
    }

    [Fact]
    public void MfaHttpContractsRedactSecretsFromStringFormatting()
    {
        var secret = $"secret-{Guid.NewGuid():N}";
        object[] contracts =
        [
            new MfaCodeRequest { Code = secret },
            new MfaRecoveryCodeRequest { RecoveryCode = secret },
            new MfaContextResponse("mfaRequired", "Authenticator", secret),
            new MfaEnrollmentResponse(secret, secret),
            new MfaEnrollmentConfirmedResponse([secret]),
        ];

        Assert.All(contracts, contract =>
        {
            Assert.DoesNotContain(secret, contract.ToString(), StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", contract.ToString(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task AbsoluteSessionLifetimeExpiresAtExactlyEightHours()
    {
        var startedAt = new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new AdjustableTimeProvider(startedAt);
        var sessionValidator = new StubIdentitySessionValidator();
        var authenticationService = new RecordingAuthenticationService();
        var validator = CreateValidator(timeProvider, sessionValidator);
        var identityOptions = new IdentityOptions();
        var principal = CreatePrincipal(identityOptions);
        var properties = new AuthenticationProperties();
        var scheme = CreateScheme();
        var httpContext = CreateHttpContext(authenticationService);
        var signingInContext = new CookieSigningInContext(
            httpContext,
            scheme,
            new CookieAuthenticationOptions(),
            principal,
            properties,
            new CookieOptions());

        await validator.SigningIn(signingInContext);

        timeProvider.Advance(TimeSpan.FromHours(8) - TimeSpan.FromSeconds(1));
        var beforeBoundary = CreateValidationContext(httpContext, scheme, principal, properties);
        await validator.ValidatePrincipal(beforeBoundary);
        Assert.NotNull(beforeBoundary.Principal);
        Assert.Equal(1, sessionValidator.CallCount);
        Assert.False(authenticationService.SignedOut);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var atBoundary = CreateValidationContext(httpContext, scheme, principal, properties);
        await validator.ValidatePrincipal(atBoundary);
        Assert.Null(atBoundary.Principal);
        Assert.Equal(1, sessionValidator.CallCount);
        Assert.True(authenticationService.SignedOut);
    }

    private static ServiceCollection CreateValidatorServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIdentitySessionValidator, StubIdentitySessionValidator>();
        services.AddSingleton<IOptions<IdentityOptions>>(Options.Create(new IdentityOptions()));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IProblemDetailsService, StubProblemDetailsService>();
        return services;
    }

    private static void AssertTemporaryCookie(
        CookieAuthenticationOptions options,
        string expectedName)
    {
        Assert.Equal(expectedName, options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Strict, options.Cookie.SameSite);
        Assert.Equal("/", options.Cookie.Path);
        Assert.Null(options.Cookie.Domain);
        Assert.Equal(TimeSpan.FromMinutes(5), options.ExpireTimeSpan);
        Assert.False(options.SlidingExpiration);
    }

    private static CareComplyCookiePrincipalValidator CreateValidator(
        TimeProvider timeProvider,
        IIdentitySessionValidator sessionValidator) => new(
            sessionValidator,
            Options.Create(new IdentityOptions()),
            timeProvider,
            new StubProblemDetailsService());

    private static ClaimsPrincipal CreatePrincipal(IdentityOptions identityOptions)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(identityOptions.ClaimsIdentity.UserIdClaimType, Guid.CreateVersion7().ToString()),
                new Claim(identityOptions.ClaimsIdentity.SecurityStampClaimType, "security-stamp"),
            ],
            IdentityConstants.ApplicationScheme);
        return new ClaimsPrincipal(identity);
    }

    private static AuthenticationScheme CreateScheme() => new(
        IdentityConstants.ApplicationScheme,
        IdentityConstants.ApplicationScheme,
        typeof(CookieAuthenticationHandler));

    private static DefaultHttpContext CreateHttpContext(
        IAuthenticationService authenticationService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(authenticationService);
        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
    }

    private static CookieValidatePrincipalContext CreateValidationContext(
        HttpContext httpContext,
        AuthenticationScheme scheme,
        ClaimsPrincipal principal,
        AuthenticationProperties properties) => new(
            httpContext,
            scheme,
            new CookieAuthenticationOptions(),
            new AuthenticationTicket(principal, properties, scheme.Name));

    private sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed class StubIdentitySessionValidator : IIdentitySessionValidator
    {
        public int CallCount { get; private set; }

        public Task<bool> IsValidAsync(
            Guid userId,
            string securityStamp,
            bool hasMfaAssurance,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class StubProblemDetailsService : IProblemDetailsService
    {
        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context) =>
            ValueTask.FromResult(true);

        public ValueTask WriteAsync(ProblemDetailsContext context) => ValueTask.CompletedTask;
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public bool SignedOut { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            SignedOut = true;
            return Task.CompletedTask;
        }
    }
}
