using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareComply.Api.Contracts.AccessManagement;
using CareComply.Api.Contracts.Authentication;
using CareComply.Application.Authorization;
using CareComply.Domain.AccessManagement;
using CareComply.Domain.Navigation;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class ClientGuestInviteRoleManagementIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const int ClientFeatureId = 2;
    private const int GuestInvitePermissionId = 97;
    private const string ValidPassword = "correct horse battery staple";

    [Fact]
    public async Task GuestInvitePermissionIsLinkedToExistingClientFeatureWithoutAutomaticRoleGrant()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();

        var permission = await context.Set<Permission>()
            .AsNoTracking()
            .SingleAsync(value => value.Id == GuestInvitePermissionId);
        var clientFeature = await context.Set<NavigationFeature>()
            .AsNoTracking()
            .SingleAsync(value => value.Id == ClientFeatureId);

        Assert.Equal(Permissions.Client.GuestInvite, permission.Name);
        Assert.Equal("clients", clientFeature.Key);
        Assert.True(await context.Set<RolePermissionFeatureLink>().AsNoTracking().AnyAsync(link =>
            link.FeatureId == ClientFeatureId && link.PermissionId == GuestInvitePermissionId));
        Assert.Equal(1, await context.Set<RolePermissionFeatureLink>().AsNoTracking().CountAsync(link =>
            link.PermissionId == GuestInvitePermissionId));
        Assert.False(await context.Set<RolePermission>().AsNoTracking().AnyAsync(value =>
            value.PermissionId == GuestInvitePermissionId));
    }

    [Fact]
    public async Task PermissionCatalogIncludesGuestInviteUnderExistingClientFeature()
    {
        var actor = await factory.CreateUserAsync(
            $"guest-invite-catalog-{Guid.NewGuid():N}@example.test",
            ValidPassword);
        await factory.GrantPermissionAsync(actor.Id, Permissions.Role.View);
        using var client = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = actor.Email, password = ValidPassword })).StatusCode);

        var modules = await client.GetFromJsonAsync<RolePermissionCatalogModuleResponse[]>(
            "/api/access-management/role-management/permission-catalog");

        Assert.NotNull(modules);
        var feature = Assert.Single(modules.SelectMany(module => module.Features), value =>
            value.Id == ClientFeatureId);
        Assert.Equal("clients", feature.Key);
        var permission = Assert.Single(feature.Permissions, value =>
            value.Code == Permissions.Client.GuestInvite);
        Assert.Equal(GuestInvitePermissionId, permission.Id);
        Assert.Equal("Guest invite", permission.Action);
    }

    [Fact]
    public async Task ExistingRoleManagementFlowGrantsAndRemovesGuestInviteAndRevokesSessions()
    {
        var targetUser = await factory.CreateUserAsync(
            $"guest-invite-target-{Guid.NewGuid():N}@example.test",
            ValidPassword);
        var targetRole = await CreateRoleAndAssignUserAsync(targetUser);
        var actor = await factory.CreateUserAsync(
            $"guest-invite-manager-{Guid.NewGuid():N}@example.test",
            ValidPassword);
        await factory.GrantPermissionAsync(actor.Id, Permissions.Role.View);
        await factory.GrantPermissionAsync(actor.Id, Permissions.Role.Manage);

        using var managerClient = factory.CreateSecureClient();
        await LoginAsync(managerClient, actor.Email!);
        await AddAntiforgeryTokenAsync(managerClient);
        using var targetClient = factory.CreateSecureClient();
        await LoginAsync(targetClient, targetUser.Email!);
        var before = await targetClient.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.NotNull(before);
        Assert.DoesNotContain(Permissions.Client.GuestInvite, before.Permissions);

        var role = await managerClient.GetFromJsonAsync<ManagedRoleDetailsResponse>(
            $"/api/access-management/role-management/roles/{targetRole.Id}");
        Assert.NotNull(role);
        var grantResponse = await managerClient.PutAsJsonAsync(
            $"/api/access-management/role-management/roles/{targetRole.Id}/permissions",
            new ReplaceManagedRolePermissionsRequest([GuestInvitePermissionId], role.EditorVersion));
        Assert.True(
            grantResponse.StatusCode == HttpStatusCode.OK,
            $"Expected OK but received {grantResponse.StatusCode}: {await grantResponse.Content.ReadAsStringAsync()}");
        var grantedRole = await grantResponse.Content.ReadFromJsonAsync<ManagedRoleDetailsResponse>();
        Assert.NotNull(grantedRole);
        Assert.Contains(GuestInvitePermissionId, grantedRole.PermissionIds);

        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/auth/me")).StatusCode);
        await LoginAsync(targetClient, targetUser.Email!);
        var grantedUser = await targetClient.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.NotNull(grantedUser);
        Assert.Contains(Permissions.Client.GuestInvite, grantedUser.Permissions);

        var removeResponse = await managerClient.PutAsJsonAsync(
            $"/api/access-management/role-management/roles/{targetRole.Id}/permissions",
            new ReplaceManagedRolePermissionsRequest([], grantedRole.EditorVersion));
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/auth/me")).StatusCode);
        await LoginAsync(targetClient, targetUser.Email!);
        var removedUser = await targetClient.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.NotNull(removedUser);
        Assert.DoesNotContain(Permissions.Client.GuestInvite, removedUser.Permissions);
    }

    private async Task<ApplicationRole> CreateRoleAndAssignUserAsync(ApplicationUser user)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var trackedUser = await userManager.FindByIdAsync(user.Id.ToString())
            ?? throw new InvalidOperationException("The expected test user does not exist.");
        var role = new ApplicationRole
        {
            CompanyId = SqlServerApiFactory.DefaultCompanyId,
            Name = $"GuestInviteTarget-{Guid.NewGuid():N}",
        };
        Assert.True((await roleManager.CreateAsync(role)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(trackedUser, role.Name!)).Succeeded);
        return role;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task AddAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add(
            "X-XSRF-TOKEN",
            document.RootElement.GetProperty("requestToken").GetString());
    }
}
