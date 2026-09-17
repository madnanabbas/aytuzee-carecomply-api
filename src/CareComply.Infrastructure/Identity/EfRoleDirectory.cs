using CareComply.Application.Abstractions.Identity;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CareComply.Application.Abstractions.Tenancy;

namespace CareComply.Infrastructure.Identity;

internal sealed class EfRoleDirectory(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantAccessor) : IRoleDirectory
{
    public async Task<IReadOnlyList<RoleDirectoryItem>> ListAsync(
        CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessor.GetAsync(cancellationToken);
        if (tenant is null) return [];
        var roles = await (from role in context.Roles.AsNoTracking()
            join company in context.Companies.AsNoTracking() on role.CompanyId equals company.Id
            where tenant.IsPlatformAdministrator || role.CompanyId == tenant.CompanyId
            orderby company.Name, role.Name
            select new RoleProjection(role.Id, role.CompanyId, company.Name, role.Name ?? string.Empty))
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
        return roles
            .Select(role =>
            {
                var permissions = permissionsByRole.GetValueOrDefault(role.Id) ?? [];
                return new RoleDirectoryItem(
                    role.Id,
                    role.Name,
                    permissions,
                    false,
                    role.CompanyId,
                    role.CompanyName);
            })
            .ToArray();
    }

    private sealed record RoleProjection(Guid Id, Guid CompanyId, string CompanyName, string Name);

    private sealed record PermissionProjection(Guid RoleId, string Permission);
}
