namespace CareComply.Application.Abstractions.Navigation;

public interface INavigationDirectory
{
    Task<IReadOnlyList<NavigationModuleMetadata>> ListActiveAsync(
        CancellationToken cancellationToken);
}

public sealed record NavigationModuleMetadata(
    string Key,
    string Name,
    int SortOrder,
    IReadOnlyList<NavigationFeatureMetadata> Features);

public sealed record NavigationFeatureMetadata(
    string Key,
    string Name,
    string Route,
    string IconKey,
    int SortOrder,
    string? ParentKey,
    IReadOnlyList<string> RequiredPermissions);
