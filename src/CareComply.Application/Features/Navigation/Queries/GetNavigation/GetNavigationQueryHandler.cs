using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.Navigation.Queries.GetNavigation;

public sealed class GetNavigationQueryHandler(
    ICurrentActor currentActor,
    IUserPermissionResolver permissionResolver,
    INavigationDirectory navigationDirectory)
    : IQueryHandler<GetNavigationQuery, GetNavigationResult>
{
    public async ValueTask<GetNavigationResult> Handle(
        GetNavigationQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentActor.IsAuthenticated || currentActor.UserId is not { } userId)
        {
            return GetNavigationResult.Unauthenticated();
        }

        var permissions = await permissionResolver.ResolveAsync(userId, cancellationToken);
        var modules = await navigationDirectory.ListActiveAsync(cancellationToken);
        var visibleModules = modules
            .OrderBy(module => module.SortOrder)
            .ThenBy(module => module.Key, StringComparer.Ordinal)
            .Select(module => CreateVisibleModule(module, permissions))
            .Where(module => module is not null)
            .Cast<NavigationModuleResult>()
            .ToArray();

        return GetNavigationResult.Succeeded(visibleModules);
    }

    private static NavigationModuleResult? CreateVisibleModule(
        NavigationModuleMetadata module,
        IReadOnlySet<string> permissions)
    {
        if (!IsSafeKey(module.Key))
        {
            return null;
        }

        var roots = module.Features
            .Where(feature => feature.ParentKey is null)
            .OrderBy(feature => feature.SortOrder)
            .ThenBy(feature => feature.Key, StringComparer.Ordinal);
        var visible = new List<NavigationFeatureResult>();

        foreach (var root in roots)
        {
            if (!IsValid(root))
            {
                continue;
            }

            var children = module.Features
                .Where(feature => string.Equals(feature.ParentKey, root.Key, StringComparison.Ordinal))
                .Where(IsValid)
                .Where(feature => IsAllowed(feature, permissions))
                .OrderBy(feature => feature.SortOrder)
                .ThenBy(feature => feature.Key, StringComparer.Ordinal)
                .Select(feature => Map(feature, []))
                .ToArray();

            if (IsAllowed(root, permissions) || children.Length > 0)
            {
                visible.Add(Map(root, children));
            }
        }

        return visible.Count == 0
            ? null
            : new(module.Key, module.Name, module.SortOrder, visible);
    }

    private static bool IsAllowed(
        NavigationFeatureMetadata feature,
        IReadOnlySet<string> permissions) =>
        feature.RequiredPermissions.Any(permissions.Contains);

    private static bool IsValid(NavigationFeatureMetadata feature) =>
        IsSafeKey(feature.Key)
        && IsSafeInternalRoute(feature.Route)
        && feature.RequiredPermissions.Count > 0
        && feature.RequiredPermissions.All(Permissions.IsDefined);

    private static bool IsSafeKey(string value) =>
        value.Length is > 0 and <= 100
        && value.All(character =>
            char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');

    private static bool IsSafeInternalRoute(string route)
    {
        if (route.Length is < 2 or > 200
            || route[0] != '/'
            || route.EndsWith('/')
            || route.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        return route[1..].Split('/').All(IsSafeKey);
    }

    private static NavigationFeatureResult Map(
        NavigationFeatureMetadata feature,
        IReadOnlyList<NavigationFeatureResult> children) =>
        new(
            feature.Key,
            feature.Name,
            feature.Route,
            feature.IconKey,
            feature.SortOrder,
            children);
}
