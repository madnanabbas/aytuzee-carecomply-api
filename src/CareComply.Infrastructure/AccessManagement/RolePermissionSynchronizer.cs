using CareComply.Application.Authorization;
using CareComply.Domain.AccessManagement;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.AccessManagement;

internal sealed class RolePermissionSynchronizer(
    CareComplyDbContext context,
    UserManager<ApplicationUser> userManager)
{
    public async Task<RolePermissionValidation> ValidateAsync(
        IReadOnlyList<int> permissionIds,
        CancellationToken cancellationToken)
    {
        var requested = permissionIds.ToHashSet();
        if (requested.Count != permissionIds.Count)
        {
            return new(false, new HashSet<int>());
        }

        var persisted = await context.Set<Permission>()
            .AsNoTracking()
            .Where(permission => requested.Contains(permission.Id))
            .Select(permission => new { permission.Id, permission.Name })
            .ToArrayAsync(cancellationToken);
        var valid = persisted.Length == requested.Count
            && persisted.All(permission => Permissions.IsDefined(permission.Name));
        return new(valid, valid ? requested : []);
    }

    public async Task<RolePermissionChangePlan> BuildChangePlanAsync(
        ApplicationRole role,
        IReadOnlySet<int> requestedPermissionIds,
        CancellationToken cancellationToken)
    {
        var existing = await context.Set<RolePermission>()
            .Where(value => value.RoleId == role.Id)
            .ToArrayAsync(cancellationToken);
        var definition = RoleDefinition.Restore(
            role.Id,
            role.Name ?? string.Empty,
            null,
            existing.Select(value => value.PermissionId));
        var change = definition.ReplacePermissions(requestedPermissionIds);
        return new(
            existing,
            definition.PermissionIds,
            change.AddedPermissionIds,
            change.RemovedPermissionIds,
            change.HasChanges);
    }

    public async Task StageAsync(
        RolePermissionChangePlan plan,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        context.RemoveRange(plan.Existing.Where(
            value => !plan.RequestedPermissionIds.Contains(value.PermissionId)));
        var existingIds = plan.Existing
            .Select(value => value.PermissionId)
            .ToHashSet();
        await context.AddRangeAsync(
            plan.RequestedPermissionIds
                .Where(permissionId => !existingIds.Contains(permissionId))
                .Select(permissionId => new RolePermission(roleId, permissionId)),
            cancellationToken);
    }

    public async Task<bool> WouldRemoveLastRoleManagerAsync(
        Guid roleId,
        IReadOnlySet<int> requestedPermissionIds,
        CancellationToken cancellationToken)
    {
        var roleManageId = await context.Set<Permission>()
            .AsNoTracking()
            .Where(permission => permission.Name == Permissions.Role.Manage)
            .Select(permission => (int?)permission.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (roleManageId is null || requestedPermissionIds.Contains(roleManageId.Value))
        {
            return false;
        }

        var currentHasRoleManage = await context.Set<RolePermission>()
            .AsNoTracking()
            .AnyAsync(
                value => value.RoleId == roleId
                    && value.PermissionId == roleManageId.Value,
                cancellationToken);
        if (!currentHasRoleManage)
        {
            return false;
        }

        var roleHasActiveUsers = await (
                from userRole in context.Set<IdentityUserRole<Guid>>().AsNoTracking()
                join user in context.Users.AsNoTracking() on userRole.UserId equals user.Id
                where userRole.RoleId == roleId && user.IsActive
                select userRole.UserId)
            .AnyAsync(cancellationToken);
        if (!roleHasActiveUsers)
        {
            return false;
        }

        return !await (
                from user in context.Users.AsNoTracking()
                join userRole in context.Set<IdentityUserRole<Guid>>().AsNoTracking()
                    on user.Id equals userRole.UserId
                join permission in context.Set<RolePermission>().AsNoTracking()
                    on userRole.RoleId equals permission.RoleId
                where user.IsActive
                    && userRole.RoleId != roleId
                    && permission.PermissionId == roleManageId.Value
                select user.Id)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> RevokeAssignedSessionsAsync(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var userIds = await context.Set<IdentityUserRole<Guid>>()
            .AsNoTracking()
            .Where(userRole => userRole.RoleId == roleId)
            .Select(userRole => userRole.UserId)
            .ToArrayAsync(cancellationToken);
        foreach (var userId in userIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null || !(await userManager.UpdateSecurityStampAsync(user)).Succeeded)
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed record RolePermissionValidation(
    bool IsValid,
    IReadOnlySet<int> PermissionIds);

internal sealed record RolePermissionChangePlan(
    IReadOnlyList<RolePermission> Existing,
    IReadOnlySet<int> RequestedPermissionIds,
    IReadOnlyList<int> AddedPermissionIds,
    IReadOnlyList<int> RemovedPermissionIds,
    bool HasChanges);
