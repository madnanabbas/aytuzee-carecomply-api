using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityAuthenticationService(
    MfaFlowSession mfaFlowSession,
    IMfaEmailSender mfaEmailSender,
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

        if (isMfaRequired
            && user.MfaMethod == UserMfaMethod.Authenticator
            && !user.TwoFactorEnabled)
        {
            var passwordResult = await signInManager.CheckPasswordSignInAsync(
                user,
                password,
                lockoutOnFailure: true);
            if (!passwordResult.Succeeded)
            {
                return IdentityAuthenticationOutcome.Failed;
            }

            await mfaFlowSession.StoreAsync(
                user,
                MfaFlowKind.Enrollment,
                UserMfaMethod.Authenticator);
            return IdentityAuthenticationOutcome.RequiresMfaEnrollment;
        }

        if (isMfaRequired
            && user.MfaMethod == UserMfaMethod.Email
            && !user.TwoFactorEnabled)
        {
            var passwordResult = await signInManager.CheckPasswordSignInAsync(
                user,
                password,
                lockoutOnFailure: true);
            if (!passwordResult.Succeeded)
            {
                return IdentityAuthenticationOutcome.Failed;
            }

            if (!user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
            {
                return IdentityAuthenticationOutcome.Unavailable;
            }

            var enableResult = await userManager.SetTwoFactorEnabledAsync(user, true);
            if (!enableResult.Succeeded)
            {
                return IdentityAuthenticationOutcome.Unavailable;
            }
        }

        var signInResult = await signInManager.PasswordSignInAsync(
            user,
            password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (signInResult.RequiresTwoFactor)
        {
            if (user.MfaMethod == UserMfaMethod.Email)
            {
                var delivered = await SendEmailCodeAsync(user, cancellationToken);
                if (!delivered)
                {
                    await mfaFlowSession.ClearAsync();
                    return IdentityAuthenticationOutcome.Unavailable;
                }
            }

            await mfaFlowSession.StoreAsync(
                user,
                MfaFlowKind.Verification,
                user.MfaMethod);
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

    private async Task<bool> SendEmailCodeAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (!user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
        {
            return false;
        }

        var code = await userManager.GenerateTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultEmailProvider);
        cancellationToken.ThrowIfCancellationRequested();
        return await mfaEmailSender.SendCodeAsync(user.Email, code, cancellationToken);
    }
}
