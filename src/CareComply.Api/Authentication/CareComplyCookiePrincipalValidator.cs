using System.Globalization;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CareComply.Api.Authentication;

public sealed class CareComplyCookiePrincipalValidator(
    IIdentitySessionValidator identitySessionValidator,
    IOptions<IdentityOptions> identityOptions,
    TimeProvider timeProvider,
    IProblemDetailsService problemDetailsService) : CookieAuthenticationEvents
{
    private const string AuthenticationStartedAtProperty = "carecomply:authentication-started-at";
    private static readonly TimeSpan AbsoluteSessionLifetime = TimeSpan.FromHours(8);

    public override Task SigningIn(CookieSigningInContext context)
    {
        if (!context.Properties.Items.ContainsKey(AuthenticationStartedAtProperty))
        {
            context.Properties.Items[AuthenticationStartedAtProperty] = timeProvider
                .GetUtcNow()
                .ToUnixTimeSeconds()
                .ToString(CultureInfo.InvariantCulture);
        }

        return Task.CompletedTask;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var userIdValue = principal?.FindFirst(
            identityOptions.Value.ClaimsIdentity.UserIdClaimType)?.Value;
        var securityStamp = principal?.FindFirst(
            identityOptions.Value.ClaimsIdentity.SecurityStampClaimType)?.Value;
        var hasMfaAssurance = principal?.HasClaim("amr", "mfa") is true;
        var isValid = Guid.TryParse(userIdValue, out var userId)
            && !string.IsNullOrWhiteSpace(securityStamp)
            && !HasReachedAbsoluteExpiration(context)
            && await identitySessionValidator.IsValidAsync(
                userId,
                securityStamp,
                hasMfaAssurance,
                context.HttpContext.RequestAborted);

        if (!isValid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        WriteProblemDetailsAsync(
            context.HttpContext,
            StatusCodes.Status401Unauthorized,
            "Authentication is required.",
            ProblemTypes.AuthenticationRequired);

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        WriteProblemDetailsAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            "Access is denied.",
            ProblemTypes.AccessDenied);

    private async Task WriteProblemDetailsAsync(
        HttpContext httpContext,
        int statusCode,
        string title,
        string type)
    {
        httpContext.Response.StatusCode = statusCode;
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Type = type,
            },
        });
    }

    private bool HasReachedAbsoluteExpiration(CookieValidatePrincipalContext context)
    {
        if (!context.Properties.Items.TryGetValue(
                AuthenticationStartedAtProperty,
                out var startedAtValue)
            || !long.TryParse(
                startedAtValue,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var startedAtUnixSeconds))
        {
            return true;
        }

        var startedAt = DateTimeOffset.FromUnixTimeSeconds(startedAtUnixSeconds);
        return timeProvider.GetUtcNow() - startedAt >= AbsoluteSessionLifetime;
    }
}
