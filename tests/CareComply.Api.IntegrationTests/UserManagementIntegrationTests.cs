using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareComply.Api.Contracts.AccessManagement;
using CareComply.Application.Authorization;
using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Auditing;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class UserManagementIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string ValidPassword = "correct horse battery staple";

    [Fact]
    public async Task AddUserEndpointsEnforceAuthenticationPermissionAndAntiforgery()
    {
        using var anonymousClient = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.GetAsync("/api/access-management/roles")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.PostAsJsonAsync("/api/access-management/users", new
            {
                firstName = "New",
                lastName = "User",
                email = "anonymous@example.test",
                phoneNumber = (string?)null,
                isActive = true,
                roleIds = Array.Empty<Guid>(),
            })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.PutAsJsonAsync(
                $"/api/access-management/users/{Guid.CreateVersion7()}/roles",
                new { roleIds = new[] { Guid.CreateVersion7() }, editorVersion = new string('A', 64) }))
            .StatusCode);

        var viewer = await factory.CreateUserAsync(
            $"create-denied-{Guid.NewGuid():N}@example.test",
            ValidPassword);
        await factory.GrantPermissionAsync(viewer.Id, Permissions.User.View);
        using var viewerClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(viewerClient, viewer.Email!)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await viewerClient.GetAsync("/api/access-management/roles")).StatusCode);
        var viewerCsrf = await GetCsrfTokenAsync(viewerClient);
        var forbidden = await SendJsonWithCsrfAsync(
            viewerClient,
            HttpMethod.Post,
            "/api/access-management/users",
            new
            {
                firstName = "New",
                lastName = "User",
                email = "forbidden@example.test",
                phoneNumber = (string?)null,
                isActive = true,
                roleIds = new[] { Guid.CreateVersion7() },
            },
            viewerCsrf);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var forbiddenRoleChange = await SendJsonWithCsrfAsync(
            viewerClient,
            HttpMethod.Put,
            $"/api/access-management/users/{viewer.Id}/roles",
            new { roleIds = new[] { Guid.CreateVersion7() }, editorVersion = new string('A', 64) },
            viewerCsrf);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRoleChange.StatusCode);

        var manager = await CreateManagerAsync();
        using var managerClient = factory.CreateSecureClient();
        _ = await CompleteManagerLoginAsync(managerClient, manager);
        var withoutAntiforgery = await managerClient.PostAsJsonAsync(
            "/api/access-management/users",
            new
            {
                firstName = "New",
                lastName = "User",
                email = "no-csrf@example.test",
                phoneNumber = (string?)null,
                isActive = true,
                roleIds = new[] { Guid.CreateVersion7() },
            });
        Assert.Equal(HttpStatusCode.BadRequest, withoutAntiforgery.StatusCode);
        var roleChangeWithoutAntiforgery = await managerClient.PutAsJsonAsync(
            $"/api/access-management/users/{manager.Id}/roles",
            new { roleIds = new[] { Guid.CreateVersion7() }, editorVersion = new string('A', 64) });
        Assert.Equal(HttpStatusCode.BadRequest, roleChangeWithoutAntiforgery.StatusCode);
    }

    [Fact]
    public async Task AddUserUsesDatabaseRolesCreatesPasswordlessAccountAtomicallyAndAuditsSafely()
    {
        var marker = Guid.NewGuid().ToString("N");
        var manager = await CreateManagerAsync();
        var firstRole = await CreateRoleAsync($"Care role {marker}");
        var secondRole = await CreateRoleAsync($"Records role {marker}");
        await GrantRolePermissionAsync(firstRole.Id, Permissions.Client.View);
        await GrantRolePermissionAsync(secondRole.Id, Permissions.User.Manage);

        using var client = factory.CreateSecureClient();
        var csrf = await CompleteManagerLoginAsync(client, manager);

        var roleResponse = await client.GetAsync("/api/access-management/roles");
        Assert.Equal(HttpStatusCode.OK, roleResponse.StatusCode);
        var roles = await roleResponse.Content.ReadFromJsonAsync<RoleMetadataResponse[]>();
        Assert.NotNull(roles);
        var firstMetadata = Assert.Single(roles, role => role.Id == firstRole.Id);
        Assert.Contains(Permissions.Client.View, firstMetadata.Permissions);
        Assert.False(firstMetadata.RequiresMfa);
        var secondMetadata = Assert.Single(roles, role => role.Id == secondRole.Id);
        Assert.Contains(Permissions.User.Manage, secondMetadata.Permissions);
        Assert.True(secondMetadata.RequiresMfa);

        var email = $"created-{marker}@example.test";
        var createResponse = await SendJsonWithCsrfAsync(
            client,
            HttpMethod.Post,
            "/api/access-management/users",
            new
            {
                firstName = "Created",
                lastName = "Person",
                email,
                phoneNumber = "+1 555 0142",
                isActive = true,
                requiresMfa = true,
                mfaMethod = "Email",
                roleIds = new[] { firstRole.Id, secondRole.Id },
            },
            csrf);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var responseJson = await createResponse.Content.ReadAsStringAsync();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateUserResponse>();
        Assert.NotNull(created);
        Assert.DoesNotContain("password", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stamp", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", responseJson, StringComparison.OrdinalIgnoreCase);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(created.Id.ToString());
            Assert.NotNull(stored);
            Assert.Equal(email.ToUpperInvariant(), stored.NormalizedEmail);
            Assert.Null(stored.PasswordHash);
            Assert.False(stored.EmailConfirmed);
            Assert.True(stored.LockoutEnabled);
            Assert.True(stored.RequiresMfa);
            Assert.Equal(UserMfaMethod.Email, stored.MfaMethod);
            Assert.Equal(
                [firstRole.Name, secondRole.Name],
                (await userManager.GetRolesAsync(stored)).Order(StringComparer.Ordinal));
            Assert.False(await userManager.CheckPasswordAsync(stored, ValidPassword));

            var audit = await context.Set<AuditLog>().AsNoTracking().SingleAsync(log =>
                log.ActorUserId == manager.Id
                && log.Action == "User.Created"
                && log.ResourceId == created.Id.ToString("N"));
            Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
            Assert.DoesNotContain(email, audit.MetadataJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Created", audit.MetadataJson ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("555", audit.MetadataJson ?? string.Empty, StringComparison.Ordinal);
        }

        using var loginClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(loginClient, email)).StatusCode);

        var duplicate = await SendJsonWithCsrfAsync(
            client,
            HttpMethod.Post,
            "/api/access-management/users",
            new
            {
                firstName = "Duplicate",
                lastName = "Person",
                email,
                phoneNumber = (string?)null,
                isActive = true,
                mfaMethod = "Authenticator",
                roleIds = new[] { firstRole.Id },
            },
            csrf);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var invalidEmail = $"invalid-role-{marker}@example.test";
        var invalidRole = await SendJsonWithCsrfAsync(
            client,
            HttpMethod.Post,
            "/api/access-management/users",
            new
            {
                firstName = "Invalid",
                lastName = "Role",
                email = invalidEmail,
                phoneNumber = (string?)null,
                isActive = true,
                mfaMethod = "Authenticator",
                roleIds = new[] { Guid.CreateVersion7() },
            },
            csrf);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);

        var normalizedEmail = email.ToUpperInvariant();
        var normalizedInvalidEmail = invalidEmail.ToUpperInvariant();
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal(1, await verificationContext.Users.CountAsync(user => user.NormalizedEmail == normalizedEmail));
        Assert.False(await verificationContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedInvalidEmail));
        Assert.Empty(await verificationContext.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task UserEditorEnforcesPermissionsConcurrencySelfProtectionRevocationAndSafeAuditing()
    {
        var marker = Guid.NewGuid().ToString("N");
        var actor = await factory.CreateUserAsync($"editor-{marker}@example.test", ValidPassword);
        await factory.GrantPermissionAsync(actor.Id, Permissions.User.View);
        await factory.GrantPermissionAsync(actor.Id, Permissions.User.Manage);
        await factory.EnableAuthenticatorAsync(actor.Id);
        var target = await CreateProfiledUserAsync(
            $"target-{marker}@example.test", "Target", "Person", true);
        using var targetClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(targetClient, target.Email!)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await targetClient.GetAsync("/api/auth/me")).StatusCode);

        using var client = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Accepted, (await LoginAsync(client, actor.Email!)).StatusCode);
        var mfaContext = await client.GetFromJsonAsync<MfaContextBody>("/api/auth/mfa/context");
        Assert.NotNull(mfaContext);
        var code = await factory.GenerateAuthenticatorCodeAsync(actor.Id);
        var mfaResponse = await SendJsonWithCsrfAsync(
            client, HttpMethod.Post, "/api/auth/mfa/verify", new { code }, mfaContext.RequestToken);
        Assert.Equal(HttpStatusCode.NoContent, mfaResponse.StatusCode);
        var csrf = await GetCsrfTokenAsync(client);

        var original = await client.GetFromJsonAsync<UserDetailsResponse>(
            $"/api/access-management/users/{target.Id}");
        Assert.NotNull(original);
        Assert.NotEqual(target.ConcurrencyStamp, original.EditorVersion);

        var update = await SendJsonWithCsrfAsync(
            client,
            HttpMethod.Put,
            $"/api/access-management/users/{target.Id}/general",
            new { firstName = "Updated", lastName = "Person", email = target.Email, phoneNumber = "+1 555 0100", editorVersion = original.EditorVersion },
            csrf);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<UserDetailsResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.FirstName);
        Assert.NotEqual(original.EditorVersion, updated.EditorVersion);

        var stale = await SendJsonWithCsrfAsync(
            client,
            HttpMethod.Put,
            $"/api/access-management/users/{target.Id}/general",
            new { firstName = "Stale", lastName = "Person", email = target.Email, phoneNumber = (string?)null, editorVersion = original.EditorVersion },
            csrf);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var stampBeforeDeactivation = (await factory.GetUserAsync(target.Id)).SecurityStamp;
        var deactivated = await SendJsonWithCsrfAsync(
            client,
            HttpMethod.Post,
            $"/api/access-management/users/{target.Id}/deactivate",
            new { editorVersion = updated.EditorVersion },
            csrf);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        var deactivatedDetails = await deactivated.Content.ReadFromJsonAsync<UserDetailsResponse>();
        Assert.NotNull(deactivatedDetails);
        var storedTarget = await factory.GetUserAsync(target.Id);
        Assert.False(storedTarget.IsActive);
        Assert.NotEqual(stampBeforeDeactivation, storedTarget.SecurityStamp);
        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/auth/me")).StatusCode);

        var activated = await SendJsonWithCsrfAsync(
            client,
            HttpMethod.Post,
            $"/api/access-management/users/{target.Id}/activate",
            new { editorVersion = deactivatedDetails.EditorVersion },
            csrf);
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        Assert.True((await factory.GetUserAsync(target.Id)).IsActive);
        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/auth/me")).StatusCode);

        var actorDetails = await client.GetFromJsonAsync<UserDetailsResponse>(
            $"/api/access-management/users/{actor.Id}");
        Assert.NotNull(actorDetails);
        var selfDeactivate = await SendJsonWithCsrfAsync(
            client,
            HttpMethod.Post,
            $"/api/access-management/users/{actor.Id}/deactivate",
            new { editorVersion = actorDetails.EditorVersion },
            csrf);
        Assert.Equal(HttpStatusCode.Conflict, selfDeactivate.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var logs = await context.Set<AuditLog>().AsNoTracking()
            .Where(log => log.ActorUserId == actor.Id && log.ResourceId == target.Id.ToString("N"))
            .ToArrayAsync();
        Assert.Contains(logs, log => log.Action == "User.GeneralUpdated" && log.Outcome == AuditOutcome.Succeeded);
        Assert.Contains(logs, log => log.Action == "User.Deactivated" && log.Outcome == AuditOutcome.Succeeded);
        Assert.Contains(logs, log => log.Action == "User.Activated" && log.Outcome == AuditOutcome.Succeeded);
        Assert.DoesNotContain(logs, log =>
            log.MetadataJson?.Contains(target.Email!, StringComparison.OrdinalIgnoreCase) is true);
    }

    [Fact]
    public async Task RoleEditorReturnsAssignmentsReplacesByIdRevokesSessionAndRecalculatesMfa()
    {
        var marker = Guid.NewGuid().ToString("N");
        var manager = await CreateManagerAsync();
        await factory.GrantPermissionAsync(manager.Id, Permissions.User.View);
        var originalRole = await CreateRoleAsync($"Original role {marker}");
        var protectedRole = await CreateRoleAsync($"Protected role {marker}");
        await GrantRolePermissionAsync(originalRole.Id, Permissions.Dashboard.View);
        await GrantRolePermissionAsync(protectedRole.Id, Permissions.User.Manage);
        var target = await CreateProfiledUserAsync(
            $"role-target-{marker}@example.test", "Role", "Target", true);
        await AssignRoleAsync(target, originalRole);

        using var targetClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(targetClient, target.Email!)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await targetClient.GetAsync("/api/auth/me")).StatusCode);

        using var managerClient = factory.CreateSecureClient();
        var csrf = await CompleteManagerLoginAsync(managerClient, manager);
        var details = await managerClient.GetFromJsonAsync<UserDetailsResponse>(
            $"/api/access-management/users/{target.Id}");
        Assert.NotNull(details);
        var assignedRole = Assert.Single(details.Roles);
        Assert.Equal(originalRole.Id, assignedRole.Id);
        Assert.Equal(originalRole.Name, assignedRole.Name);

        var invalidRoleId = Guid.CreateVersion7();
        var invalid = await SendJsonWithCsrfAsync(
            managerClient,
            HttpMethod.Put,
            $"/api/access-management/users/{target.Id}/roles",
            new { roleIds = new[] { invalidRoleId }, editorVersion = details.EditorVersion },
            csrf);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await using (var invalidScope = factory.Services.CreateAsyncScope())
        {
            var userManager = invalidScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(target.Id.ToString());
            Assert.NotNull(stored);
            Assert.Equal([originalRole.Name!], await userManager.GetRolesAsync(stored));
        }

        var stampBefore = (await factory.GetUserAsync(target.Id)).SecurityStamp;
        var changed = await SendJsonWithCsrfAsync(
            managerClient,
            HttpMethod.Put,
            $"/api/access-management/users/{target.Id}/roles",
            new { roleIds = new[] { protectedRole.Id }, editorVersion = details.EditorVersion },
            csrf);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var updated = await changed.Content.ReadFromJsonAsync<UserDetailsResponse>();
        Assert.NotNull(updated);
        Assert.Equal(protectedRole.Id, Assert.Single(updated.Roles).Id);
        Assert.True(updated.IsMfaRequiredByPermissions);
        Assert.True(updated.IsMfaRequired);
        Assert.NotEqual(details.EditorVersion, updated.EditorVersion);
        Assert.NotEqual(stampBefore, (await factory.GetUserAsync(target.Id)).SecurityStamp);
        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/auth/me")).StatusCode);

        var stale = await SendJsonWithCsrfAsync(
            managerClient,
            HttpMethod.Put,
            $"/api/access-management/users/{target.Id}/roles",
            new { roleIds = new[] { originalRole.Id }, editorVersion = details.EditorVersion },
            csrf);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var disableMfa = await SendJsonWithCsrfAsync(
            managerClient,
            HttpMethod.Put,
            $"/api/access-management/users/{target.Id}/mfa",
            new { isEnabled = false, method = "Authenticator", editorVersion = updated.EditorVersion },
            csrf);
        Assert.Equal(HttpStatusCode.Conflict, disableMfa.StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var userManagerForVerification = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var storedTarget = await userManagerForVerification.FindByIdAsync(target.Id.ToString());
        Assert.NotNull(storedTarget);
        Assert.Equal([protectedRole.Name!], await userManagerForVerification.GetRolesAsync(storedTarget));

        var audit = await context.Set<AuditLog>().AsNoTracking().SingleAsync(log =>
            log.ActorUserId == manager.Id
            && log.Action == "User.RolesChanged"
            && log.ResourceId == target.Id.ToString("N")
            && log.Outcome == AuditOutcome.Succeeded);
        Assert.Contains(originalRole.Id.ToString("N"), audit.MetadataJson, StringComparison.Ordinal);
        Assert.Contains(protectedRole.Id.ToString("N"), audit.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain(originalRole.Name!, audit.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain(protectedRole.Name!, audit.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain(target.Email!, audit.MetadataJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DynamicMfaSupportsEnrollmentDisableAndPermissionFloor()
    {
        var marker = Guid.NewGuid().ToString("N");
        var actor = await factory.CreateUserAsync($"mfa-admin-{marker}@example.test", ValidPassword);
        await factory.GrantPermissionAsync(actor.Id, Permissions.User.View);
        await factory.GrantPermissionAsync(actor.Id, Permissions.User.Manage);
        await factory.EnableAuthenticatorAsync(actor.Id);
        using var client = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Accepted, (await LoginAsync(client, actor.Email!)).StatusCode);
        var actorContext = await client.GetFromJsonAsync<MfaContextBody>("/api/auth/mfa/context");
        Assert.NotNull(actorContext);
        var actorCode = await factory.GenerateAuthenticatorCodeAsync(actor.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await SendJsonWithCsrfAsync(
            client, HttpMethod.Post, "/api/auth/mfa/verify", new { code = actorCode }, actorContext.RequestToken)).StatusCode);
        var csrf = await GetCsrfTokenAsync(client);

        var target = await factory.CreateUserAsync($"mfa-target-{marker}@example.test", ValidPassword);
        using var existingSession = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(existingSession, target.Email!)).StatusCode);
        var details = await client.GetFromJsonAsync<UserDetailsResponse>($"/api/access-management/users/{target.Id}");
        Assert.NotNull(details);

        var enabledResponse = await SendJsonWithCsrfAsync(
            client, HttpMethod.Put, $"/api/access-management/users/{target.Id}/mfa",
            new { isEnabled = true, method = "Authenticator", editorVersion = details.EditorVersion }, csrf);
        Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);
        var enabled = await enabledResponse.Content.ReadFromJsonAsync<UserDetailsResponse>();
        Assert.NotNull(enabled);
        Assert.True(enabled.RequiresMfa);
        Assert.True(enabled.IsMfaRequired);
        Assert.False(enabled.IsMfaEnabled);
        Assert.Equal(HttpStatusCode.Unauthorized, (await existingSession.GetAsync("/api/auth/me")).StatusCode);

        using var enrollmentClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Accepted, (await LoginAsync(enrollmentClient, target.Email!)).StatusCode);
        var enrollmentContext = await enrollmentClient.GetFromJsonAsync<MfaContextBody>("/api/auth/mfa/context");
        Assert.NotNull(enrollmentContext);
        Assert.Equal("mfaEnrollmentRequired", enrollmentContext.Status);

        await factory.EnableAuthenticatorAsync(target.Id);
        var oldCode = await factory.GenerateAuthenticatorCodeAsync(target.Id);
        var enrolledDetails = await client.GetFromJsonAsync<UserDetailsResponse>($"/api/access-management/users/{target.Id}");
        Assert.NotNull(enrolledDetails);
        Assert.True(enrolledDetails.IsMfaEnabled);

        var disabledResponse = await SendJsonWithCsrfAsync(
            client, HttpMethod.Put, $"/api/access-management/users/{target.Id}/mfa",
            new { isEnabled = false, method = "Authenticator", editorVersion = enrolledDetails.EditorVersion }, csrf);
        Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
        var disabled = await disabledResponse.Content.ReadFromJsonAsync<UserDetailsResponse>();
        Assert.NotNull(disabled);
        Assert.False(disabled.RequiresMfa);
        Assert.False(disabled.IsMfaRequired);
        Assert.False(disabled.IsMfaEnabled);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(target.Id.ToString());
            Assert.NotNull(stored);
            Assert.Equal(0, await userManager.CountRecoveryCodesAsync(stored));
            Assert.False(await userManager.VerifyTwoFactorTokenAsync(
                stored,
                userManager.Options.Tokens.AuthenticatorTokenProvider,
                oldCode));
        }

        using var passwordOnlyClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(passwordOnlyClient, target.Email!)).StatusCode);

        var protectedTarget = await factory.CreateUserAsync($"mfa-protected-{marker}@example.test", ValidPassword);
        await factory.GrantPermissionAsync(protectedTarget.Id, Permissions.User.Manage);
        var protectedDetails = await client.GetFromJsonAsync<UserDetailsResponse>($"/api/access-management/users/{protectedTarget.Id}");
        Assert.NotNull(protectedDetails);
        Assert.True(protectedDetails.IsMfaRequiredByPermissions);
        var rejected = await SendJsonWithCsrfAsync(
            client, HttpMethod.Put, $"/api/access-management/users/{protectedTarget.Id}/mfa",
            new { isEnabled = false, method = "Authenticator", editorVersion = protectedDetails.EditorVersion }, csrf);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    }

    [Fact]
    public async Task ChangingMfaMethodRevokesSessionsAndInvalidatesPreviousMethod()
    {
        var marker = Guid.NewGuid().ToString("N");
        var manager = await CreateManagerAsync();
        await factory.GrantPermissionAsync(manager.Id, Permissions.User.View);
        using var managerClient = factory.CreateSecureClient();
        var csrf = await CompleteManagerLoginAsync(managerClient, manager);

        var target = await factory.CreateUserAsync(
            $"mfa-method-{marker}@example.test",
            ValidPassword);
        var recoveryCodes = await factory.EnableAuthenticatorAsync(target.Id);
        var oldAuthenticatorCode = await factory.GenerateAuthenticatorCodeAsync(target.Id);
        using var targetClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Accepted, (await LoginAsync(targetClient, target.Email!)).StatusCode);
        var targetContext = await targetClient.GetFromJsonAsync<MfaContextBody>("/api/auth/mfa/context");
        Assert.NotNull(targetContext);
        Assert.Equal(HttpStatusCode.NoContent, (await SendJsonWithCsrfAsync(
            targetClient,
            HttpMethod.Post,
            "/api/auth/mfa/verify",
            new { code = await factory.GenerateAuthenticatorCodeAsync(target.Id) },
            targetContext.RequestToken)).StatusCode);

        var details = await managerClient.GetFromJsonAsync<UserDetailsResponse>(
            $"/api/access-management/users/{target.Id}");
        Assert.NotNull(details);
        var switchedToEmailResponse = await SendJsonWithCsrfAsync(
            managerClient,
            HttpMethod.Put,
            $"/api/access-management/users/{target.Id}/mfa",
            new { isEnabled = true, method = "Email", editorVersion = details.EditorVersion },
            csrf);
        Assert.Equal(HttpStatusCode.OK, switchedToEmailResponse.StatusCode);
        var emailDetails = await switchedToEmailResponse.Content.ReadFromJsonAsync<UserDetailsResponse>();
        Assert.NotNull(emailDetails);
        Assert.Equal("Email", emailDetails.MfaMethod);
        Assert.True(emailDetails.IsMfaEnabled);
        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/auth/me")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(target.Id.ToString());
            Assert.NotNull(stored);
            Assert.Equal(UserMfaMethod.Email, stored.MfaMethod);
            Assert.Equal(0, await userManager.CountRecoveryCodesAsync(stored));
            Assert.False(await userManager.VerifyTwoFactorTokenAsync(
                stored,
                userManager.Options.Tokens.AuthenticatorTokenProvider,
                oldAuthenticatorCode));
        }

        var switchedBackResponse = await SendJsonWithCsrfAsync(
            managerClient,
            HttpMethod.Put,
            $"/api/access-management/users/{target.Id}/mfa",
            new { isEnabled = true, method = "Authenticator", editorVersion = emailDetails.EditorVersion },
            csrf);
        Assert.Equal(HttpStatusCode.OK, switchedBackResponse.StatusCode);
        var authenticatorDetails = await switchedBackResponse.Content.ReadFromJsonAsync<UserDetailsResponse>();
        Assert.NotNull(authenticatorDetails);
        Assert.Equal("Authenticator", authenticatorDetails.MfaMethod);
        Assert.False(authenticatorDetails.IsMfaEnabled);
        Assert.NotEmpty(recoveryCodes);

        using var enrollmentClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Accepted, (await LoginAsync(enrollmentClient, target.Email!)).StatusCode);
        var enrollmentContext = await enrollmentClient.GetFromJsonAsync<MfaContextBody>(
            "/api/auth/mfa/context");
        Assert.NotNull(enrollmentContext);
        Assert.Equal("mfaEnrollmentRequired", enrollmentContext.Status);
        Assert.Equal("Authenticator", enrollmentContext.Method);
    }

    [Fact]
    public async Task UserListRequiresAuthenticationAndUserViewPermission()
    {
        using var anonymousClient = factory.CreateSecureClient();
        var anonymousResponse = await anonymousClient.GetAsync("/api/access-management/users");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var actor = await factory.CreateUserAsync(
            $"user-list-denied-{Guid.NewGuid():N}@example.test",
            ValidPassword);
        using var forbiddenClient = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await LoginAsync(forbiddenClient, actor.Email!)).StatusCode);

        var forbiddenResponse = await forbiddenClient.GetAsync("/api/access-management/users");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        var csrf = await GetCsrfTokenAsync(forbiddenClient);
        var forbiddenMutation = await SendJsonWithCsrfAsync(
            forbiddenClient,
            HttpMethod.Put,
            $"/api/access-management/users/{actor.Id}/general",
            new
            {
                firstName = "Test",
                lastName = "User",
                email = actor.Email,
                phoneNumber = (string?)null,
                editorVersion = new string('A', 64),
            },
            csrf);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenMutation.StatusCode);
    }

    [Fact]
    public async Task UserListSupportsSearchFiltersMultipleRolesMfaAndPagination()
    {
        var marker = $"directory{Guid.NewGuid():N}";
        var actor = await factory.CreateUserAsync(
            $"actor-{marker}@example.test",
            ValidPassword);
        await factory.GrantPermissionAsync(actor.Id, Permissions.User.View);

        var firstRole = await CreateRoleAsync($"Alpha {marker}");
        var secondRole = await CreateRoleAsync($"Beta {marker}");
        var first = await CreateProfiledUserAsync(
            $"first-{marker}@example.test",
            marker,
            "Alpha",
            true);
        var second = await CreateProfiledUserAsync(
            $"second-{marker}@example.test",
            marker,
            "Beta",
            true);
        _ = await CreateProfiledUserAsync(
            $"inactive-{marker}@example.test",
            marker,
            "Inactive",
            false);
        await AssignRoleAsync(first, firstRole);
        await AssignRoleAsync(first, secondRole);
        await AssignRoleAsync(second, firstRole);
        await factory.EnableAuthenticatorAsync(first.Id);

        using var client = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(client, actor.Email!)).StatusCode);

        var firstPage = await client.GetFromJsonAsync<UserListResponse>(
            $"/api/access-management/users?search={marker}&isActive=true"
            + $"&roleId={firstRole.Id}&pageNumber=1&pageSize=1&sortBy=email&sortDirection=asc");

        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.TotalCount);
        Assert.Single(firstPage.Items);
        Assert.Equal(1, firstPage.PageNumber);
        Assert.Equal(1, firstPage.PageSize);
        Assert.Contains(firstPage.AvailableRoles, role => role.Id == firstRole.Id);
        Assert.Contains(firstPage.AvailableRoles, role => role.Id == secondRole.Id);

        var enabled = await client.GetFromJsonAsync<UserListResponse>(
            $"/api/access-management/users?search={marker}&mfaStatus=enabled&pageSize=25");
        Assert.NotNull(enabled);
        var enabledUser = Assert.Single(enabled.Items);
        Assert.Equal(first.Id, enabledUser.Id);
        Assert.Equal("enabled", enabledUser.MfaStatus);
        Assert.Equal(
            [firstRole.Name, secondRole.Name],
            enabledUser.Roles.Select(role => role.Name));

        var inactive = await client.GetFromJsonAsync<UserListResponse>(
            $"/api/access-management/users?search={marker}&isActive=false&pageSize=25");
        Assert.NotNull(inactive);
        Assert.Single(inactive.Items);
        Assert.False(inactive.Items[0].IsActive);
        Assert.Empty(inactive.Items[0].Roles);

        var byDisplayName = await client.GetFromJsonAsync<UserListResponse>(
            $"/api/access-management/users?search={Uri.EscapeDataString($"{marker} Beta")}&pageSize=25");
        Assert.NotNull(byDisplayName);
        Assert.Equal(second.Id, Assert.Single(byDisplayName.Items).Id);

        var byEmail = await client.GetFromJsonAsync<UserListResponse>(
            $"/api/access-management/users?search={Uri.EscapeDataString(second.Email!)}&pageSize=25");
        Assert.NotNull(byEmail);
        Assert.Equal(second.Id, Assert.Single(byEmail.Items).Id);
    }

    [Fact]
    public async Task UserListResponseExcludesSensitiveIdentityFieldsAndModelHasNoDrift()
    {
        var actor = await factory.CreateUserAsync(
            $"user-list-safe-{Guid.NewGuid():N}@example.test",
            ValidPassword);
        await factory.GrantPermissionAsync(actor.Id, Permissions.User.View);
        using var client = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(client, actor.Email!)).StatusCode);

        var response = await client.GetAsync(
            $"/api/access-management/users?search={Uri.EscapeDataString(actor.Email!)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var propertyNames = EnumeratePropertyNames(document.RootElement).ToArray();

        Assert.DoesNotContain(propertyNames, name => name.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("stamp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("lockout", StringComparison.OrdinalIgnoreCase));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    private async Task<ApplicationUser> CreateProfiledUserAsync(
        string email,
        string firstName,
        string lastName,
        bool isActive)
    {
        var user = await factory.CreateUserAsync(email, ValidPassword, isActive);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = await userManager.FindByIdAsync(user.Id.ToString())
            ?? throw new InvalidOperationException("The expected user does not exist.");
        stored.FirstName = firstName;
        stored.LastName = lastName;
        stored.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Assert.True((await userManager.UpdateAsync(stored)).Succeeded);
        return stored;
    }

    private async Task<ApplicationRole> CreateRoleAsync(string name)
    {
        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var role = new ApplicationRole { Name = name };
        Assert.True((await roleManager.CreateAsync(role)).Succeeded);
        return role;
    }

    private async Task<ApplicationUser> CreateManagerAsync()
    {
        var manager = await factory.CreateUserAsync(
            $"user-manager-{Guid.NewGuid():N}@example.test",
            ValidPassword);
        await factory.GrantPermissionAsync(manager.Id, Permissions.User.Manage);
        await factory.EnableAuthenticatorAsync(manager.Id);
        return manager;
    }

    private async Task<string> CompleteManagerLoginAsync(
        HttpClient client,
        ApplicationUser manager)
    {
        Assert.Equal(HttpStatusCode.Accepted, (await LoginAsync(client, manager.Email!)).StatusCode);
        var mfaContext = await client.GetFromJsonAsync<MfaContextBody>("/api/auth/mfa/context");
        Assert.NotNull(mfaContext);
        var code = await factory.GenerateAuthenticatorCodeAsync(manager.Id);
        var response = await SendJsonWithCsrfAsync(
            client,
            HttpMethod.Post,
            "/api/auth/mfa/verify",
            new { code },
            mfaContext.RequestToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return await GetCsrfTokenAsync(client);
    }

    private async Task GrantRolePermissionAsync(Guid roleId, string permissionName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var permissionId = await context.Set<Permission>()
            .Where(permission => permission.Name == permissionName)
            .Select(permission => permission.Id)
            .SingleAsync();
        context.Add(new RolePermission(roleId, permissionId));
        await context.SaveChangesAsync();
    }

    private async Task AssignRoleAsync(ApplicationUser user, ApplicationRole role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = await userManager.FindByIdAsync(user.Id.ToString())
            ?? throw new InvalidOperationException("The expected user does not exist.");
        var roleName = role.Name
            ?? throw new InvalidOperationException("The expected role does not have a name.");
        Assert.True((await userManager.AddToRoleAsync(stored, roleName)).Succeeded);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("requestToken").GetString()
            ?? throw new InvalidOperationException("The CSRF endpoint did not return a token.");
    }

    private static async Task<HttpResponseMessage> SendJsonWithCsrfAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object payload,
        string csrfToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(payload) };
        request.Headers.Add("X-XSRF-TOKEN", csrfToken);
        return await client.SendAsync(request);
    }

    private static IEnumerable<string> EnumeratePropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return property.Name;
                foreach (var nested in EnumeratePropertyNames(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nested in EnumeratePropertyNames(item))
                {
                    yield return nested;
                }
            }
        }
    }

    private sealed record MfaContextBody(string Status, string Method, string RequestToken);
}
