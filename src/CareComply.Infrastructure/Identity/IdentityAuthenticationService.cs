using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityAuthenticationService(
    MfaFlowSession mfaFlowSession,
    IMfaRequirementEvaluator mfaRequirementEvaluator,
    LoginPasswordTimingProtector loginPasswordTimingProtector,
    SignInManager<ApplicationUser> signInManager,
    TimeProvider timeProvider,
    UserManager<ApplicationUser> userManager)
    : IIdentityAuthentication
{
    public async Task<IdentityAuthenticationOutcome> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            loginPasswordTimingProtector.Verify(password);
            return IdentityAuthenticationOutcome.Failed;
        }

        if (!user.IsActive)
        {
            _ = userManager.PasswordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash ?? string.Empty,
                password);
            return IdentityAuthenticationOutcome.Failed;
        }

        var isMfaRequired = await mfaRequirementEvaluator.IsRequiredAsync(
            user.Id,
            cancellationToken);
        var hasUsableMfa = await signInManager.IsTwoFactorEnabledAsync(user);

        if (user.TwoFactorEnabled && !hasUsableMfa)
        {
            return IdentityAuthenticationOutcome.Unavailable;
        }

        if (isMfaRequired && !user.TwoFactorEnabled)
        {
            var passwordResult = await signInManager.CheckPasswordSignInAsync(
                user,
                password,
                lockoutOnFailure: true);
            if (!passwordResult.Succeeded)
            {
                return IdentityAuthenticationOutcome.Failed;
            }

            await mfaFlowSession.StoreAsync(user, MfaFlowKind.Enrollment);
            return IdentityAuthenticationOutcome.RequiresMfaEnrollment;
        }

        var signInResult = await signInManager.PasswordSignInAsync(
            user,
            password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (signInResult.RequiresTwoFactor)
        {
            await mfaFlowSession.StoreAsync(user, MfaFlowKind.Verification);
            return IdentityAuthenticationOutcome.RequiresTwoFactor;
        }

        if (!signInResult.Succeeded)
        {
            return IdentityAuthenticationOutcome.Failed;
        }

        var now = timeProvider.GetUtcNow();
        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;
        var updateResult = await userManager.UpdateAsync(user);

        if (updateResult.Succeeded)
        {
            return IdentityAuthenticationOutcome.Succeeded;
        }

        await signInManager.SignOutAsync();
        return IdentityAuthenticationOutcome.Unavailable;
    }
}
