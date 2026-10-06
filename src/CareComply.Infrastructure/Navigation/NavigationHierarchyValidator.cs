using CareComply.Domain.AccessManagement;
using CareComply.Domain.Navigation;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Navigation;

internal sealed class NavigationHierarchyValidator(CareComplyDbContext context)
{
    public Task<bool> ModuleExistsAsync(
        int moduleId,
        CancellationToken cancellationToken) =>
        context.Set<NavigationModule>()
            .AsNoTracking()
            .AnyAsync(module => module.Id == moduleId, cancellationToken);

    public Task<bool> ModuleKeyExistsAsync(
        string key,
        int? excludedModuleId,
        CancellationToken cancellationToken) =>
        context.Set<NavigationModule>()
            .AsNoTracking()
            .AnyAsync(
                module => module.Key == key
                    && (!excludedModuleId.HasValue || module.Id != excludedModuleId.Value),
                cancellationToken);

    public Task<bool> FeatureKeyExistsAsync(
        string key,
        int? excludedFeatureId,
        CancellationToken cancellationToken) =>
        context.Set<NavigationFeature>()
            .AsNoTracking()
            .AnyAsync(
                feature => feature.Key == key
                    && (!excludedFeatureId.HasValue || feature.Id != excludedFeatureId.Value),
                cancellationToken);

    public Task<bool> FeatureRouteExistsAsync(
        string route,
        int? excludedFeatureId,
        CancellationToken cancellationToken) =>
        context.Set<NavigationFeature>()
            .AsNoTracking()
            .AnyAsync(
                feature => feature.Route == route
                    && (!excludedFeatureId.HasValue || feature.Id != excludedFeatureId.Value),
                cancellationToken);

    public async Task<bool> IsValidParentAsync(
        int moduleId,
        int? parentFeatureId,
        int? featureId,
        CancellationToken cancellationToken)
    {
        if (parentFeatureId is null)
        {
            return true;
        }

        if (featureId == parentFeatureId)
        {
            return false;
        }

        var parent = await context.Set<NavigationFeature>()
            .AsNoTracking()
            .Where(feature => feature.Id == parentFeatureId.Value)
            .Select(feature => new
            {
                feature.ModuleId,
                feature.ParentFeatureId,
            })
            .SingleOrDefaultAsync(cancellationToken);

        return parent is not null
            && parent.ModuleId == moduleId
            && parent.ParentFeatureId is null;
    }

    public Task<NavigationFeatureRelationship?> GetFeatureRelationshipAsync(
        int featureId,
        CancellationToken cancellationToken) =>
        context.Set<NavigationFeature>()
            .AsNoTracking()
            .Where(feature => feature.Id == featureId)
            .Select(feature => new NavigationFeatureRelationship(
                feature.ModuleId,
                feature.ParentFeatureId))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> FeatureHasChildrenAsync(
        int featureId,
        CancellationToken cancellationToken) =>
        context.Set<NavigationFeature>()
            .AsNoTracking()
            .AnyAsync(feature => feature.ParentFeatureId == featureId, cancellationToken);

    public Task<bool> ModuleHasFeaturesAsync(
        int moduleId,
        CancellationToken cancellationToken) =>
        context.Set<NavigationFeature>()
            .AsNoTracking()
            .AnyAsync(feature => feature.ModuleId == moduleId, cancellationToken);

    public Task<bool> FeatureHasPermissionLinksAsync(
        int featureId,
        CancellationToken cancellationToken) =>
        context.Set<RolePermissionFeatureLink>()
            .AsNoTracking()
            .AnyAsync(link => link.FeatureId == featureId, cancellationToken);
}

internal sealed record NavigationFeatureRelationship(
    int ModuleId,
    int? ParentFeatureId);
