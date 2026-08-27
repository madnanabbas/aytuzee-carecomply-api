namespace CareComply.Application.Abstractions.Navigation;

public interface INavigationAdministration
{
    Task<IReadOnlyList<NavigationAdministrationModule>> ListAsync(
        CancellationToken cancellationToken);

    Task<NavigationAdministrationMutation> CreateModuleAsync(
        string key,
        string name,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken);

    Task<NavigationAdministrationMutation> UpdateModuleAsync(
        int moduleId,
        string key,
        string name,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken);

    Task<NavigationAdministrationMutation> CreateFeatureAsync(
        int moduleId,
        int? parentFeatureId,
        string key,
        string name,
        string route,
        string iconKey,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken);

    Task<NavigationAdministrationMutation> UpdateFeatureAsync(
        int featureId,
        int moduleId,
        int? parentFeatureId,
        string key,
        string name,
        string route,
        string iconKey,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken);
}

public sealed record NavigationAdministrationModule(
    int Id,
    string Key,
    string Name,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<NavigationAdministrationFeature> Features);

public sealed record NavigationAdministrationFeature(
    int Id,
    int ModuleId,
    int? ParentFeatureId,
    string Key,
    string Name,
    string Route,
    string IconKey,
    int SortOrder,
    bool IsActive);

public sealed record NavigationAdministrationMutation(
    NavigationAdministrationMutationStatus Status,
    int? Id = null);

public enum NavigationAdministrationMutationStatus
{
    Succeeded,
    NotFound,
    DuplicateKey,
    DuplicateRoute,
    InvalidParent,
    Unavailable,
}
