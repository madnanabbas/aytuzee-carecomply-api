using CareComply.Domain.Navigation;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Navigation;

internal sealed class NavigationMutationWriter(CareComplyDbContext context)
{
    public async Task<NavigationWriteResult> CreateModuleAsync(
        string key,
        string name,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var id = await AllocateModuleIdAsync(cancellationToken);
        var affectedRows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [dbo].[NavigationModules] ([Id], [Key], [Name], [SortOrder], [IsActive])
            VALUES ({id}, {key}, {name}, {sortOrder}, {isActive})
            """,
            cancellationToken);
        return new(id, affectedRows);
    }

    public async Task<int> UpdateModuleAsync(
        int moduleId,
        string key,
        string name,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken) =>
        await context.Set<NavigationModule>()
            .Where(module => module.Id == moduleId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(module => module.Key, key)
                    .SetProperty(module => module.Name, name)
                    .SetProperty(module => module.SortOrder, sortOrder)
                    .SetProperty(module => module.IsActive, isActive),
                cancellationToken);

    public async Task<int> DeleteModuleAsync(
        int moduleId,
        CancellationToken cancellationToken) =>
        await context.Set<NavigationModule>()
            .Where(module => module.Id == moduleId)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<NavigationWriteResult> CreateFeatureAsync(
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
        var id = await AllocateFeatureIdAsync(cancellationToken);
        var affectedRows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [dbo].[NavigationFeatures]
                ([Id], [ModuleId], [ParentFeatureId], [Key], [Name], [Route], [IconKey], [SortOrder], [IsActive])
            VALUES
                ({id}, {moduleId}, {parentFeatureId}, {key}, {name}, {route}, {iconKey}, {sortOrder}, {isActive})
            """,
            cancellationToken);
        return new(id, affectedRows);
    }

    public async Task<int> UpdateFeatureAsync(
        int featureId,
        int moduleId,
        int? parentFeatureId,
        string key,
        string name,
        string route,
        string iconKey,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken) =>
        await context.Set<NavigationFeature>()
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

    public async Task<int> DeleteFeatureAsync(
        int featureId,
        CancellationToken cancellationToken) =>
        await context.Set<NavigationFeature>()
            .Where(feature => feature.Id == featureId)
            .ExecuteDeleteAsync(cancellationToken);

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
}

internal sealed record NavigationWriteResult(int Id, int AffectedRows);
