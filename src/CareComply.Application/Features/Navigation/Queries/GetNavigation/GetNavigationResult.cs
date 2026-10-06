namespace CareComply.Application.Features.Navigation.Queries.GetNavigation;

public sealed record GetNavigationResult(
    IReadOnlyList<NavigationModuleResult>? Modules,
    bool IsUnauthenticated)
{
    public static GetNavigationResult Succeeded(IReadOnlyList<NavigationModuleResult> modules) =>
        new(modules, false);

    public static GetNavigationResult Unauthenticated() => new(null, true);
}

public sealed record NavigationModuleResult(
    string Key,
    string Name,
    int SortOrder,
    IReadOnlyList<NavigationFeatureResult> Features);

public sealed record NavigationFeatureResult(
    string Key,
    string Name,
    string Route,
    string IconKey,
    int SortOrder,
    IReadOnlyList<NavigationFeatureResult> Children);
