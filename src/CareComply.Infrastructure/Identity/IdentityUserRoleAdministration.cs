using CareComply.Application.Abstractions.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CareComply.Infrastructure.Tenancy;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityUserRoleAdministration(
    CareComplyDbContext context,
    TenantUserAccess tenantUserAccess,
    IUserDirectory userDirectory,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : IUserRoleAdministration
{
    public async Task<UserRoleUpdateResult> ReplaceAsync(
        Guid userId,
        IReadOnlyList<Guid> roleIds,
        string editorVersion,
        CancellationToken cancellationToken)
    {
        if (!await tenantUserAccess.CanManageAsync(userId, cancellationToken))
        {
            return Result(UserRoleUpdateStatus.NotFound);
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result(UserRoleUpdateStatus.NotFound);
        }

        if (!IdentityEditorVersion.Matches(user.ConcurrencyStamp ?? string.Empty, editorVersion))
        {
            return Result(UserRoleUpdateStatus.Conflict);
        }

        var requestedRoleIds = roleIds.ToHashSet();
        var requestedRoles = await context.Roles
            .Where(role => requestedRoleIds.Contains(role.Id) && role.CompanyId == user.CompanyId)
            .Select(role => new RoleProjection(role.Id, role.Name ?? string.Empty))
            .ToArrayAsync(cancellationToken);
        if (requestedRoles.Length != requestedRoleIds.Count
            || requestedRoles.Any(role => string.IsNullOrWhiteSpace(role.Name)))
        {
            return Result(UserRoleUpdateStatus.RoleNotFound);
        }

        var currentRoles = await (
                from userRole in context.Set<IdentityUserRole<Guid>>()
                join role in context.Roles on userRole.RoleId equals role.Id
                where userRole.UserId == user.Id
                select new RoleProjection(role.Id, role.Name ?? string.Empty))
            .ToArrayAsync(cancellationToken);
        var currentRoleIds = currentRoles.Select(role => role.Id).ToHashSet();
        var addedRoleIds = requestedRoleIds.Except(currentRoleIds).Order().ToArray();
        var removedRoleIds = currentRoleIds.Except(requestedRoleIds).Order().ToArray();

        if (addedRoleIds.Length == 0 && removedRoleIds.Length == 0)
        {
            return new(
                UserRoleUpdateStatus.NoChanges,
                await userDirectory.GetByIdAsync(user.Id, cancellationToken),
                [],
                []);
        }

        var removedRoleNames = currentRoles
            .Where(role => removedRoleIds.Contains(role.Id))
            .Select(role => role.Name)
            .ToArray();
        if (removedRoleNames.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, removedRoleNames);
            if (!removeResult.Succeeded)
            {
                return Result(UserRoleUpdateStatus.Unavailable);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var addedRoleNames = requestedRoles
            .Where(role => addedRoleIds.Contains(role.Id))
            .Select(role => role.Name)
            .ToArray();
        if (addedRoleNames.Length > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, addedRoleNames);
            if (!addResult.Succeeded)
            {
                return Result(UserRoleUpdateStatus.Unavailable);
            }
        }

        user.UpdatedAtUtc = timeProvider.GetUtcNow();
        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            return Result(UserRoleUpdateStatus.Conflict);
        }

        var updated = await userDirectory.GetByIdAsync(user.Id, cancellationToken);
        return updated is null
            ? Result(UserRoleUpdateStatus.Unavailable)
            : new(UserRoleUpdateStatus.Succeeded, updated, addedRoleIds, removedRoleIds);
    }

    private static UserRoleUpdateResult Result(UserRoleUpdateStatus status) =>
        new(status, null, [], []);

    private sealed record RoleProjection(Guid Id, string Name);
}
