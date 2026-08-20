using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Identity;

public static class MfaAuthenticationDefaults
{
    public const string FlowScheme = "CareComply.MfaFlow";
}

internal enum MfaFlowKind
{
    Verification,
    Enrollment,
}

internal sealed record MfaFlowState(
    Guid UserId,
    string SecurityStamp,
    MfaFlowKind Kind);

internal sealed class MfaFlowSession(
    IHttpContextAccessor httpContextAccessor,
    IOptions<IdentityOptions> identityOptions)
{
    private const string FlowKindClaim = "carecomply:mfa-flow";

    public async Task StoreAsync(ApplicationUser user, MfaFlowKind kind)
    {
        var httpContext = GetHttpContext();
        var securityStamp = user.SecurityStamp;

        if (string.IsNullOrWhiteSpace(securityStamp))
        {
            throw new InvalidOperationException("The MFA flow cannot be created without a security stamp.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(identityOptions.Value.ClaimsIdentity.SecurityStampClaimType, securityStamp),
                new Claim(FlowKindClaim, kind.ToString()),
            ],
            MfaAuthenticationDefaults.FlowScheme);

        await httpContext.SignInAsync(
            MfaAuthenticationDefaults.FlowScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });
    }

    public async Task<MfaFlowState?> GetAsync()
    {
        var result = await GetHttpContext().AuthenticateAsync(
            MfaAuthenticationDefaults.FlowScheme);
        var principal = result.Principal;
        var userIdValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var securityStamp = principal?.FindFirstValue(
            identityOptions.Value.ClaimsIdentity.SecurityStampClaimType);
        var kindValue = principal?.FindFirstValue(FlowKindClaim);

        return Guid.TryParse(userIdValue, out var userId)
            && !string.IsNullOrWhiteSpace(securityStamp)
            && Enum.TryParse<MfaFlowKind>(kindValue, ignoreCase: false, out var kind)
                ? new MfaFlowState(userId, securityStamp, kind)
                : null;
    }

    public async Task ClearAsync()
    {
        var httpContext = GetHttpContext();
        await httpContext.SignOutAsync(MfaAuthenticationDefaults.FlowScheme);
        await httpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
    }

    private HttpContext GetHttpContext() =>
        httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("An HTTP context is required for an MFA flow.");
}
