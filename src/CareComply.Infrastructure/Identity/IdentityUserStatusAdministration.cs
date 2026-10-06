using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;
using CareComply.Infrastructure.Tenancy;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityUserStatusAdministration(
    UserManager<ApplicationUser> userManager,
    TenantUserAccess tenantUserAccess,
    IUserDirectory userDirectory) : IUserStatusAdministration
{
    public async Task<UserStatusUpdateResult> SetActiveAsync(
        Guid userId,
        bool isActive,
        string editorVersion,
        CancellationToken cancellationToken)
    {
        if (!await tenantUserAccess.CanManageAsync(userId, cancellationToken))
        {
            return new(UserAdministrationStatus.NotFound, null);
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return new(UserAdministrationStatus.NotFound, null);
        }

        if (!IdentityEditorVersion.Matches(user.ConcurrencyStamp ?? string.Empty, editorVersion))
        {
            return new(UserAdministrationStatus.Conflict, null);
        }

        if (user.IsActive == isActive)
        {
            return new(
                UserAdministrationStatus.NoChanges,
                await userDirectory.GetByIdAsync(user.Id, cancellationToken));
        }

        user.IsActive = isActive;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var result = isActive
            ? await userManager.UpdateAsync(user)
            : await userManager.UpdateSecurityStampAsync(user);

        if (!result.Succeeded)
        {
            return new(UserAdministrationStatus.Conflict, null);
        }

        return new(
            UserAdministrationStatus.Succeeded,
            await userDirectory.GetByIdAsync(user.Id, cancellationToken));
    }
}
