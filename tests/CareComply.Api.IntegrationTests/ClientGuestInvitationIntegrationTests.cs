using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using CareComply.Domain.Companies;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class ClientGuestInvitationIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task AuthorizedTenantUserCreatesHashedInvitationWithoutCreatingClientOrLeakingSensitiveData()
    {
        Assert.Contains(Permissions.Client.GuestInvite, Permissions.All);
        var actor = await factory.CreateUserAsync($"guest-invite-{Guid.NewGuid():N}@example.test", Password);
        await factory.GrantPermissionAsync(actor.Id, Permissions.Client.GuestInvite);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        int clientsBefore;
        await using (var beforeScope = factory.Services.CreateAsyncScope())
        {
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            clientsBefore = await beforeDb.Clients.CountAsync();
        }
        var before = DateTimeOffset.UtcNow;

        var response = await http.PostAsJsonAsync("/api/clients/guest-invitations", new
        {
            clientName = "  John Smith  ",
            email = "  JOHN.SMITH@Example.COM  ",
            mobileNumber = "  +971 50 123 4567  ",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseText = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(responseText);
        var invitationId = json.RootElement.GetProperty("invitationId").GetGuid();
        var guestLink = json.RootElement.GetProperty("guestLink").GetString()!;
        var expiresAt = json.RootElement.GetProperty("expiresAtUtc").GetDateTimeOffset();
        Assert.InRange(expiresAt, before.AddDays(7).AddMinutes(-1), before.AddDays(7).AddMinutes(1));
        Assert.DoesNotContain("tokenHash", responseText, StringComparison.OrdinalIgnoreCase);
        var uri = new Uri(guestLink);
        Assert.Equal("https://carecomply.example.test/client-intake", uri.GetLeftPart(UriPartial.Path));
        Assert.Empty(uri.Query);
        Assert.StartsWith("#token=", uri.Fragment, StringComparison.Ordinal);
        var publicToken = uri.Fragment[7..];
        Assert.Equal(43, publicToken.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", publicToken);
        var rawBytes = DecodeBase64Url(publicToken);
        Assert.Equal(32, rawBytes.Length);
        var secondResponse = await http.PostAsJsonAsync(
            "/api/clients/guest-invitations",
            ValidRequest());
        secondResponse.EnsureSuccessStatusCode();
        using var secondJson = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        var secondToken = new Uri(secondJson.RootElement.GetProperty("guestLink").GetString()!).Fragment[7..];
        Assert.NotEqual(publicToken, secondToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var persisted = await scope.ServiceProvider.GetRequiredService<CareComplyDbContext>()
            .ClientGuestInvitations.AsNoTracking().SingleAsync(x => x.Id == invitationId);
        Assert.Equal(SqlServerApiFactory.DefaultCompanyId, persisted.CompanyId);
        Assert.Equal("John Smith", persisted.ClientName);
        Assert.Equal("john.smith@example.com", persisted.Email);
        Assert.Equal("+971 50 123 4567", persisted.MobileNumber);
        Assert.Equal(SHA256.HashData(rawBytes), persisted.TokenHash);
        Assert.False(persisted.TokenHash.AsSpan().SequenceEqual(rawBytes));
        Assert.Null(persisted.CreatedClientId);
        Assert.Null(persisted.SentAtUtc);
        Assert.Null(persisted.OpenedAtUtc);
        Assert.Null(persisted.CompletedAtUtc);
        Assert.Null(persisted.RevokedAtUtc);
        Assert.NotEmpty(persisted.RowVersion);
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal(clientsBefore, await db.Clients.CountAsync());
        var tokenIndex = db.Model.FindEntityType(typeof(ClientGuestInvitation))!.GetIndexes()
            .Single(index => index.GetDatabaseName() == "UX_ClientGuestInvitations_TokenHash");
        Assert.True(tokenIndex.IsUnique);
        var audit = await db.Set<AuditLog>().AsNoTracking().SingleAsync(x =>
            x.ActorUserId == actor.Id && x.Action == "ClientGuestInvitation.Created"
                && x.ResourceId == invitationId.ToString("N"));
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        var serializedAudit = $"{audit.MetadataJson} {audit.ResourceId}";
        Assert.DoesNotContain("John Smith", serializedAudit, StringComparison.Ordinal);
        Assert.DoesNotContain("john.smith", serializedAudit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(publicToken, serializedAudit, StringComparison.Ordinal);
        Assert.DoesNotContain(guestLink, serializedAudit, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexString(persisted.TokenHash), serializedAudit, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvitationEndpointRequiresAuthenticationAndDedicatedPermission()
    {
        using var anonymous = factory.CreateSecureClient();
        var unauthenticated = await anonymous.PostAsJsonAsync(
            "/api/clients/guest-invitations",
            ValidRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        var actor = await factory.CreateUserAsync($"no-invite-{Guid.NewGuid():N}@example.test", Password);
        await factory.GrantPermissionAsync(actor.Id, Permissions.Client.Create);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var forbidden = await http.PostAsJsonAsync("/api/clients/guest-invitations", ValidRequest());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task TenantCannotOverrideCompanyAndValidationRejectsInvalidContactData()
    {
        var actor = await factory.CreateUserAsync($"tenant-invite-{Guid.NewGuid():N}@example.test", Password);
        await factory.GrantPermissionAsync(actor.Id, Permissions.Client.GuestInvite);
        var otherCompanyId = await CreateCompanyAsync();
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var overrideAttempt = await http.PostAsJsonAsync("/api/clients/guest-invitations", new
        {
            clientName = "Client",
            email = "client@example.test",
            companyId = otherCompanyId,
        });
        Assert.Equal(HttpStatusCode.NotFound, overrideAttempt.StatusCode);

        var invalid = await http.PostAsJsonAsync("/api/clients/guest-invitations", new
        {
            clientName = "",
            email = "not-email",
            mobileNumber = "abc",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var body = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("clientName", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mobileNumber", body, StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.False(await db.ClientGuestInvitations.AnyAsync(x => x.CreatedByUserId == actor.Id));
    }

    [Fact]
    public async Task PlatformAdministratorMustSelectAnActiveCompany()
    {
        var actor = await factory.CreateUserAsync($"platform-invite-{Guid.NewGuid():N}@example.test", Password);
        await factory.GrantPermissionAsync(actor.Id, Permissions.Client.GuestInvite);
        var companyId = await CreateCompanyAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == actor.Id);
            user.IsPlatformAdministrator = true;
            user.CompanyId = null;
            await db.SaveChangesAsync();
        }
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var missing = await http.PostAsJsonAsync("/api/clients/guest-invitations", ValidRequest());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var created = await http.PostAsJsonAsync("/api/clients/guest-invitations", new
        {
            clientName = "Platform client",
            email = "platform-client@example.test",
            companyId,
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var invitation = await verifyScope.ServiceProvider.GetRequiredService<CareComplyDbContext>()
            .ClientGuestInvitations.AsNoTracking().SingleAsync(x => x.CreatedByUserId == actor.Id);
        Assert.Equal(companyId, invitation.CompanyId);
    }

    [Fact]
    public async Task AuthorizedUserSendsCanonicalEmailAndPersistsOnlyLatestSendMetadata()
    {
        factory.ClientGuestInvitationEmailSender.Clear();
        var actor = await CreateActorAsync();
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var generated = await GenerateInvitationAsync(http, "Email Client", "persisted@example.test");
        byte[] originalHash;
        int clientsBefore;
        await using (var beforeScope = factory.Services.CreateAsyncScope())
        {
            var db = beforeScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var invitation = await db.ClientGuestInvitations.AsNoTracking()
                .SingleAsync(x => x.Id == generated.Id);
            originalHash = invitation.TokenHash.ToArray();
            clientsBefore = await db.Clients.CountAsync();
        }

        var response = await SendInvitationAsync(http, generated.Id, generated.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseText = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(generated.Token, responseText, StringComparison.Ordinal);
        Assert.DoesNotContain("tokenHash", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("guestLink", responseText, StringComparison.OrdinalIgnoreCase);
        var message = Assert.Single(factory.ClientGuestInvitationEmailSender.Messages);
        var canonicalLink = $"https://carecomply.example.test/client-intake#token={generated.Token}";
        Assert.Equal("persisted@example.test", message.RecipientAddress);
        Assert.Contains("Email Client", message.PlainText, StringComparison.Ordinal);
        Assert.Contains("Email Client", message.Html, StringComparison.Ordinal);
        Assert.Contains(canonicalLink, message.PlainText, StringComparison.Ordinal);
        Assert.Contains(canonicalLink, message.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("?token=", message.PlainText, StringComparison.Ordinal);
        Assert.Contains("expires", message.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not share", message.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(generated.Id.ToString(), message.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToHexString(originalHash), message.Html, StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbAfter = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await dbAfter.ClientGuestInvitations.AsNoTracking()
            .SingleAsync(x => x.Id == generated.Id);
        Assert.NotNull(persisted.SentAtUtc);
        Assert.Equal(originalHash, persisted.TokenHash);
        Assert.Null(persisted.OpenedAtUtc);
        Assert.Null(persisted.CompletedAtUtc);
        Assert.Null(persisted.RevokedAtUtc);
        Assert.Null(persisted.CreatedClientId);
        Assert.Equal(clientsBefore, await dbAfter.Clients.CountAsync());
        var audit = await dbAfter.Set<AuditLog>().AsNoTracking().SingleAsync(x =>
            x.ActorUserId == actor.Id && x.Action == "ClientGuestInvitation.Sent"
                && x.ResourceId == generated.Id.ToString("N"));
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.True(string.IsNullOrEmpty(audit.MetadataJson) || audit.MetadataJson == "{}");
        var serializedAudit = $"{audit.MetadataJson} {audit.ResourceId}";
        Assert.DoesNotContain("Email Client", serializedAudit, StringComparison.Ordinal);
        Assert.DoesNotContain("persisted@example.test", serializedAudit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(generated.Token, serializedAudit, StringComparison.Ordinal);
        Assert.DoesNotContain(canonicalLink, serializedAudit, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendRequiresAuthenticationPermissionAndCompanyScopeWhilePlatformAdminCanSend()
    {
        factory.ClientGuestInvitationEmailSender.Clear();
        var owner = await CreateActorAsync();
        using var ownerHttp = factory.CreateSecureClient();
        await SignInAsync(ownerHttp, owner.Email!);
        var generated = await GenerateInvitationAsync(ownerHttp, "Scoped Client", "scoped@example.test");

        using var anonymous = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendInvitationAsync(anonymous, generated.Id, generated.Token)).StatusCode);

        var unauthorized = await factory.CreateUserAsync($"no-send-{Guid.NewGuid():N}@example.test", Password);
        using var unauthorizedHttp = factory.CreateSecureClient();
        await SignInAsync(unauthorizedHttp, unauthorized.Email!);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendInvitationAsync(unauthorizedHttp, generated.Id, generated.Token)).StatusCode);

        var otherCompanyId = await CreateCompanyAsync();
        var other = await CreateActorAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == other.Id);
            user.CompanyId = otherCompanyId;
            await db.SaveChangesAsync();
        }
        using var otherHttp = factory.CreateSecureClient();
        await SignInAsync(otherHttp, other.Email!);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendInvitationAsync(otherHttp, generated.Id, generated.Token)).StatusCode);

        var platform = await CreateActorAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == platform.Id);
            user.IsPlatformAdministrator = true;
            user.CompanyId = null;
            await db.SaveChangesAsync();
        }
        using var platformHttp = factory.CreateSecureClient();
        await SignInAsync(platformHttp, platform.Email!);
        Assert.Equal(HttpStatusCode.OK,
            (await SendInvitationAsync(platformHttp, generated.Id, generated.Token)).StatusCode);
        Assert.Single(factory.ClientGuestInvitationEmailSender.Messages);
    }

    [Fact]
    public async Task SendRejectsMalformedWrongLengthAndMismatchedTokensWithoutChangingHash()
    {
        factory.ClientGuestInvitationEmailSender.Clear();
        var actor = await CreateActorAsync();
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var generated = await GenerateInvitationAsync(http, "Token Client", "token@example.test");
        var originalHash = SHA256.HashData(DecodeBase64Url(generated.Token));

        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendInvitationAsync(http, generated.Id, "not+a+base64url+token")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendInvitationAsync(http, generated.Id, EncodeBase64Url(RandomNumberGenerator.GetBytes(16)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendInvitationAsync(http, generated.Id, EncodeBase64Url(RandomNumberGenerator.GetBytes(32)))).StatusCode);
        Assert.Empty(factory.ClientGuestInvitationEmailSender.Messages);

        await using var scope = factory.Services.CreateAsyncScope();
        var persisted = await scope.ServiceProvider.GetRequiredService<CareComplyDbContext>()
            .ClientGuestInvitations.AsNoTracking().SingleAsync(x => x.Id == generated.Id);
        Assert.Equal(originalHash, persisted.TokenHash);
        Assert.Null(persisted.SentAtUtc);
    }

    [Fact]
    public async Task LifecycleAllowsResendOpenedAndBoundClientButRejectsExpiredRevokedAndCompleted()
    {
        factory.ClientGuestInvitationEmailSender.Clear();
        var actor = await CreateActorAsync();
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var generated = await GenerateInvitationAsync(http, "Lifecycle Client", "life@example.test");
        Assert.Equal(HttpStatusCode.OK,
            (await SendInvitationAsync(http, generated.Id, generated.Token)).StatusCode);
        DateTimeOffset firstSent;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var invitation = await db.ClientGuestInvitations.SingleAsync(x => x.Id == generated.Id);
            firstSent = invitation.SentAtUtc!.Value;
            invitation.MarkOpened(DateTimeOffset.UtcNow);
            var location = await db.CompanyLocations.AsNoTracking()
                .FirstAsync(x => x.CompanyId == SqlServerApiFactory.DefaultCompanyId);
            var clientId = Guid.CreateVersion7();
            var client = Client.Create(clientId, SqlServerApiFactory.DefaultCompanyId, "Bound", null, "Client", null,
                new DateOnly(1990, 1, 1), ClientGender.Unknown, $"PMI-{Guid.NewGuid():N}",
                new DateOnly(2026, 1, 1), null, ClientStatus.Pending, DateTimeOffset.UtcNow);
            client.AssignLocation(location.Id, location.Name, DateTimeOffset.UtcNow);
            db.Clients.Add(client);
            invitation.BindCreatedClient(clientId, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK,
            (await SendInvitationAsync(http, generated.Id, generated.Token)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var invitation = await scope.ServiceProvider.GetRequiredService<CareComplyDbContext>()
                .ClientGuestInvitations.AsNoTracking().SingleAsync(x => x.Id == generated.Id);
            Assert.True(invitation.SentAtUtc >= firstSent);
            Assert.NotNull(invitation.OpenedAtUtc);
            Assert.NotNull(invitation.CreatedClientId);
        }

        foreach (var state in new[] { "expired", "revoked", "completed" })
        {
            var item = await InsertInvitationAsync(actor.Id, state);
            var unavailable = await SendInvitationAsync(http, item.Id, item.Token);
            Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        }
        Assert.Equal(2, factory.ClientGuestInvitationEmailSender.Messages.Count);
    }

    [Fact]
    public async Task DeliveryFailurePreservesInvitationAndFailedAuditThenRetrySucceeds()
    {
        factory.ClientGuestInvitationEmailSender.Clear();
        factory.ClientGuestInvitationEmailSender.DeliverySucceeds = false;
        var actor = await CreateActorAsync();
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var generated = await GenerateInvitationAsync(http, "Retry Client", "retry@example.test");

        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await SendInvitationAsync(http, generated.Id, generated.Token)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var invitation = await db.ClientGuestInvitations.AsNoTracking().SingleAsync(x => x.Id == generated.Id);
            Assert.Null(invitation.SentAtUtc);
            Assert.Null(invitation.RevokedAtUtc);
            Assert.Null(invitation.CompletedAtUtc);
            var audit = await db.Set<AuditLog>().AsNoTracking().SingleAsync(x =>
                x.ActorUserId == actor.Id && x.Action == "ClientGuestInvitation.Sent");
            Assert.Equal(AuditOutcome.Failed, audit.Outcome);
            Assert.Equal("{\"reasonCode\":\"DeliveryFailed\"}", audit.MetadataJson);
        }

        factory.ClientGuestInvitationEmailSender.DeliverySucceeds = true;
        Assert.Equal(HttpStatusCode.OK,
            (await SendInvitationAsync(http, generated.Id, generated.Token)).StatusCode);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var persisted = await verifyScope.ServiceProvider.GetRequiredService<CareComplyDbContext>()
            .ClientGuestInvitations.AsNoTracking().SingleAsync(x => x.Id == generated.Id);
        Assert.NotNull(persisted.SentAtUtc);
        Assert.Equal(SHA256.HashData(DecodeBase64Url(generated.Token)), persisted.TokenHash);
    }

    private static object ValidRequest() => new
    {
        clientName = "Example Client",
        email = "client@example.test",
        mobileNumber = (string?)null,
    };

    private async Task<ApplicationUser> CreateActorAsync()
    {
        var actor = await factory.CreateUserAsync($"guest-send-{Guid.NewGuid():N}@example.test", Password);
        await factory.GrantPermissionAsync(actor.Id, Permissions.Client.GuestInvite);
        return actor;
    }

    private static Task<HttpResponseMessage> SendInvitationAsync(HttpClient http, Guid id, string token) =>
        http.PostAsJsonAsync($"/api/clients/guest-invitations/{id}/send", new { token });

    private static string EncodeBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task<(Guid Id, string Token)> GenerateInvitationAsync(
        HttpClient http,
        string clientName,
        string email)
    {
        var response = await http.PostAsJsonAsync("/api/clients/guest-invitations", new
        {
            clientName,
            email,
            mobileNumber = (string?)null,
        });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var link = new Uri(json.RootElement.GetProperty("guestLink").GetString()!);
        return (json.RootElement.GetProperty("invitationId").GetGuid(), link.Fragment[7..]);
    }

    private async Task<(Guid Id, string Token)> InsertInvitationAsync(Guid createdByUserId, string state)
    {
        var now = DateTimeOffset.UtcNow;
        var token = EncodeBase64Url(RandomNumberGenerator.GetBytes(32));
        var createdAt = state == "expired" ? now.AddDays(-2) : now;
        var expiresAt = state == "expired" ? now.AddDays(-1) : now.AddDays(7);
        var invitation = ClientGuestInvitation.Create(
            Guid.CreateVersion7(), SqlServerApiFactory.DefaultCompanyId, "State Client",
            $"{state}-{Guid.NewGuid():N}@example.test", null,
            SHA256.HashData(DecodeBase64Url(token)), expiresAt, createdAt, createdByUserId);
        if (state == "revoked") invitation.Revoke(now);
        if (state == "completed") invitation.Complete(now);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.ClientGuestInvitations.Add(invitation);
        await db.SaveChangesAsync();
        return (invitation.Id, token);
    }

    private async Task<Guid> CreateCompanyAsync()
    {
        var id = Guid.CreateVersion7();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.Companies.Add(Company.Create(id, $"Invitation {id:N}", $"INV-{id:N}", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return id;
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(padded);
    }

    private static async Task SignInAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await client.GetAsync("/api/auth/csrf");
        using var document = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", document.RootElement.GetProperty("requestToken").GetString());
    }
}
