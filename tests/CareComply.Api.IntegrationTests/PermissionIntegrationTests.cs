using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using CareComply.Api.Authorization;
using CareComply.Api.Contracts.Authentication;
using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Authorization;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CareComply.Api.IntegrationTests;

public sealed class PermissionIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string ValidPassword = "correct horse battery staple";
    private const string InitialIdentityMigration = "20260815195544_InitialIdentity";

    [Fact]
    public async Task MigrationUpgradesInitialIdentityAndSeedsExactCatalog()
    {
        var connectionString = await factory.CreateIsolatedDatabaseAsync();
        var options = new DbContextOptionsBuilder<CareComplyDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var context = new CareComplyDbContext(options);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(InitialIdentityMigration);
        Assert.False(await TableExistsAsync(context, "Permissions"));
        Assert.False(await TableExistsAsync(context, "RolePermissions"));

        await migrator.MigrateAsync();

        Assert.True(await TableExistsAsync(context, "Permissions"));
        Assert.True(await TableExistsAsync(context, "RolePermissions"));
        var seeded = await context.Set<Permission>()
            .AsNoTracking()
            .OrderBy(permission => permission.Id)
            .Select(permission => new { permission.Id, permission.Name })
            .ToArrayAsync();
        Assert.Equal(Enumerable.Range(1, 48), seeded.Select(permission => permission.Id));
        Assert.Equal(Permissions.All, seeded.Select(permission => permission.Name));
    }

    [Fact]
    public async Task DatabaseEnforcesPermissionAndRolePermissionConstraints()
    {
        await using (var duplicateNameScope = factory.Services.CreateAsyncScope())
        {
            var context = duplicateNameScope.ServiceProvider
                .GetRequiredService<CareComplyDbContext>();
            await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
                "INSERT INTO [dbo].[Permissions] ([Id], [Name]) VALUES (1000, 'Client.View')"));
        }

        var role = await CreateRoleAsync();
        var permissionId = await GetPermissionIdAsync(Permissions.Client.View);
        await AddRolePermissionAsync(role.Id, permissionId);

        await using (var duplicateScope = factory.Services.CreateAsyncScope())
        {
            var context = duplicateScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            context.Add(new RolePermission(role.Id, permissionId));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using (var invalidRoleScope = factory.Services.CreateAsyncScope())
        {
            var context = invalidRoleScope.ServiceProvider
                .GetRequiredService<CareComplyDbContext>();
            context.Add(new RolePermission(Guid.CreateVersion7(), permissionId));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using (var invalidPermissionScope = factory.Services.CreateAsyncScope())
        {
            var context = invalidPermissionScope.ServiceProvider
                .GetRequiredService<CareComplyDbContext>();
            context.Add(new RolePermission(role.Id, 1000));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using (var deleteScope = factory.Services.CreateAsyncScope())
        {
            var context = deleteScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var assignedPermission = await context.Set<Permission>().SingleAsync(
                permission => permission.Id == permissionId);
            context.Remove(assignedPermission);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await DeleteRoleAsync(role.Id);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<CareComplyDbContext>();
        Assert.False(await verificationContext.Set<RolePermission>().AnyAsync(
            rolePermission => rolePermission.RoleId == role.Id));
    }

    [Fact]
    public async Task ResolverReturnsDistinctUnionAndFailsClosedForInactiveUser()
    {
        var user = await CreateUserAsync();
        var firstRole = await CreateRoleAsync();
        var secondRole = await CreateRoleAsync();
        await AssignRoleAsync(user.Id, firstRole.Name!);
        await AssignRoleAsync(user.Id, secondRole.Name!);
        await GrantAsync(firstRole.Id, Permissions.Client.View);
        await GrantAsync(secondRole.Id, Permissions.Client.View);
        await GrantAsync(secondRole.Id, Permissions.Employee.View);

        Assert.Equal(
            [Permissions.Client.View, Permissions.Employee.View],
            (await ResolveInNewScopeAsync(user.Id)).Order(StringComparer.Ordinal));

        await factory.DisableUserAsync(user.Id);

        Assert.Empty(await ResolveInNewScopeAsync(user.Id));
    }

    [Fact]
    public async Task ScopedSnapshotIsStableAndNewScopeObservesRolePermissionChanges()
    {
        var user = await CreateUserAsync();
        var role = await CreateRoleAsync();
        await AssignRoleAsync(user.Id, role.Name!);
        await GrantAsync(role.Id, Permissions.Client.Create);

        await using var currentScope = factory.Services.CreateAsyncScope();
        var currentResolver = currentScope.ServiceProvider
            .GetRequiredService<IUserPermissionResolver>();
        var initial = await currentResolver.ResolveAsync(user.Id, CancellationToken.None);
        Assert.Contains(Permissions.Client.Create, initial);

        await RevokeAsync(role.Id, Permissions.Client.Create);

        var cached = await currentResolver.ResolveAsync(user.Id, CancellationToken.None);
        Assert.Contains(Permissions.Client.Create, cached);
        Assert.Empty(await ResolveInNewScopeAsync(user.Id));

        await GrantAsync(role.Id, Permissions.Client.Create);
        Assert.Contains(
            Permissions.Client.Create,
            await ResolveInNewScopeAsync(user.Id));
    }

    [Fact]
    public async Task RemovingUserRoleTakesEffectOnNextRequest()
    {
        var user = await CreateUserAsync();
        var role = await CreateRoleAsync();
        await AssignRoleAsync(user.Id, role.Name!);
        await GrantAsync(role.Id, Permissions.Client.Create);
        using var client = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await LoginAsync(client, user.Email!)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.GetAsync("/api/test/permission")).StatusCode);

        await RemoveRoleAsync(user.Id, role.Name!);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/test/permission")).StatusCode);
    }

    [Fact]
    public async Task AddingRolePermissionTakesEffectOnNextRequest()
    {
        var user = await CreateUserAsync();
        var role = await CreateRoleAsync();
        await AssignRoleAsync(user.Id, role.Name!);
        using var client = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await LoginAsync(client, user.Email!)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/test/permission")).StatusCode);

        await GrantAsync(role.Id, Permissions.Client.Create);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.GetAsync("/api/test/permission")).StatusCode);
    }

    [Fact]
    public async Task PermissionEndpointReturns401And403ProblemDetails()
    {
        using var anonymousClient = factory.CreateSecureClient();
        var anonymousResponse = await anonymousClient.GetAsync("/api/test/permission");
        await AssertProblemDetailsAsync(
            anonymousResponse,
            HttpStatusCode.Unauthorized,
            "urn:carecomply:problem:authentication-required");

        var user = await CreateUserAsync();
        using var authenticatedClient = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await LoginAsync(authenticatedClient, user.Email!)).StatusCode);
        var forbiddenResponse = await authenticatedClient.GetAsync("/api/test/permission");
        await AssertProblemDetailsAsync(
            forbiddenResponse,
            HttpStatusCode.Forbidden,
            "urn:carecomply:problem:access-denied");
    }

    [Fact]
    public async Task UnknownEndpointPermissionMetadataFailsClosed()
    {
        Assert.Throws<ArgumentException>(() => new HasPermissionAttribute("Unknown.Permission"));
        var provider = new PermissionPolicyProvider(Options.Create(new Microsoft.AspNetCore.Authorization.AuthorizationOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetPolicyAsync(
            PermissionPolicyProvider.PolicyPrefix + "Unknown.Permission"));
    }

    [Fact]
    public async Task EndpointAndCqrsUseSameConstantAndOnePermissionDatabaseLoad()
    {
        var endpoint = typeof(TestPermissionController).GetMethod(
            nameof(TestPermissionController.Get));
        Assert.NotNull(endpoint);
        var attribute = Assert.Single(endpoint.GetCustomAttributes<HasPermissionAttribute>());
        Assert.Equal(Permissions.Client.Create, attribute.Permission);
        Assert.Equal(attribute.Permission, new TestPermissionCommand().RequiredPermission);

        var user = await CreateUserAsync();
        var role = await CreateRoleAsync();
        await AssignRoleAsync(user.Id, role.Name!);
        await GrantAsync(role.Id, Permissions.Client.Create);
        using var client = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await LoginAsync(client, user.Email!)).StatusCode);
        using var commandCounter = new PermissionResolutionCommandCounter();

        var response = await client.GetAsync("/api/test/permission");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, commandCounter.CommandCount);
    }

    [Fact]
    public async Task MeReturnsBackendDerivedPermissionsWithoutRoleMetadata()
    {
        var user = await CreateUserAsync();
        var role = await CreateRoleAsync();
        await AssignRoleAsync(user.Id, role.Name!);
        await GrantAsync(role.Id, Permissions.Employee.View);
        await GrantAsync(role.Id, Permissions.Client.View);
        using var client = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await LoginAsync(client, user.Email!)).StatusCode);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var currentUser = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(currentUser);
        Assert.Equal(
            [Permissions.Client.View, Permissions.Employee.View],
            currentUser.Permissions);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("role", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lockout", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ApplicationUser> CreateUserAsync() => await factory.CreateUserAsync(
        $"permission-user-{Guid.NewGuid():N}@example.test",
        ValidPassword);

    private async Task<ApplicationRole> CreateRoleAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var role = new ApplicationRole { Name = $"TestRole-{Guid.NewGuid():N}" };
        var result = await roleManager.CreateAsync(role);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
        return role;
    }

    private async Task DeleteRoleAsync(Guid roleId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        Assert.NotNull(role);
        Assert.True((await roleManager.DeleteAsync(role)).Succeeded);
    }

    private async Task AssignRoleAsync(Guid userId, string roleName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        Assert.True((await userManager.AddToRoleAsync(user, roleName)).Succeeded);
    }

    private async Task RemoveRoleAsync(Guid userId, string roleName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        Assert.True((await userManager.RemoveFromRoleAsync(user, roleName)).Succeeded);
    }

    private async Task GrantAsync(Guid roleId, string permission)
    {
        var permissionId = await GetPermissionIdAsync(permission);
        await AddRolePermissionAsync(roleId, permissionId);
    }

    private async Task AddRolePermissionAsync(Guid roleId, int permissionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        context.Add(new RolePermission(roleId, permissionId));
        await context.SaveChangesAsync();
    }

    private async Task RevokeAsync(Guid roleId, string permission)
    {
        var permissionId = await GetPermissionIdAsync(permission);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var assignment = await context.Set<RolePermission>().SingleAsync(
            rolePermission => rolePermission.RoleId == roleId
                && rolePermission.PermissionId == permissionId);
        context.Remove(assignment);
        await context.SaveChangesAsync();
    }

    private async Task<int> GetPermissionIdAsync(string permission)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await context.Set<Permission>()
            .Where(candidate => candidate.Name == permission)
            .Select(candidate => candidate.Id)
            .SingleAsync();
    }

    private async Task<IReadOnlySet<string>> ResolveInNewScopeAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IUserPermissionResolver>();
        return await resolver.ResolveAsync(userId, CancellationToken.None);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email) =>
        await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = ValidPassword });

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string type)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(type, document.RootElement.GetProperty("type").GetString());
        Assert.Equal((int)status, document.RootElement.GetProperty("status").GetInt32());
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
        Assert.DoesNotContain(
            Permissions.Client.Create,
            document.RootElement.ToString(),
            StringComparison.Ordinal);
    }

    private static async Task<bool> TableExistsAsync(
        CareComplyDbContext context,
        string tableName)
    {
        var connection = context.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = @name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(await command.ExecuteScalarAsync(),
            System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    private sealed class PermissionResolutionCommandCounter :
        IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = [];
        private readonly IDisposable _allListenersSubscription;
        private int _commandCount;

        public PermissionResolutionCommandCounter() =>
            _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);

        public int CommandCount => Volatile.Read(ref _commandCount);

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == DbLoggerCategory.Name)
            {
                _subscriptions.Add(value.Subscribe(this));
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == RelationalEventId.CommandExecuted.Name
                && value.Value is CommandExecutedEventData eventData
                && eventData.Command.CommandText.Contains(
                    "CareComply.PermissionResolution",
                    StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _commandCount);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            _allListenersSubscription.Dispose();

            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
