using System.Security.Claims;
using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityMfaService(
    MfaFlowSession flowSession,
    IMfaEmailSender mfaEmailSender,
    IMfaRequirementEvaluator requirementEvaluator,
    SignInManager<ApplicationUser> signInManager,
    TimeProvider timeProvider,
    UserManager<ApplicationUser> userManager,
    IOptions<MfaPolicyOptions> options)
    : IIdentityMfa
{
    private const int RecoveryCodeCount = 10;

    public async Task<MfaContext> GetContextAsync(CancellationToken cancellationToken)
    {
        var context = await GetValidContextAsync(cancellationToken);
        return context?.Kind switch
        {
            MfaFlowKind.Verification => new(
                MfaContextKind.VerificationRequired,
                context.Method),
            MfaFlowKind.Enrollment => new(
                MfaContextKind.EnrollmentRequired,
                UserMfaMethod.Authenticator),
            _ => new(MfaContextKind.None, UserMfaMethod.Authenticator),
        };
    }

    public async Task<MfaEnrollmentData?> BeginEnrollmentAsync(
        CancellationToken cancellationToken)
    {
        var context = await GetValidContextAsync(cancellationToken);
        if (context is not
            { Kind: MfaFlowKind.Enrollment, Method: UserMfaMethod.Authenticator })
        {
            return null;
        }

        var key = await userManager.GetAuthenticatorKeyAsync(context.User);
        if (string.IsNullOrWhiteSpace(key))
        {
            var resetResult = await userManager.ResetAuthenticatorKeyAsync(context.User);
            if (!resetResult.Succeeded)
            {
                throw new InvalidOperationException("The authenticator key could not be generated.");
            }

            key = await userManager.GetAuthenticatorKeyAsync(context.User);
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("The authenticator key could not be loaded.");
            }

            await flowSession.StoreAsync(
                context.User,
                MfaFlowKind.Enrollment,
                UserMfaMethod.Authenticator);
        }

        var accountName = context.User.Email
            ?? throw new InvalidOperationException("The MFA account does not have an email address.");
        var issuer = options.Value.Issuer;
        var authenticatorUri = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}?secret={Uri.EscapeDataString(key)}&issuer={Uri.EscapeDataString(issuer)}&digits=6");

        return new MfaEnrollmentData(FormatKey(key), authenticatorUri);
    }

    public async Task<MfaEnrollmentConfirmation> ConfirmEnrollmentAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var context = await GetValidContextAsync(cancellationToken);
        if (context is not
            { Kind: MfaFlowKind.Enrollment, Method: UserMfaMethod.Authenticator })
        {
            return MfaEnrollmentConfirmation.Failed(MfaSignInStatus.Expired);
        }

        var normalizedCode = NormalizeAuthenticatorCode(code);
        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            context.User,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            normalizedCode);
        if (!isValid)
        {
            return MfaEnrollmentConfirmation.Failed(MfaSignInStatus.Invalid);
        }

        var recoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(
                context.User,
                RecoveryCodeCount))?
            .ToArray();
        if (recoveryCodes is null || recoveryCodes.Length != RecoveryCodeCount)
        {
            return MfaEnrollmentConfirmation.Failed(MfaSignInStatus.Unavailable);
        }

        var now = timeProvider.GetUtcNow();
        context.User.LastLoginAtUtc = now;
        context.User.UpdatedAtUtc = now;
        var enableResult = await userManager.SetTwoFactorEnabledAsync(context.User, true);
        if (!enableResult.Succeeded)
        {
            return MfaEnrollmentConfirmation.Failed(MfaSignInStatus.Unavailable);
        }

        await signInManager.SignInWithClaimsAsync(
            context.User,
            isPersistent: false,
            [new Claim("amr", "mfa")]);
        await flowSession.ClearAsync();
        return new MfaEnrollmentConfirmation(MfaSignInStatus.Succeeded, recoveryCodes);
    }

    public async Task<MfaSignInStatus> VerifyAuthenticatorAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var context = await GetValidContextAsync(cancellationToken);
        if (context is not { Kind: MfaFlowKind.Verification })
        {
            return MfaSignInStatus.Expired;
        }

        var result = context.Method == UserMfaMethod.Email
            ? await signInManager.TwoFactorSignInAsync(
                TokenOptions.DefaultEmailProvider,
                code.Trim(),
                isPersistent: false,
                rememberClient: false)
            : await signInManager.TwoFactorAuthenticatorSignInAsync(
                NormalizeAuthenticatorCode(code),
                isPersistent: false,
                rememberClient: false);
        return await CompleteSignInAsync(context.User, result, cancellationToken);
    }

    public async Task<MfaSignInStatus> VerifyRecoveryCodeAsync(
        string recoveryCode,
        CancellationToken cancellationToken)
    {
        var context = await GetValidContextAsync(cancellationToken);
        if (context is not
            { Kind: MfaFlowKind.Verification, Method: UserMfaMethod.Authenticator })
        {
            return MfaSignInStatus.Expired;
        }

        var result = await signInManager.TwoFactorRecoveryCodeSignInAsync(
            recoveryCode.Trim());
        return await CompleteSignInAsync(context.User, result, cancellationToken);
    }

    public async Task<MfaEmailSendStatus> ResendEmailCodeAsync(
        CancellationToken cancellationToken)
    {
        var context = await GetValidContextAsync(cancellationToken);
        if (context is not { Kind: MfaFlowKind.Verification, Method: UserMfaMethod.Email })
        {
            return MfaEmailSendStatus.Expired;
        }

        if (!context.User.EmailConfirmed || string.IsNullOrWhiteSpace(context.User.Email))
        {
            await flowSession.ClearAsync();
            return MfaEmailSendStatus.Unavailable;
        }

        var code = await userManager.GenerateTwoFactorTokenAsync(
            context.User,
            TokenOptions.DefaultEmailProvider);
        cancellationToken.ThrowIfCancellationRequested();
        var delivered = await mfaEmailSender.SendCodeAsync(
            context.User.Email,
            code,
            cancellationToken);
        return delivered ? MfaEmailSendStatus.Succeeded : MfaEmailSendStatus.Unavailable;
    }

    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await flowSession.ClearAsync();
    }

    private async Task<MfaSignInStatus> CompleteSignInAsync(
        ApplicationUser user,
        SignInResult result,
        CancellationToken cancellationToken)
    {
        if (!result.Succeeded)
        {
            if (result.IsLockedOut || result.IsNotAllowed)
            {
                await flowSession.ClearAsync();
            }

            return MfaSignInStatus.Invalid;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var now = timeProvider.GetUtcNow();
        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await signInManager.SignOutAsync();
            await flowSession.ClearAsync();
            return MfaSignInStatus.Unavailable;
        }

        await flowSession.ClearAsync();
        return MfaSignInStatus.Succeeded;
    }

    private async Task<ValidMfaContext?> GetValidContextAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var flow = await flowSession.GetAsync();
        if (flow is null)
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(flow.UserId.ToString());
        var isValid = user is { IsActive: true }
            && !string.IsNullOrWhiteSpace(user.SecurityStamp)
            && string.Equals(user.SecurityStamp, flow.SecurityStamp, StringComparison.Ordinal)
            && user.MfaMethod == flow.Method;

        if (isValid && flow.Kind == MfaFlowKind.Verification)
        {
            var identityChallengeUser = await signInManager.GetTwoFactorAuthenticationUserAsync();
            isValid = identityChallengeUser?.Id == user!.Id
                && user.TwoFactorEnabled
                && await signInManager.IsTwoFactorEnabledAsync(user);
        }
        else if (isValid && flow.Kind == MfaFlowKind.Enrollment)
        {
            isValid = !user!.TwoFactorEnabled
                && flow.Method == UserMfaMethod.Authenticator
                && await requirementEvaluator.IsRequiredAsync(user.Id, cancellationToken);
        }

        if (!isValid)
        {
            await flowSession.ClearAsync();
            return null;
        }

        return new ValidMfaContext(user!, flow.Kind, flow.Method);
    }

    private static string NormalizeAuthenticatorCode(string code) =>
        code.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

    private static string FormatKey(string key) => string.Join(
        ' ',
        Enumerable.Range(0, (key.Length + 3) / 4)
            .Select(index => key.Substring(index * 4, Math.Min(4, key.Length - (index * 4)))))
        .ToUpperInvariant();

    private sealed record ValidMfaContext(
        ApplicationUser User,
        MfaFlowKind Kind,
        UserMfaMethod Method);
}
