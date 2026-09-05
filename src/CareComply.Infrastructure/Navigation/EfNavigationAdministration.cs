using CareComply.Application.Abstractions.Navigation;
using CareComply.Domain.Navigation;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Navigation;

internal sealed class EfNavigationAdministration(CareComplyDbContext context)
    : INavigationAdministration
{
    public async Task<IReadOnlyList<NavigationAdministrationModule>> ListAsync(
        CancellationToken cancellationToken)
    {
        var modules = await context.Set<NavigationModule>()
            .AsNoTracking()
            .OrderBy(module => module.SortOrder)
            .ThenBy(module => module.Key)
            .Select(module => new ModuleRow(
                module.Id,
                module.Key,
                module.Name,
                module.SortOrder,
                module.IsActive))
            .ToArrayAsync(cancellationToken);

        if (modules.Length == 0)
        {
            return [];
        }

        var moduleIds = modules.Select(module => module.Id).ToArray();
        var features = await context.Set<NavigationFeature>()
            .AsNoTracking()
            .Where(feature => moduleIds.Contains(feature.ModuleId))
            .OrderBy(feature => feature.ModuleId)
            .ThenBy(feature => feature.SortOrder)
            .ThenBy(feature => feature.Key)
            .Select(feature => new NavigationAdministrationFeature(
                feature.Id,
                feature.ModuleId,
                feature.ParentFeatureId,
                feature.Key,
                feature.Name,
                feature.Route,
                feature.IconKey,
                feature.SortOrder,
                feature.IsActive))
            .ToArrayAsync(cancellationToken);

        var featuresByModule = features
            .GroupBy(feature => feature.ModuleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<NavigationAdministrationFeature>)group.ToArray());

        return modules
            .Select(module => new NavigationAdministrationModule(
                module.Id,
                module.Key,
                module.Name,
                module.SortOrder,
                module.IsActive,
                featuresByModule.GetValueOrDefault(module.Id, [])))
            .ToArray();
    }

    public async Task<NavigationAdministrationMutation> CreateModuleAsync(
        string key,
        string name,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken)
    {
        EnsureAuditedTransaction();

        if (await ModuleKeyExistsAsync(key, excludedModuleId: null, cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.DuplicateKey);
        }

        var id = await AllocateModuleIdAsync(cancellationToken);
        var affectedRows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [dbo].[NavigationModules] ([Id], [Key], [Name], [SortOrder], [IsActive])
            VALUES ({id}, {key}, {name}, {sortOrder}, {isActive})
            """,
            cancellationToken);

        return affectedRows == 1
            ? new(NavigationAdministrationMutationStatus.Succeeded, id)
            : new(NavigationAdministrationMutationStatus.Unavailable);
    }

    public async Task<NavigationAdministrationMutation> UpdateModuleAsync(
        int moduleId,
        string key,
        string name,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken)
    {
        EnsureAuditedTransaction();

        if (!await context.Set<NavigationModule>()
                .AsNoTracking()
                .AnyAsync(module => module.Id == moduleId, cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.NotFound);
        }

        if (await ModuleKeyExistsAsync(key, moduleId, cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.DuplicateKey);
        }

        var affectedRows = await context.Set<NavigationModule>()
            .Where(module => module.Id == moduleId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(module => module.Key, key)
                    .SetProperty(module => module.Name, name)
                    .SetProperty(module => module.SortOrder, sortOrder)
                    .SetProperty(module => module.IsActive, isActive),
                cancellationToken);

        return affectedRows == 1
            ? new(NavigationAdministrationMutationStatus.Succeeded, moduleId)
            : new(NavigationAdministrationMutationStatus.Unavailable);
    }

    public async Task<NavigationAdministrationMutation> DeleteModuleAsync(
        int moduleId,
        CancellationToken cancellationToken)
    {
        EnsureAuditedTransaction();
        if (!await ModuleExistsAsync(moduleId, cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.NotFound);
        }
        if (await context.Set<NavigationFeature>().AsNoTracking()
                .AnyAsync(feature => feature.ModuleId == moduleId, cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.InUse);
        }

        var affectedRows = await context.Set<NavigationModule>()
            .Where(module => module.Id == moduleId)
            .ExecuteDeleteAsync(cancellationToken);
        return affectedRows == 1
            ? new(NavigationAdministrationMutationStatus.Succeeded, moduleId)
            : new(NavigationAdministrationMutationStatus.Unavailable);
    }

    public async Task<NavigationAdministrationMutation> CreateFeatureAsync(
        int moduleId,
        int? parentFeatureId,
        string key,
        string name,
        string route,
        string iconKey,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken)
    {
        EnsureAuditedTransaction();

        if (!await ModuleExistsAsync(moduleId, cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.NotFound);
        }

        if (!await IsValidParentAsync(
                moduleId,
                parentFeatureId,
                featureId: null,
                cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.InvalidParent);
        }

        if (await FeatureKeyExistsAsync(key, excludedFeatureId: null, cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.DuplicateKey);
        }

        if (await FeatureRouteExistsAsync(route, excludedFeatureId: null, cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.DuplicateRoute);
        }

        var id = await AllocateFeatureIdAsync(cancellationToken);
        var affectedRows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [dbo].[NavigationFeatures]
                ([Id], [ModuleId], [ParentFeatureId], [Key], [Name], [Route], [IconKey], [SortOrder], [IsActive])
            VALUES
                ({id}, {moduleId}, {parentFeatureId}, {key}, {name}, {route}, {iconKey}, {sortOrder}, {isActive})
            """,
            cancellationToken);

        return affectedRows == 1
            ? new(NavigationAdministrationMutationStatus.Succeeded, id)
            : new(NavigationAdministrationMutationStatus.Unavailable);
    }

    public async Task<NavigationAdministrationMutation> UpdateFeatureAsync(
        int featureId,
        int moduleId,
        int? parentFeatureId,
        string key,
        string name,
        string route,
        string iconKey,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken)
    {
        EnsureAuditedTransaction();

        var existingFeature = await context.Set<NavigationFeature>()
            .AsNoTracking()
            .Where(feature => feature.Id == featureId)
            .Select(feature => new
            {
                feature.ModuleId,
                feature.ParentFeatureId,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (existingFeature is null)
        {
            return new(NavigationAdministrationMutationStatus.NotFound);
        }

        if (!await ModuleExistsAsync(moduleId, cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.NotFound);
        }

        var hasChildren = await context.Set<NavigationFeature>()
            .AsNoTracking()
            .AnyAsync(feature => feature.ParentFeatureId == featureId, cancellationToken);

        if (hasChildren
            && (parentFeatureId is not null || existingFeature.ModuleId != moduleId))
        {
            return new(NavigationAdministrationMutationStatus.InvalidParent);
        }

        if (!await IsValidParentAsync(
                moduleId,
                parentFeatureId,
                featureId,
                cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.InvalidParent);
        }

        if (await FeatureKeyExistsAsync(key, featureId, cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.DuplicateKey);
        }

        if (await FeatureRouteExistsAsync(route, featureId, cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.DuplicateRoute);
        }

        var affectedRows = await context.Set<NavigationFeature>()
            .Where(feature => feature.Id == featureId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(feature => feature.ModuleId, moduleId)
                    .SetProperty(feature => feature.ParentFeatureId, parentFeatureId)
                    .SetProperty(feature => feature.Key, key)
                    .SetProperty(feature => feature.Name, name)
                    .SetProperty(feature => feature.Route, route)
                    .SetProperty(feature => feature.IconKey, iconKey)
                    .SetProperty(feature => feature.SortOrder, sortOrder)
                    .SetProperty(feature => feature.IsActive, isActive),
                cancellationToken);

        return affectedRows == 1
            ? new(NavigationAdministrationMutationStatus.Succeeded, featureId)
            : new(NavigationAdministrationMutationStatus.Unavailable);
    }

    public async Task<NavigationAdministrationMutation> DeleteFeatureAsync(
        int featureId,
        CancellationToken cancellationToken)
    {
        EnsureAuditedTransaction();
        if (!await context.Set<NavigationFeature>().AsNoTracking()
                .AnyAsync(feature => feature.Id == featureId, cancellationToken))
        {
            return new(NavigationAdministrationMutationStatus.NotFound);
        }

        var hasChildren = await context.Set<NavigationFeature>().AsNoTracking()
            .AnyAsync(feature => feature.ParentFeatureId == featureId, cancellationToken);
        var hasPermissions = await context.Set<CareComply.Domain.AccessManagement.RolePermissionFeatureLink>()
            .AsNoTracking()
            .AnyAsync(link => link.FeatureId == featureId, cancellationToken);
        if (hasChildren || hasPermissions)
        {
            return new(NavigationAdministrationMutationStatus.InUse);
        }

        var affectedRows = await context.Set<NavigationFeature>()
            .Where(feature => feature.Id == featureId)
            .ExecuteDeleteAsync(cancellationToken);
        return affectedRows == 1
            ? new(NavigationAdministrationMutationStatus.Succeeded, featureId)
            : new(NavigationAdministrationMutationStatus.Unavailable);
    }

    private Task<bool> ModuleExistsAsync(
        int moduleId,
        CancellationToken cancellationToken) => context.Set<NavigationModule>()
        .AsNoTracking()
        .AnyAsync(module => module.Id == moduleId, cancellationToken);

    private Task<bool> ModuleKeyExistsAsync(
        string key,
        int? excludedModuleId,
        CancellationToken cancellationToken) => context.Set<NavigationModule>()
        .AsNoTracking()
        .AnyAsync(
            module => module.Key == key
                && (!excludedModuleId.HasValue || module.Id != excludedModuleId.Value),
            cancellationToken);

    private Task<bool> FeatureKeyExistsAsync(
        string key,
        int? excludedFeatureId,
        CancellationToken cancellationToken) => context.Set<NavigationFeature>()
        .AsNoTracking()
        .AnyAsync(
            feature => feature.Key == key
                && (!excludedFeatureId.HasValue || feature.Id != excludedFeatureId.Value),
            cancellationToken);

    private Task<bool> FeatureRouteExistsAsync(
        string route,
        int? excludedFeatureId,
        CancellationToken cancellationToken) => context.Set<NavigationFeature>()
        .AsNoTracking()
        .AnyAsync(
            feature => feature.Route == route
                && (!excludedFeatureId.HasValue || feature.Id != excludedFeatureId.Value),
            cancellationToken);

    private async Task<bool> IsValidParentAsync(
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

    private async Task<int> AllocateModuleIdAsync(CancellationToken cancellationToken)
    {
        var values = await context.Database
            .SqlQuery<int>($"""
                SELECT COALESCE(MAX([Id]), 0) + 1 AS [Value]
                FROM [dbo].[NavigationModules] WITH (UPDLOCK, HOLDLOCK)
                """)
            .ToArrayAsync(cancellationToken);

        return values.Single();
    }

    private async Task<int> AllocateFeatureIdAsync(CancellationToken cancellationToken)
    {
        var values = await context.Database
            .SqlQuery<int>($"""
                SELECT COALESCE(MAX([Id]), 0) + 1 AS [Value]
                FROM [dbo].[NavigationFeatures] WITH (UPDLOCK, HOLDLOCK)
                """)
            .ToArrayAsync(cancellationToken);

        return values.Single();
    }

    private void EnsureAuditedTransaction()
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Navigation mutations must execute within the application audit transaction.");
        }
    }

    private sealed record ModuleRow(
        int Id,
        string Key,
        string Name,
        int SortOrder,
        bool IsActive);
}
