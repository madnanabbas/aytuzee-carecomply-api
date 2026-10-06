using CareComply.Application.Abstractions.Navigation;
using CareComply.Domain.Navigation;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Navigation;

internal sealed class NavigationManagementReader(CareComplyDbContext context)
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

    private sealed record ModuleRow(
        int Id,
        string Key,
        string Name,
        int SortOrder,
        bool IsActive);
}
