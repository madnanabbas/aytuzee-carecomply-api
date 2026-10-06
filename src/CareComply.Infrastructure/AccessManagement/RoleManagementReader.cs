using System.Text;
using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.AccessManagement;
using CareComply.Domain.Navigation;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.AccessManagement;

internal sealed class RoleManagementReader(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantAccessor)
{
    public async Task<IReadOnlyList<RoleManagementListItem>> ListAsync(
        CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessor.GetAsync(cancellationToken);
        if (tenant is null)
        {
            return [];
        }

        var roles = await (
                from role in context.Roles.AsNoTracking()
                join company in context.Companies.AsNoTracking() on role.CompanyId equals company.Id
                where tenant.IsPlatformAdministrator || role.CompanyId == tenant.CompanyId
                orderby company.Name, role.Name
                select new RoleRow(
                    role.Id,
                    role.CompanyId,
                    company.Name,
                    role.Name ?? string.Empty,
                    role.ConcurrencyStamp ?? string.Empty))
            .ToArrayAsync(cancellationToken);
        if (roles.Length == 0)
        {
            return [];
        }

        var roleIds = roles.Select(role => role.Id).ToArray();
        var descriptions = await context.Set<RoleProfile>()
            .AsNoTracking()
            .Where(profile => roleIds.Contains(profile.RoleId))
            .ToDictionaryAsync(
                profile => profile.RoleId,
                profile => profile.Description,
                cancellationToken);
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
            role.CompanyId,
            role.CompanyName,
            role.Name,
            descriptions.GetValueOrDefault(role.Id) ?? string.Empty,
            userCounts.GetValueOrDefault(role.Id),
            permissionCounts.GetValueOrDefault(role.Id),
            IdentityEditorVersion.Create(role.ConcurrencyStamp))).ToArray();
    }

    public async Task<RoleManagementDetails?> GetAsync(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessor.GetAsync(cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var role = await (
                from value in context.Roles.AsNoTracking()
                join company in context.Companies.AsNoTracking() on value.CompanyId equals company.Id
                where value.Id == roleId
                    && (tenant.IsPlatformAdministrator || value.CompanyId == tenant.CompanyId)
                select new RoleRow(
                    value.Id,
                    value.CompanyId,
                    company.Name,
                    value.Name ?? string.Empty,
                    value.ConcurrencyStamp ?? string.Empty))
            .SingleOrDefaultAsync(cancellationToken);
        return role is null ? null : await BuildDetailsAsync(role, cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionCatalogModule>> GetPermissionCatalogAsync(
        CancellationToken cancellationToken)
    {
        var modules = await context.Set<NavigationModule>()
            .AsNoTracking()
            .Select(module => new { module.Id, module.Key, module.Name, module.SortOrder })
            .ToArrayAsync(cancellationToken);
        var features = await context.Set<NavigationFeature>()
            .AsNoTracking()
            .Select(feature => new
            {
                feature.Id,
                feature.ModuleId,
                feature.Key,
                feature.Name,
                feature.SortOrder,
            })
            .ToArrayAsync(cancellationToken);
        var permissionLinks = await (
                from link in context.Set<RolePermissionFeatureLink>().AsNoTracking()
                join permission in context.Set<Permission>().AsNoTracking()
                    on link.PermissionId equals permission.Id
                select new
                {
                    link.FeatureId,
                    PermissionId = permission.Id,
                    PermissionCode = permission.Name,
                })
            .ToArrayAsync(cancellationToken);

        return modules
            .OrderBy(module => module.SortOrder)
            .ThenBy(module => module.Key, StringComparer.Ordinal)
            .Select(module => new PermissionCatalogModule(
                module.Id,
                module.Key,
                module.Name,
                module.SortOrder,
                features
                    .Where(feature => feature.ModuleId == module.Id)
                    .OrderBy(feature => feature.SortOrder)
                    .ThenBy(feature => feature.Key, StringComparer.Ordinal)
                    .Select(feature => new PermissionCatalogFeature(
                        feature.Id,
                        feature.Key,
                        feature.Name,
                        feature.SortOrder,
                        permissionLinks
                            .Where(link => link.FeatureId == feature.Id)
                            .OrderBy(link => link.PermissionCode, StringComparer.Ordinal)
                            .Select(link => new PermissionCatalogPermissions(
                                link.PermissionId,
                                link.PermissionCode,
                                GetActionLabel(link.PermissionCode)))
                            .ToArray()))
                    .ToArray()))
            .ToArray();
    }

    private async Task<RoleManagementDetails> BuildDetailsAsync(
        RoleRow role,
        CancellationToken cancellationToken)
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
            role.CompanyId,
            role.CompanyName,
            role.Name,
            description,
            permissionIds,
            assignedUserCount,
            IdentityEditorVersion.Create(role.ConcurrencyStamp));
    }

    private static string GetActionLabel(string permissionCode)
    {
        var separator = permissionCode.LastIndexOf('.');
        var action = separator >= 0 ? permissionCode[(separator + 1)..] : permissionCode;
        if (string.Equals(action, "Create", StringComparison.Ordinal))
        {
            return "Add";
        }

        if (string.Equals(action, "RevealSsn", StringComparison.Ordinal))
        {
            return "Reveal SSN";
        }

        if (action.Length == 0)
        {
            return permissionCode;
        }

        var result = new StringBuilder(action.Length + 4);
        for (var index = 0; index < action.Length; index++)
        {
            var character = action[index];
            if (index > 0 && char.IsUpper(character) && char.IsLower(action[index - 1]))
            {
                result.Append(' ');
            }

            result.Append(index == 0
                ? char.ToUpperInvariant(character)
                : char.ToLowerInvariant(character));
        }

        return result.ToString();
    }

    private sealed record RoleRow(
        Guid Id,
        Guid CompanyId,
        string CompanyName,
        string Name,
        string ConcurrencyStamp);

    private sealed record CountRow(Guid Id, int Count);
}
