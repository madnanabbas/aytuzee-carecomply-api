using CareComply.Application.Authorization;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Persistence;
using CareComply.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Identity.Staging;

internal sealed class StagingRoleReconciler(
    CareComplyDbContext context,
    RoleManager<ApplicationRole> roleManager)
{
    public async Task<Dictionary<string, ApplicationRole>> ReconcileAsync(
        CancellationToken cancellationToken)
    {
        var permissionIds = await LoadCanonicalPermissionIdsAsync(cancellationToken);
        var roles = new Dictionary<string, ApplicationRole>(StringComparer.Ordinal);

        foreach (var fixture in StagingIdentityFixtures.Roles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var role = await roleManager.FindByNameAsync(fixture.Name);
            if (role is null)
            {
                role = new ApplicationRole
                {
                    Name = fixture.Name,
                    CompanyId = TenantDefaults.DefaultCompanyId,
                };
                StagingIdentityResultGuard.EnsureSucceeded(
                    await roleManager.CreateAsync(role),
                    $"create the staging role '{fixture.Name}'");
            }

            roles.Add(fixture.Name, role);

            var desiredPermissionIds = fixture.Permissions
                .Select(permission => permissionIds[permission])
                .ToHashSet();
            var existingAssignments = await context.Set<RolePermission>()
                .Where(assignment => assignment.RoleId == role.Id)
                .ToListAsync(cancellationToken);

            context.RemoveRange(existingAssignments.Where(
                assignment => !desiredPermissionIds.Contains(assignment.PermissionId)));
            var existingPermissionIds = existingAssignments
                .Select(assignment => assignment.PermissionId)
                .ToHashSet();
            await context.AddRangeAsync(
                desiredPermissionIds
                    .Where(permissionId => !existingPermissionIds.Contains(permissionId))
                    .Select(permissionId => new RolePermission(role.Id, permissionId)),
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        return roles;
    }

    private async Task<Dictionary<string, int>> LoadCanonicalPermissionIdsAsync(
        CancellationToken cancellationToken)
    {
        var persistedPermissions = await context.Set<Permission>()
            .AsNoTracking()
            .ToDictionaryAsync(
                permission => permission.Name,
                permission => permission.Id,
                StringComparer.Ordinal,
                cancellationToken);

        if (persistedPermissions.Count != Permissions.All.Count
            || Permissions.All.Any(permission => !persistedPermissions.ContainsKey(permission)))
        {
            throw new InvalidOperationException(
                "The canonical permission reference data is unavailable or inconsistent.");
        }

        return persistedPermissions;
    }
}
