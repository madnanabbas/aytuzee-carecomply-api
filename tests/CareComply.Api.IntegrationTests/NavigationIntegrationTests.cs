using System.Net;
using System.Net.Http.Json;
using CareComply.Api.Contracts.Navigation;
using CareComply.Application.Authorization;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class NavigationIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string ValidPassword = "correct horse battery staple";

    [Fact]
    public async Task AnonymousRequestIsRejected()
    {
        using var client = factory.CreateSecureClient();

        var response = await client.GetAsync("/api/navigation");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeViewPermissionShowsEmployeesUnderTheClientsModule()
    {
        var user = await factory.CreateUserAsync(
            $"employee-navigation-{Guid.NewGuid():N}@example.test", ValidPassword);
        await factory.GrantPermissionAsync(user.Id, Permissions.Employee.View);
        using var client = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/auth/login",
                new { email = user.Email, password = ValidPassword })).StatusCode);

        var modules = await client.GetFromJsonAsync<NavigationModuleResponse[]>("/api/navigation");

        Assert.NotNull(modules);
        Assert.Contains(modules.SelectMany(module => module.Features), feature =>
            feature.Key == "employees" && feature.Route == "/employees");
    }

    [Fact]
    public async Task ClientNavigationChildrenArePermissionFilteredNestedAndOrdered()
    {
        var allowedUser = await factory.CreateUserAsync(
            $"client-navigation-{Guid.NewGuid():N}@example.test", ValidPassword);
        await factory.GrantPermissionAsync(allowedUser.Id, Permissions.Client.View);
        await factory.GrantPermissionAsync(allowedUser.Id, Permissions.ClientForm.View);
        using var allowedClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent,
            (await allowedClient.PostAsJsonAsync("/api/auth/login",
                new { email = allowedUser.Email, password = ValidPassword })).StatusCode);

        var allowedModules = await allowedClient
            .GetFromJsonAsync<NavigationModuleResponse[]>("/api/navigation");

        Assert.NotNull(allowedModules);
        var people = Assert.Single(allowedModules, module => module.Key == "people");
        var clients = Assert.Single(people.Features, feature => feature.Key == "clients");
        Assert.Equal(["client-list", "client-forms"], clients.Children.Select(child => child.Key));
        var clientList = clients.Children[0];
        Assert.Equal("Client List", clientList.Name);
        Assert.Equal("/clients/list", clientList.Route);
        Assert.Equal(5, clientList.SortOrder);
        var forms = clients.Children[1];
        Assert.Equal("client-forms", forms.Key);
        Assert.Equal("Forms", forms.Name);
        Assert.Equal("/clients/forms", forms.Route);
        Assert.Equal(10, forms.SortOrder);

        var formsOnlyUser = await factory.CreateUserAsync(
            $"client-forms-only-navigation-{Guid.NewGuid():N}@example.test", ValidPassword);
        await factory.GrantPermissionAsync(formsOnlyUser.Id, Permissions.ClientForm.View);
        using var formsOnlyClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent,
            (await formsOnlyClient.PostAsJsonAsync("/api/auth/login",
                new { email = formsOnlyUser.Email, password = ValidPassword })).StatusCode);

        var formsOnlyModules = await formsOnlyClient
            .GetFromJsonAsync<NavigationModuleResponse[]>("/api/navigation");

        Assert.NotNull(formsOnlyModules);
        var formsOnlyPeople = Assert.Single(formsOnlyModules, module => module.Key == "people");
        var formsOnlyClients = Assert.Single(
            formsOnlyPeople.Features, feature => feature.Key == "clients");
        Assert.Equal("client-forms", Assert.Single(formsOnlyClients.Children).Key);
        Assert.DoesNotContain(
            formsOnlyModules.SelectMany(module => module.Features)
                .SelectMany(feature => feature.Children),
            child => child.Key == "client-list");

        var listOnlyUser = await factory.CreateUserAsync(
            $"client-list-only-navigation-{Guid.NewGuid():N}@example.test", ValidPassword);
        await factory.GrantPermissionAsync(listOnlyUser.Id, Permissions.Client.View);
        using var listOnlyClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent,
            (await listOnlyClient.PostAsJsonAsync("/api/auth/login",
                new { email = listOnlyUser.Email, password = ValidPassword })).StatusCode);

        var listOnlyModules = await listOnlyClient
            .GetFromJsonAsync<NavigationModuleResponse[]>("/api/navigation");

        Assert.NotNull(listOnlyModules);
        var listOnlyPeople = Assert.Single(listOnlyModules, module => module.Key == "people");
        var listOnlyClients = Assert.Single(
            listOnlyPeople.Features, feature => feature.Key == "clients");
        Assert.Equal("client-list", Assert.Single(listOnlyClients.Children).Key);
        Assert.DoesNotContain(
            listOnlyModules.SelectMany(module => module.Features)
                .SelectMany(feature => feature.Children),
            child => child.Key == "client-forms");
    }

    [Fact]
    public async Task ReturnsOnlyOrderedDatabaseMetadataAllowedByEffectivePermissions()
    {
        var user = await factory.CreateUserAsync(
            $"navigation-{Guid.NewGuid():N}@example.test",
            ValidPassword);
        await factory.GrantPermissionAsync(user.Id, Permissions.Dashboard.View);
        await factory.GrantPermissionAsync(user.Id, Permissions.EmployeeDocument.View);
        await factory.GrantPermissionAsync(user.Id, Permissions.User.View);
        await factory.GrantPermissionAsync(user.Id, Permissions.Role.View);
        using var client = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync(
                "/api/auth/login",
                new { email = user.Email, password = ValidPassword })).StatusCode);
        var auditCountBefore = await factory.CountAllBusinessAuditLogsAsync();

        var response = await client.GetAsync("/api/navigation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var modules = await response.Content.ReadFromJsonAsync<NavigationModuleResponse[]>();
        Assert.NotNull(modules);
        Assert.Equal(["overview", "compliance", "administration"],
            modules.Select(module => module.Key));
        Assert.Equal("dashboard", Assert.Single(modules[0].Features).Key);
        Assert.Equal("documents", Assert.Single(modules[1].Features).Key);
        var accessManagement = Assert.Single(modules[2].Features);
        Assert.Equal("access-management", accessManagement.Key);
        Assert.Equal(["users", "roles"], accessManagement.Children.Select(child => child.Key));
        Assert.All(modules.SelectMany(module => module.Features), feature =>
            Assert.StartsWith("/", feature.Route, StringComparison.Ordinal));
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("permission", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(auditCountBefore, await factory.CountAllBusinessAuditLogsAsync());

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task InactiveDatabaseModulesAndFeaturesAreHidden()
    {
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            await context.Database.ExecuteSqlRawAsync("""
                INSERT INTO dbo.NavigationModules (Id, [Key], Name, SortOrder, IsActive)
                VALUES (9001, 'inactive-module-test', 'Inactive Module Test', 9001, 0),
                       (9002, 'active-empty-module-test', 'Active Empty Module Test', 9002, 1);
                INSERT INTO dbo.NavigationFeatures
                    (Id, ModuleId, ParentFeatureId, [Key], Name, Route, IconKey, SortOrder, IsActive)
                VALUES (9001, 9001, NULL, 'inactive-module-feature-test', 'Hidden',
                        '/inactive-module-feature-test', 'dashboard', 10, 1),
                       (9002, 9002, NULL, 'inactive-feature-test', 'Hidden',
                        '/inactive-feature-test', 'clients', 10, 0);
                INSERT INTO dbo.NavigationFeaturePermissions (FeatureId, PermissionId)
                VALUES (9001, 1), (9002, 2);
                """);
        }

        try
        {
            var user = await factory.CreateUserAsync(
                $"inactive-navigation-{Guid.NewGuid():N}@example.test",
                ValidPassword);
            await factory.GrantPermissionAsync(user.Id, Permissions.Dashboard.View);
            await factory.GrantPermissionAsync(user.Id, Permissions.Client.View);
            using var client = factory.CreateSecureClient();
            Assert.Equal(
                HttpStatusCode.NoContent,
                (await client.PostAsJsonAsync(
                    "/api/auth/login",
                    new { email = user.Email, password = ValidPassword })).StatusCode);

            var modules = await client.GetFromJsonAsync<NavigationModuleResponse[]>("/api/navigation");

            Assert.NotNull(modules);
            Assert.DoesNotContain(modules, module => module.Key is
                "inactive-module-test" or "active-empty-module-test");
        }
        finally
        {
            await using var cleanupScope = factory.Services.CreateAsyncScope();
            var context = cleanupScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            await context.Database.ExecuteSqlRawAsync("""
                DELETE FROM dbo.NavigationFeaturePermissions WHERE FeatureId IN (9001, 9002);
                DELETE FROM dbo.NavigationFeatures WHERE Id IN (9001, 9002);
                DELETE FROM dbo.NavigationModules WHERE Id IN (9001, 9002);
                """);
        }
    }
}
