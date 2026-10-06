namespace CareComply.Api.Contracts.Navigation;

public sealed record NavigationModuleResponse(
    string Key,
    string Name,
    int SortOrder,
    IReadOnlyList<NavigationFeatureResponse> Features);

public sealed record NavigationFeatureResponse(
    string Key,
    string Name,
    string Route,
    string IconKey,
    int SortOrder,
    IReadOnlyList<NavigationFeatureResponse> Children);
