using CareComply.Application.Abstractions.Identity;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CareComply.Application.Abstractions.Tenancy;

namespace CareComply.Infrastructure.Identity;

internal sealed class EfRoleDirectory(
    CareComplyDbContext context,
    IOptions<MfaPolicyOptions> mfaOptions,
    ITenantScopeAccessor tenantAccessor) : IRoleDirectory
{
    public async Task<IReadOnlyList<RoleDirectoryItem>> ListAsync(
        CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessor.GetAsync(cancellationToken);
        if (tenant is null) return [];
        var roles = await context.Roles
            .AsNoTracking()
            .Where(role => tenant.IsPlatformAdministrator || role.CompanyId == tenant.CompanyId)
            .OrderBy(role => role.Name)
            .Select(role => new RoleProjection(role.Id, role.Name ?? string.Empty))
            .ToArrayAsync(cancellationToken);

        var roleIds = roles.Select(role => role.Id).ToArray();
        var assignments = roleIds.Length == 0
            ? []
            : await (
                    from rolePermission in context.Set<RolePermission>().AsNoTracking()
                    join permission in context.Set<Permission>().AsNoTracking()
                        on rolePermission.PermissionId equals permission.Id
                    where roleIds.Contains(rolePermission.RoleId)
                    orderby permission.Name
                    select new PermissionProjection(rolePermission.RoleId, permission.Name))
                .ToArrayAsync(cancellationToken);

        var permissionsByRole = assignments
            .GroupBy(assignment => assignment.RoleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(assignment => assignment.Permission)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
        var mfaPermissions = mfaOptions.Value.RequiredPermissions.ToHashSet(StringComparer.Ordinal);

        return roles
            .Select(role =>
            {
                var permissions = permissionsByRole.GetValueOrDefault(role.Id) ?? [];
                return new RoleDirectoryItem(
                    role.Id,
                    role.Name,
                    permissions,
                    permissions.Any(mfaPermissions.Contains));
            })
            .ToArray();
    }

    private sealed record RoleProjection(Guid Id, string Name);

    private sealed record PermissionProjection(Guid RoleId, string Permission);
}
