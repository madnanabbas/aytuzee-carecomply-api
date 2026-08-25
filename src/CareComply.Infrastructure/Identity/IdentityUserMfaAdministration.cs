using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityUserMfaAdministration(
    UserManager<ApplicationUser> userManager,
    IUserStore<ApplicationUser> userStore,
    IMfaRequirementEvaluator mfaRequirementEvaluator,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IUserMfaAdministration
{
    public async Task<UserMfaUpdateResult> SetAsync(
        Guid userId,
        bool isEnabled,
        UserMfaMethod method,
        string editorVersion,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return new(UserMfaUpdateStatus.NotFound, null);
        }

        if (!IdentityEditorVersion.Matches(user.ConcurrencyStamp ?? string.Empty, editorVersion))
        {
            return new(UserMfaUpdateStatus.Conflict, null);
        }

        var requiredByPermissions = await mfaRequirementEvaluator
            .IsRequiredByPermissionsAsync(user.Id, cancellationToken);
        if (!isEnabled && requiredByPermissions)
        {
            return new(
                UserMfaUpdateStatus.RequiredByPermissions,
                await userDirectory.GetByIdAsync(user.Id, cancellationToken));
        }

        var methodChanged = user.MfaMethod != method;
        var desiredIdentityMfaState = isEnabled
            && method == UserMfaMethod.Email
            && user.EmailConfirmed;
        var needsIdentityStateChange = method == UserMfaMethod.Email
            ? user.TwoFactorEnabled != desiredIdentityMfaState
            : !isEnabled && user.TwoFactorEnabled;

        if (user.RequiresMfa == isEnabled && !methodChanged && !needsIdentityStateChange)
        {
            return new(
                UserMfaUpdateStatus.NoChanges,
                await userDirectory.GetByIdAsync(user.Id, cancellationToken));
        }

        user.RequiresMfa = isEnabled;
        user.MfaMethod = method;
        user.UpdatedAtUtc = timeProvider.GetUtcNow();

        if (!isEnabled || methodChanged)
        {
            var cleared = await ClearAuthenticatorMaterialAsync(user, cancellationToken);
            if (!cleared)
            {
                return new(UserMfaUpdateStatus.Unavailable, null);
            }
        }

        if (desiredIdentityMfaState && !user.TwoFactorEnabled)
        {
            var enabled = await userManager.SetTwoFactorEnabledAsync(user, true);
            if (!enabled.Succeeded)
            {
                return new(UserMfaUpdateStatus.Unavailable, null);
            }
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            return new(UserMfaUpdateStatus.Conflict, null);
        }

        return new(
            methodChanged
                ? UserMfaUpdateStatus.MethodChanged
                : UserMfaUpdateStatus.Succeeded,
            await userDirectory.GetByIdAsync(user.Id, cancellationToken));
    }

    private async Task<bool> ClearAuthenticatorMaterialAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var disabled = await userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disabled.Succeeded)
        {
            return false;
        }

        var keyReset = await userManager.ResetAuthenticatorKeyAsync(user);
        if (!keyReset.Succeeded
            || userStore is not IUserTwoFactorRecoveryCodeStore<ApplicationUser> recoveryStore)
        {
            return false;
        }

        await recoveryStore.ReplaceCodesAsync(user, [], cancellationToken);
        return true;
    }
}
