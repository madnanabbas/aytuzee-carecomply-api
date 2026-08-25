using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Authorization;
using CareComply.Application.Features.Navigation.Queries.GetNavigation;

namespace CareComply.Application.Tests;

public sealed class NavigationQueryTests
{
    [Fact]
    public async Task UnauthenticatedActorFailsClosedWithoutLoadingMetadata()
    {
        var directory = new CapturingNavigationDirectory([]);
        var handler = new GetNavigationQueryHandler(
            new TestActor(false, null),
            new TestPermissionResolver(new HashSet<string>(StringComparer.Ordinal)),
            directory);

        var result = await handler.Handle(new(), CancellationToken.None);

        Assert.True(result.IsUnauthenticated);
        Assert.Null(result.Modules);
        Assert.False(directory.WasCalled);
    }

    [Fact]
    public async Task FiltersUnknownUnsafeAndUnauthorizedFeaturesAndPreservesNestedOrdering()
    {
        var modules = new[]
        {
            new NavigationModuleMetadata("later", "Later", 20,
            [
                Feature("unsafe", "javascript:alert(1)", 10, null, Permissions.Dashboard.View),
                Feature("unknown", "/unknown", 20, null, "Unknown.View"),
            ]),
            new NavigationModuleMetadata("administration", "Administration", 10,
            [
                Feature("access-management", "/access-control", 10, null, Permissions.Role.View),
                Feature("roles", "/access-control/roles", 20, "access-management", Permissions.Role.View),
                Feature("users", "/access-control/users", 10, "access-management", Permissions.User.View),
            ]),
        };
        using var source = new CancellationTokenSource();
        var directory = new CapturingNavigationDirectory(modules);
        var handler = new GetNavigationQueryHandler(
            new TestActor(true, Guid.CreateVersion7()),
            new TestPermissionResolver(new HashSet<string>([Permissions.Role.View], StringComparer.Ordinal)),
            directory);

        var result = await handler.Handle(new(), source.Token);

        var module = Assert.Single(result.Modules!);
        Assert.Equal("administration", module.Key);
        var parent = Assert.Single(module.Features);
        Assert.Equal("access-management", parent.Key);
        Assert.Equal("roles", Assert.Single(parent.Children).Key);
        Assert.Equal(source.Token, directory.CancellationToken);
    }

    [Fact]
    public async Task SupportsAnyOfFeaturePermissionsAndDeterministicOrdering()
    {
        var directory = new CapturingNavigationDirectory([
            new NavigationModuleMetadata("compliance", "Compliance", 10,
            [
                Feature(
                    "documents",
                    "/documents",
                    20,
                    null,
                    Permissions.ClientDocument.View,
                    Permissions.EmployeeDocument.View),
                Feature("dhs-library", "/dhs-library", 10, null, Permissions.DHSLibrary.View),
            ]),
        ]);
        var handler = new GetNavigationQueryHandler(
            new TestActor(true, Guid.CreateVersion7()),
            new TestPermissionResolver(new HashSet<string>(
                [Permissions.EmployeeDocument.View, Permissions.DHSLibrary.View],
                StringComparer.Ordinal)),
            directory);

        var result = await handler.Handle(new(), CancellationToken.None);

        Assert.Equal(
            ["dhs-library", "documents"],
            Assert.Single(result.Modules!).Features.Select(feature => feature.Key));
    }

    private static NavigationFeatureMetadata Feature(
        string key,
        string route,
        int sortOrder,
        string? parentKey,
        params string[] permissions) =>
        new(key, key, route, key, sortOrder, parentKey, permissions);

    private sealed record TestActor(bool IsAuthenticated, Guid? UserId) : ICurrentActor;

    private sealed class TestPermissionResolver(IReadOnlySet<string> permissions)
        : IUserPermissionResolver
    {
        public Task<IReadOnlySet<string>> ResolveAsync(
            Guid userId,
            CancellationToken cancellationToken) => Task.FromResult(permissions);
    }

    private sealed class CapturingNavigationDirectory(
        IReadOnlyList<NavigationModuleMetadata> modules) : INavigationDirectory
    {
        public bool WasCalled { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<IReadOnlyList<NavigationModuleMetadata>> ListActiveAsync(
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            CancellationToken = cancellationToken;
            return Task.FromResult(modules);
        }
    }
}
