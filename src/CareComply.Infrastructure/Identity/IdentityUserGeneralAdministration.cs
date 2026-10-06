using CareComply.Application.Abstractions.Identity;
using CareComply.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityUserGeneralAdministration(
    UserManager<ApplicationUser> userManager,
    TenantUserAccess tenantUserAccess,
    IUserDirectory userDirectory,
    UserCompanyLocationValidator companyLocationValidator,
    UserLocationSynchronizer locationSynchronizer,
    TimeProvider timeProvider) : IUserGeneralAdministration
{
    public async Task<UserGeneralUpdateResult> UpdateAsync(
        UserGeneralUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!await tenantUserAccess.CanManageAsync(request.UserId, cancellationToken))
        {
            return Failed(UserAdministrationStatus.NotFound);
        }

        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return Failed(UserAdministrationStatus.NotFound);
        }

        var assignment = await companyLocationValidator.ValidateAsync(
            user,
            request,
            cancellationToken);
        if (!assignment.IsValid)
        {
            return Failed(assignment.FailureStatus);
        }

        if (!IdentityEditorVersion.Matches(
                user.ConcurrencyStamp ?? string.Empty,
                request.EditorVersion))
        {
            return Failed(UserAdministrationStatus.Conflict);
        }

        var changedFields = UserGeneralProfileUpdater.GetChangedFields(user, request);
        if (changedFields.Count == 0)
        {
            return new(
                UserAdministrationStatus.NoChanges,
                await userDirectory.GetByIdAsync(user.Id, cancellationToken),
                []);
        }

        var passwordChanged = !string.IsNullOrWhiteSpace(request.Password);
        var emailChanged = UserGeneralProfileUpdater.Apply(
            user,
            request,
            assignment.CompanyId,
            timeProvider.GetUtcNow());

        var profileResult = await userManager.UpdateAsync(user);
        if (!profileResult.Succeeded)
        {
            return Failed(IdentityUserFailureClassifier.ClassifyProfileUpdate(profileResult));
        }

        if (!user.IsPlatformAdministrator)
        {
            await locationSynchronizer.ReplaceAsync(
                user.Id,
                assignment.LocationIds,
                cancellationToken);
        }

        if (passwordChanged)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await userManager.ResetPasswordAsync(
                user,
                resetToken,
                request.Password!);

            if (!passwordResult.Succeeded)
            {
                return Failed(IdentityUserFailureClassifier.ClassifyPasswordReset(passwordResult));
            }
        }

        // ResetPasswordAsync already rotates the security stamp.
        // Email changes also revoke existing sessions, even when the password is unchanged.
        if (emailChanged && !passwordChanged)
        {
            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                return Failed(UserAdministrationStatus.Conflict);
            }
        }

        return new(
            UserAdministrationStatus.Succeeded,
            await userDirectory.GetByIdAsync(user.Id, cancellationToken),
            changedFields);
    }

    private static UserGeneralUpdateResult Failed(UserAdministrationStatus status) =>
        new(status, null, []);
}
