using CareComply.Application.Abstractions.Navigation;
using CareComply.Infrastructure.Persistence;

namespace CareComply.Infrastructure.Navigation;

internal sealed class EfNavigationAdministration(
    CareComplyDbContext context,
    NavigationManagementReader reader,
    NavigationHierarchyValidator validator,
    NavigationMutationWriter writer) : INavigationAdministration
{
    public Task<IReadOnlyList<NavigationAdministrationModule>> ListAsync(
        CancellationToken cancellationToken) =>
        reader.ListAsync(cancellationToken);

    public async Task<NavigationAdministrationMutation> CreateModuleAsync(
        string key,
        string name,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken)
    {
        EnsureAuditedTransaction();

        if (await validator.ModuleKeyExistsAsync(
                key,
                excludedModuleId: null,
                cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.DuplicateKey);
        }

        var result = await writer.CreateModuleAsync(
            key,
            name,
            sortOrder,
            isActive,
            cancellationToken);
        return FromWrite(result);
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

        if (!await validator.ModuleExistsAsync(moduleId, cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.NotFound);
        }

        if (await validator.ModuleKeyExistsAsync(key, moduleId, cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.DuplicateKey);
        }

        var affectedRows = await writer.UpdateModuleAsync(
            moduleId,
            key,
            name,
            sortOrder,
            isActive,
            cancellationToken);
        return FromAffectedRows(affectedRows, moduleId);
    }

    public async Task<NavigationAdministrationMutation> DeleteModuleAsync(
        int moduleId,
        CancellationToken cancellationToken)
    {
        EnsureAuditedTransaction();

        if (!await validator.ModuleExistsAsync(moduleId, cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.NotFound);
        }

        if (await validator.ModuleHasFeaturesAsync(moduleId, cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.InUse);
        }

        var affectedRows = await writer.DeleteModuleAsync(moduleId, cancellationToken);
        return FromAffectedRows(affectedRows, moduleId);
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

        if (!await validator.ModuleExistsAsync(moduleId, cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.NotFound);
        }

        if (!await validator.IsValidParentAsync(
                moduleId,
                parentFeatureId,
                featureId: null,
                cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.InvalidParent);
        }

        if (await validator.FeatureKeyExistsAsync(
                key,
                excludedFeatureId: null,
                cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.DuplicateKey);
        }

        if (await validator.FeatureRouteExistsAsync(
                route,
                excludedFeatureId: null,
                cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.DuplicateRoute);
        }

        var result = await writer.CreateFeatureAsync(
            moduleId,
            parentFeatureId,
            key,
            name,
            route,
            iconKey,
            sortOrder,
            isActive,
            cancellationToken);
        return FromWrite(result);
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

        var existingFeature = await validator.GetFeatureRelationshipAsync(
            featureId,
            cancellationToken);
        if (existingFeature is null)
        {
            return Failed(NavigationAdministrationMutationStatus.NotFound);
        }

        if (!await validator.ModuleExistsAsync(moduleId, cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.NotFound);
        }

        var hasChildren = await validator.FeatureHasChildrenAsync(
            featureId,
            cancellationToken);
        if (hasChildren
            && (parentFeatureId is not null || existingFeature.ModuleId != moduleId))
        {
            return Failed(NavigationAdministrationMutationStatus.InvalidParent);
        }

        if (!await validator.IsValidParentAsync(
                moduleId,
                parentFeatureId,
                featureId,
                cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.InvalidParent);
        }

        if (await validator.FeatureKeyExistsAsync(key, featureId, cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.DuplicateKey);
        }

        if (await validator.FeatureRouteExistsAsync(route, featureId, cancellationToken))
        {
            return Failed(NavigationAdministrationMutationStatus.DuplicateRoute);
        }

        var affectedRows = await writer.UpdateFeatureAsync(
            featureId,
            moduleId,
            parentFeatureId,
            key,
            name,
            route,
            iconKey,
            sortOrder,
            isActive,
            cancellationToken);
        return FromAffectedRows(affectedRows, featureId);
    }

    public async Task<NavigationAdministrationMutation> DeleteFeatureAsync(
        int featureId,
        CancellationToken cancellationToken)
    {
        EnsureAuditedTransaction();

        if (await validator.GetFeatureRelationshipAsync(featureId, cancellationToken) is null)
        {
            return Failed(NavigationAdministrationMutationStatus.NotFound);
        }

        var hasChildren = await validator.FeatureHasChildrenAsync(featureId, cancellationToken);
        var hasPermissions = await validator.FeatureHasPermissionLinksAsync(
            featureId,
            cancellationToken);
        if (hasChildren || hasPermissions)
        {
            return Failed(NavigationAdministrationMutationStatus.InUse);
        }

        var affectedRows = await writer.DeleteFeatureAsync(featureId, cancellationToken);
        return FromAffectedRows(affectedRows, featureId);
    }

    private void EnsureAuditedTransaction()
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Navigation mutations must execute within the application audit transaction.");
        }
    }

    private static NavigationAdministrationMutation FromWrite(NavigationWriteResult result) =>
        FromAffectedRows(result.AffectedRows, result.Id);

    private static NavigationAdministrationMutation FromAffectedRows(
        int affectedRows,
        int id) =>
        affectedRows == 1
            ? new(NavigationAdministrationMutationStatus.Succeeded, id)
            : Failed(NavigationAdministrationMutationStatus.Unavailable);

    private static NavigationAdministrationMutation Failed(
        NavigationAdministrationMutationStatus status) =>
        new(status);
}
