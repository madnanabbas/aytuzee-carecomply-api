using CareComply.Application.Abstractions.Navigation;
using CareComply.Domain.Navigation;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Navigation;

internal sealed class EfNavigationDirectory(CareComplyDbContext context) : INavigationDirectory
{
    public async Task<IReadOnlyList<NavigationModuleMetadata>> ListActiveAsync(
        CancellationToken cancellationToken)
    {
        var modules = await context.Set<NavigationModule>()
            .AsNoTracking()
            .Where(module => module.IsActive)
            .OrderBy(module => module.SortOrder)
            .ThenBy(module => module.Key)
            .Select(module => new ModuleRow(module.Id, module.Key, module.Name, module.SortOrder))
            .ToArrayAsync(cancellationToken);

        var moduleIds = modules.Select(module => module.Id).ToArray();
        var features = await context.Set<NavigationFeature>()
            .AsNoTracking()
            .Where(feature => feature.IsActive && moduleIds.Contains(feature.ModuleId))
            .OrderBy(feature => feature.SortOrder)
            .ThenBy(feature => feature.Key)
            .Select(feature => new FeatureRow(
                feature.Id,
                feature.ModuleId,
                feature.ParentFeatureId,
                feature.Key,
                feature.Name,
                feature.Route,
                feature.IconKey,
                feature.SortOrder))
            .ToArrayAsync(cancellationToken);

        var activeFeatureIds = features.Select(feature => feature.Id).ToHashSet();
        var permissionRows = await (
                from mapping in context.Set<NavigationFeaturePermissionLink>().AsNoTracking()
                join permission in context.Set<Permission>().AsNoTracking()
                    on mapping.PermissionId equals permission.Id
                where activeFeatureIds.Contains(mapping.FeatureId)
                orderby mapping.FeatureId, permission.Name
                select new PermissionRow(mapping.FeatureId, permission.Name))
            .ToArrayAsync(cancellationToken);

        var parentKeys = features.ToDictionary(feature => feature.Id, feature => feature.Key);
        var permissionsByFeature = permissionRows
            .GroupBy(row => row.FeatureId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.Permission)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
        var featuresByModule = features
            .Where(feature => feature.ParentFeatureId is null
                || activeFeatureIds.Contains(feature.ParentFeatureId.Value))
            .GroupBy(feature => feature.ModuleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<NavigationFeatureMetadata>)group
                    .Select(feature => new NavigationFeatureMetadata(
                        feature.Key,
                        feature.Name,
                        feature.Route,
                        feature.IconKey,
                        feature.SortOrder,
                        feature.ParentFeatureId is { } parentId
                            && parentKeys.TryGetValue(parentId, out var parentKey)
                                ? parentKey
                                : null,
                        permissionsByFeature.GetValueOrDefault(feature.Id, [])))
                    .ToArray());

        return modules
            .Select(module => new NavigationModuleMetadata(
                module.Key,
                module.Name,
                module.SortOrder,
                featuresByModule.GetValueOrDefault(module.Id, [])))
            .ToArray();
    }

    private sealed record ModuleRow(int Id, string Key, string Name, int SortOrder);

    private sealed record FeatureRow(
        int Id,
        int ModuleId,
        int? ParentFeatureId,
        string Key,
        string Name,
        string Route,
        string IconKey,
        int SortOrder);

    private sealed record PermissionRow(int FeatureId, string Permission);
}
