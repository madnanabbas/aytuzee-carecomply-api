using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class ClientDocumentsIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task EndpointsEnforcePermissionScopeValidationVersioningHeadersAndAuditPrivacy()
    {
        var anonymous = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/clients/{Guid.NewGuid()}/documents")).StatusCode);

        var deniedActor = await factory.CreateUserAsync($"documents-denied-{Guid.NewGuid():N}@example.test", Password);
        var denied = factory.CreateSecureClient();
        await SignInAsync(denied, deniedActor.Email!);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await denied.GetAsync($"/api/clients/{Guid.NewGuid()}/documents")).StatusCode);

        var setup = await SetupAsync();
        var client = factory.CreateSecureClient();
        await SignInAsync(client, setup.Actor.Email!);

        var upload = await UploadAsync(client, setup.ClientId, Png(), "Private Care Plan", "care-plan.png");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var created = await ReadDetailsAsync(upload);
        Assert.Equal(1, created.Document.CurrentVersionNumber);

        var list = await client.GetFromJsonAsync<ClientDocumentResponse[]>(
            $"/api/clients/{setup.ClientId}/documents");
        var listed = Assert.Single(list!);
        Assert.Equal("Private Care Plan", listed.Title);

        Assert.Equal(HttpStatusCode.Conflict,
            (await UploadAsync(client, setup.ClientId, Png(), "Duplicate", "duplicate.png")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await UploadAsync(client, setup.ClientId, "<html>unsafe</html>"u8.ToArray(), "Spoofed", "spoofed.pdf")).StatusCode);

        var content = await client.GetAsync(
            $"/api/clients/{setup.ClientId}/documents/{created.Document.Id}/versions/{created.Document.CurrentVersionId}/content");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("no-store", content.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", content.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("attachment", content.Content.Headers.ContentDisposition?.ToString(), StringComparison.OrdinalIgnoreCase);

        var replacedResponse = await ReplaceAsync(client, setup.ClientId, created.Document.Id,
            created.Document.EditorVersion, ChangedPng(1), "replacement.png");
        Assert.Equal(HttpStatusCode.OK, replacedResponse.StatusCode);
        var replaced = await ReadDetailsAsync(replacedResponse);
        Assert.Equal(2, replaced.Document.CurrentVersionNumber);
        Assert.Equal(2, replaced.Versions.Count);
        Assert.Single(replaced.Versions, version => version.IsCurrent);

        Assert.Equal(HttpStatusCode.Conflict,
            (await ArchiveAsync(client, setup.ClientId, created.Document.Id, created.Document.EditorVersion)).StatusCode);
        var archivedResponse = await ArchiveAsync(client, setup.ClientId, replaced.Document.Id, replaced.Document.EditorVersion);
        Assert.Equal(HttpStatusCode.OK, archivedResponse.StatusCode);
        Assert.Equal("Archived", (await ReadDetailsAsync(archivedResponse)).Document.Status);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/clients/{setup.InaccessibleLocationClientId}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/clients/{setup.OtherCompanyClientId}/documents")).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var metadata = await db.Set<AuditLog>().AsNoTracking()
            .Where(x => x.ActorUserId == setup.Actor.Id && x.ResourceType == "ClientDocument")
            .Select(x => x.MetadataJson ?? string.Empty).ToArrayAsync();
        Assert.NotEmpty(metadata);
        Assert.All(metadata, value =>
        {
            Assert.DoesNotContain("Private Care Plan", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("care-plan.png", value, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task ConcurrentReplacementsLeaveExactlyOneCurrentVersionAndPreserveTheWinner()
    {
        var setup = await SetupAsync();
        var client = factory.CreateSecureClient();
        await SignInAsync(client, setup.Actor.Email!);
        var created = await ReadDetailsAsync(await UploadAsync(
            client, setup.ClientId, Png(), "Concurrent", "initial.png"));

        var attempts = await Task.WhenAll(
            ReplaceAsync(client, setup.ClientId, created.Document.Id, created.Document.EditorVersion, ChangedPng(2), "two.png"),
            ReplaceAsync(client, setup.ClientId, created.Document.Id, created.Document.EditorVersion, ChangedPng(3), "three.png"));
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Conflict);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var document = await db.ClientDocuments.AsNoTracking().SingleAsync(x => x.Id == created.Document.Id);
        var versions = await db.ClientDocumentVersions.AsNoTracking()
            .Where(x => x.DocumentId == created.Document.Id).ToArrayAsync();
        var current = Assert.Single(versions, version => version.IsCurrent);
        Assert.Equal(current.Id, document.CurrentVersionId);
        Assert.Equal(2, versions.Length);
    }

    private async Task<Setup> SetupAsync()
    {
        var actor = await factory.CreateUserAsync($"documents-{Guid.NewGuid():N}@example.test", Password);
        foreach (var permission in new[] { Permissions.ClientDocument.View, Permissions.ClientDocument.Upload,
                     Permissions.ClientDocument.Download, Permissions.ClientDocument.Replace, Permissions.ClientDocument.Archive })
            await factory.GrantPermissionAsync(actor.Id, permission);
        var location = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        await GiveLocationAccessAsync(actor.Id, location);
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, location);
        var inaccessibleLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        var inaccessibleClient = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, inaccessibleLocation);
        var other = await CreateCompanyAsync();
        return new(actor, clientId, inaccessibleClient, await CreateClientAsync(other.CompanyId, other.LocationId));
    }

    private async Task<Guid> CreateLocationAsync(Guid companyId)
    {
        var id = Guid.CreateVersion7();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.CompanyLocations.Add(CareComply.Domain.Companies.CompanyLocation.Create(
            id, companyId, $"Documents {id:N}", false, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<(Guid CompanyId, Guid LocationId)> CreateCompanyAsync()
    {
        var companyId = Guid.CreateVersion7(); var locationId = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.Companies.Add(CareComply.Domain.Companies.Company.Create(companyId, $"Documents {companyId:N}", $"DOC-{companyId:N}", now));
        db.CompanyLocations.Add(CareComply.Domain.Companies.CompanyLocation.Create(locationId, companyId, "Main", true, now));
        await db.SaveChangesAsync();
        return (companyId, locationId);
    }

    private async Task<Guid> CreateClientAsync(Guid companyId, Guid locationId)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        var client = Client.Create(id, companyId, "Document", null, "Client", null,
            new DateOnly(1990, 1, 1), ClientGender.Unknown, $"PMI-{Guid.NewGuid():N}",
            new DateOnly(2026, 1, 1), null, ClientStatus.Active, now);
        client.AssignLocation(locationId, "Documents", now);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.Clients.Add(client); await db.SaveChangesAsync(); return id;
    }

    private async Task GiveLocationAccessAsync(Guid userId, Guid locationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.UserLocations.Add(new ApplicationUserLocation { UserId = userId, LocationId = locationId });
        await db.SaveChangesAsync();
    }

    private static async Task SignInAsync(HttpClient client, string email)
    {
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password })).StatusCode);
        using var json = JsonDocument.Parse(await (await client.GetAsync("/api/auth/csrf")).Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", json.RootElement.GetProperty("requestToken").GetString());
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid clientId, byte[] bytes, string title, string name)
    {
        var body = new MultipartFormDataContent();
        body.Add(new StringContent("Clinical"), "Category"); body.Add(new StringContent(title), "Title");
        var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        body.Add(file, "File", name);
        return client.PostAsync($"/api/clients/{clientId}/documents", body);
    }

    private static Task<HttpResponseMessage> ReplaceAsync(HttpClient client, Guid clientId, Guid documentId, string version, byte[] bytes, string name)
    {
        var body = new MultipartFormDataContent(); body.Add(new StringContent(version), "EditorVersion");
        var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        body.Add(file, "File", name);
        return client.PostAsync($"/api/clients/{clientId}/documents/{documentId}/versions", body);
    }

    private static Task<HttpResponseMessage> ArchiveAsync(HttpClient client, Guid clientId, Guid documentId, string version) =>
        client.PostAsJsonAsync($"/api/clients/{clientId}/documents/{documentId}/archive", new { editorVersion = version });

    private static async Task<ClientDocumentDetailsResponse> ReadDetailsAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClientDocumentDetailsResponse>())!;
    }

    private static byte[] Png() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private static byte[] ChangedPng(byte marker) { var value = Png(); value[40] = marker; return value; }

    private sealed record Setup(ApplicationUser Actor, Guid ClientId, Guid InaccessibleLocationClientId, Guid OtherCompanyClientId);
    private sealed record ClientDocumentResponse(Guid Id, string Title);
    private sealed record ClientDocumentDetailsResponse(ClientDocumentData Document, IReadOnlyList<ClientDocumentVersionData> Versions);
    private sealed record ClientDocumentData(Guid Id, string Title, string Status, Guid CurrentVersionId, int CurrentVersionNumber, string EditorVersion);
    private sealed record ClientDocumentVersionData(Guid Id, bool IsCurrent);
}
