using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Application.Authorization;
using CareComply.Domain.AccessManagement;
using CareComply.Domain.Navigation;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.AccessManagement;

internal sealed class EfRoleManagement(
    CareComplyDbContext context,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager) : IRoleManagement
{
    public async Task<IReadOnlyList<RoleManagementListItem>> ListAsync(CancellationToken cancellationToken)
    {
        var roles = await context.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new RoleRow(role.Id, role.Name ?? string.Empty, role.ConcurrencyStamp ?? string.Empty))
            .ToArrayAsync(cancellationToken);
        if (roles.Length == 0)
        {
            return [];
        }

        var roleIds = roles.Select(role => role.Id).ToArray();
        var descriptions = await context.Set<RoleProfile>()
            .AsNoTracking()
            .Where(profile => roleIds.Contains(profile.RoleId))
            .ToDictionaryAsync(profile => profile.RoleId, profile => profile.Description, cancellationToken);
        var userCounts = await context.Set<IdentityUserRole<Guid>>()
            .AsNoTracking()
            .Where(userRole => roleIds.Contains(userRole.RoleId))
            .GroupBy(userRole => userRole.RoleId)
            .Select(group => new CountRow(group.Key, group.Count()))
            .ToDictionaryAsync(row => row.Id, row => row.Count, cancellationToken);
        var permissionCounts = await context.Set<RolePermission>()
            .AsNoTracking()
            .Where(rolePermission => roleIds.Contains(rolePermission.RoleId))
            .GroupBy(rolePermission => rolePermission.RoleId)
            .Select(group => new CountRow(group.Key, group.Count()))
            .ToDictionaryAsync(row => row.Id, row => row.Count, cancellationToken);

        return roles.Select(role => new RoleManagementListItem(
            role.Id,
            role.Name,
            descriptions.GetValueOrDefault(role.Id) ?? string.Empty,
            userCounts.GetValueOrDefault(role.Id),
            permissionCounts.GetValueOrDefault(role.Id),
            IdentityEditorVersion.Create(role.ConcurrencyStamp))).ToArray();
    }

    public async Task<RoleManagementDetails?> GetAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .AsNoTracking()
            .Where(value => value.Id == roleId)
            .Select(value => new RoleRow(value.Id, value.Name ?? string.Empty, value.ConcurrencyStamp ?? string.Empty))
            .SingleOrDefaultAsync(cancellationToken);
        return role is null ? null : await BuildDetailsAsync(role, cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionCatalogModule>> GetPermissionCatalogAsync(
        CancellationToken cancellationToken)
    {
        var rows = await (
                from link in context.Set<RolePermissionFeatureLink>().AsNoTracking()
                join feature in context.Set<NavigationFeature>().AsNoTracking() on link.FeatureId equals feature.Id
                join module in context.Set<NavigationModule>().AsNoTracking() on feature.ModuleId equals module.Id
                join permission in context.Set<Permission>().AsNoTracking() on link.PermissionId equals permission.Id
                where module.IsActive && feature.IsActive
                orderby module.SortOrder, module.Key, feature.SortOrder, feature.Key, permission.Name
                select new CatalogRow(
                    module.Id,
                    module.Key,
                    module.Name,
                    module.SortOrder,
                    feature.Id,
                    feature.Key,
                    feature.Name,
                    feature.SortOrder,
                    permission.Id,
                    permission.Name))
            .ToArrayAsync(cancellationToken);

        return rows
            .GroupBy(row => new { row.ModuleId, row.ModuleKey, row.ModuleName, row.ModuleSortOrder })
            .OrderBy(group => group.Key.ModuleSortOrder)
            .ThenBy(group => group.Key.ModuleKey, StringComparer.Ordinal)
            .Select(module => new PermissionCatalogModule(
                module.Key.ModuleId,
                module.Key.ModuleKey,
                module.Key.ModuleName,
                module.Key.ModuleSortOrder,
                module.GroupBy(row => new { row.FeatureId, row.FeatureKey, row.FeatureName, row.FeatureSortOrder })
                    .OrderBy(group => group.Key.FeatureSortOrder)
                    .ThenBy(group => group.Key.FeatureKey, StringComparer.Ordinal)
                    .Select(feature => new PermissionCatalogFeature(
                        feature.Key.FeatureId,
                        feature.Key.FeatureKey,
                        feature.Key.FeatureName,
                        feature.Key.FeatureSortOrder,
                        feature.OrderBy(row => row.PermissionCode, StringComparer.Ordinal)
                            .Select(row => new PermissionCatalogPermissions(
                                row.PermissionId,
                                row.PermissionCode,
                                GetActionLabel(row.PermissionCode)))
                            .ToArray()))
                    .ToArray()))
            .ToArray();
    }

    public async Task<RoleMutationResult> CreateAsync(
        string name,
        string? description,
        IReadOnlyList<int> permissionIds,
        CancellationToken cancellationToken)
    {
        var permissionValidation = await ValidatePermissionsAsync(permissionIds, cancellationToken);
        if (!permissionValidation.IsValid)
        {
            return new(RoleMutationStatus.PermissionNotFound, null);
        }

        var applicationRole = new ApplicationRole { Name = name.Trim() };
        var definition = RoleDefinition.Create(applicationRole.Id, name, description, permissionValidation.PermissionIds);
        var createResult = await roleManager.CreateAsync(applicationRole);
        if (!createResult.Succeeded)
        {
            return new(
                createResult.Errors.Any(error => error.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
                    ? RoleMutationStatus.DuplicateName
                    : RoleMutationStatus.Unavailable,
                null);
        }

        context.Add(new RoleProfile(applicationRole.Id, definition.Description));
        await context.AddRangeAsync(
            definition.PermissionIds.Select(permissionId => new RolePermission(applicationRole.Id, permissionId)),
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new(RoleMutationStatus.Succeeded, await GetAsync(applicationRole.Id, cancellationToken));
    }

    public async Task<RoleMutationResult> UpdateGeneralAsync(
        Guid roleId,
        string name,
        string? description,
        string editorVersion,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return new(RoleMutationStatus.NotFound, null);
        }

        if (!IdentityEditorVersion.Matches(role.ConcurrencyStamp ?? string.Empty, editorVersion))
        {
            return new(RoleMutationStatus.Conflict, null);
        }

        var profile = await context.Set<RoleProfile>().SingleOrDefaultAsync(value => value.RoleId == roleId, cancellationToken);
        var permissionIds = await context.Set<RolePermission>()
            .AsNoTracking()
            .Where(value => value.RoleId == roleId)
            .Select(value => value.PermissionId)
            .ToArrayAsync(cancellationToken);
        var definition = RoleDefinition.Restore(role.Id, role.Name ?? string.Empty, profile?.Description, permissionIds);
        if (!definition.ChangeGeneral(name, description))
        {
            return new(RoleMutationStatus.NoChanges, await GetAsync(roleId, cancellationToken));
        }

        role.Name = definition.Name;
        var identityResult = await roleManager.UpdateAsync(role);
        if (!identityResult.Succeeded)
        {
            return new(
                identityResult.Errors.Any(error => error.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
                    ? RoleMutationStatus.DuplicateName
                    : RoleMutationStatus.Conflict,
                null);
        }

        if (profile is null)
        {
            context.Add(new RoleProfile(roleId, definition.Description));
        }
        else
        {
            profile.ChangeDescription(definition.Description);
        }

        await context.SaveChangesAsync(cancellationToken);
        return new(RoleMutationStatus.Succeeded, await GetAsync(roleId, cancellationToken));
    }

    public async Task<RolePermissionMutationResult> ReplacePermissionsAsync(
        Guid roleId,
        IReadOnlyList<int> permissionIds,
        string editorVersion,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return Result(RoleMutationStatus.NotFound);
        }

        if (!IdentityEditorVersion.Matches(role.ConcurrencyStamp ?? string.Empty, editorVersion))
        {
            return Result(RoleMutationStatus.Conflict);
        }

        var validation = await ValidatePermissionsAsync(permissionIds, cancellationToken);
        if (!validation.IsValid)
        {
            return Result(RoleMutationStatus.PermissionNotFound);
        }

        var existing = await context.Set<RolePermission>()
            .Where(value => value.RoleId == roleId)
            .ToArrayAsync(cancellationToken);
        var definition = RoleDefinition.Restore(
            role.Id,
            role.Name ?? string.Empty,
            null,
            existing.Select(value => value.PermissionId));
        var change = definition.ReplacePermissions(validation.PermissionIds);
        if (!change.HasChanges)
        {
            return new(RoleMutationStatus.NoChanges, await GetAsync(roleId, cancellationToken), [], []);
        }

        if (await WouldRemoveLastRoleManagerAsync(roleId, definition.PermissionIds, cancellationToken))
        {
            return Result(RoleMutationStatus.LastRoleManager);
        }

        var requested = definition.PermissionIds;
        context.RemoveRange(existing.Where(value => !requested.Contains(value.PermissionId)));
        var existingIds = existing.Select(value => value.PermissionId).ToHashSet();
        await context.AddRangeAsync(
            requested.Where(permissionId => !existingIds.Contains(permissionId))
                .Select(permissionId => new RolePermission(roleId, permissionId)),
            cancellationToken);

        var roleUpdate = await roleManager.UpdateAsync(role);
        if (!roleUpdate.Succeeded)
        {
            return Result(RoleMutationStatus.Conflict);
        }

        await context.SaveChangesAsync(cancellationToken);
        if (!await RevokeAssignedSessionsAsync(roleId, cancellationToken))
        {
            return Result(RoleMutationStatus.Unavailable);
        }

        return new(
            RoleMutationStatus.Succeeded,
            await GetAsync(roleId, cancellationToken),
            change.AddedPermissionIds,
            change.RemovedPermissionIds);
    }

    private async Task<RoleManagementDetails> BuildDetailsAsync(RoleRow role, CancellationToken cancellationToken)
    {
        var description = await context.Set<RoleProfile>()
            .AsNoTracking()
            .Where(profile => profile.RoleId == role.Id)
            .Select(profile => profile.Description)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        var permissionIds = await context.Set<RolePermission>()
            .AsNoTracking()
            .Where(value => value.RoleId == role.Id)
            .OrderBy(value => value.PermissionId)
            .Select(value => value.PermissionId)
            .ToArrayAsync(cancellationToken);
        var assignedUserCount = await context.Set<IdentityUserRole<Guid>>()
            .AsNoTracking()
            .CountAsync(userRole => userRole.RoleId == role.Id, cancellationToken);
        return new(
            role.Id,
            role.Name,
            description,
            permissionIds,
            assignedUserCount,
            IdentityEditorVersion.Create(role.ConcurrencyStamp));
    }

    private async Task<PermissionValidation> ValidatePermissionsAsync(
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

    private async Task<bool> WouldRemoveLastRoleManagerAsync(
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
            .AnyAsync(value => value.RoleId == roleId && value.PermissionId == roleManageId.Value, cancellationToken);
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
                join userRole in context.Set<IdentityUserRole<Guid>>().AsNoTracking() on user.Id equals userRole.UserId
                join permission in context.Set<RolePermission>().AsNoTracking() on userRole.RoleId equals permission.RoleId
                where user.IsActive
                    && userRole.RoleId != roleId
                    && permission.PermissionId == roleManageId.Value
                select user.Id)
            .AnyAsync(cancellationToken);
    }

    private async Task<bool> RevokeAssignedSessionsAsync(Guid roleId, CancellationToken cancellationToken)
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

    private static string GetActionLabel(string permissionCode)
    {
        var separator = permissionCode.LastIndexOf('.');
        var action = separator >= 0 ? permissionCode[(separator + 1)..] : permissionCode;
        if (action.Length == 0)
        {
            return permissionCode;
        }

        var result = new System.Text.StringBuilder(action.Length + 4);
        for (var index = 0; index < action.Length; index++)
        {
            var character = action[index];
            if (index > 0 && char.IsUpper(character) && char.IsLower(action[index - 1]))
            {
                result.Append(' ');
            }
            result.Append(index == 0 ? char.ToUpperInvariant(character) : char.ToLowerInvariant(character));
        }
        return result.ToString();
    }

    private static RolePermissionMutationResult Result(RoleMutationStatus status) => new(status, null, [], []);

    private sealed record RoleRow(Guid Id, string Name, string ConcurrencyStamp);
    private sealed record CountRow(Guid Id, int Count);
    private sealed record CatalogRow(
        int ModuleId, string ModuleKey, string ModuleName, int ModuleSortOrder,
        int FeatureId, string FeatureKey, string FeatureName, int FeatureSortOrder,
        int PermissionId, string PermissionCode);
    private sealed record PermissionValidation(bool IsValid, IReadOnlySet<int> PermissionIds);
}
