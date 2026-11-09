using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientForms;
using CareComply.Domain.Clients;
using CareComply.Domain.Companies;
using CareComply.Domain.Forms;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Forms;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using CareComply.Api.Development;

namespace CareComply.Api.IntegrationTests;

public sealed class ClientFormsIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task EndpointsRequirePermissionAndReturnOnlySafePublishedFormData()
    {
        var anonymousClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymousClient.GetAsync($"/api/clients/{Guid.NewGuid()}/form-templates")).StatusCode);

        var deniedUser = await factory.CreateUserAsync(
            $"forms-denied-{Guid.NewGuid():N}@example.test", Password);
        var deniedClient = factory.CreateSecureClient();
        await SignInAsync(deniedClient, deniedUser.Email!);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await deniedClient.GetAsync($"/api/clients/{Guid.NewGuid()}/form-templates")).StatusCode);

        var actor = await factory.CreateUserAsync(
            $"forms-{Guid.NewGuid():N}@example.test", Password);
        await factory.GrantPermissionAsync(actor.Id, Permissions.ClientForm.View);
        var fixture = await CreateFixtureAsync(actor.Id, structuredPartyNames: true);
        var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        foreach (var suffix in new[]
                 {
                     string.Empty,
                     $"/{fixture.PublishedVersionId}/content",
                     $"/{fixture.PublishedVersionId}/bindings",
                 })
        {
            var route = $"/api/clients/{fixture.ClientId}/form-templates{suffix}";
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await anonymousClient.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await deniedClient.GetAsync(route)).StatusCode);
        }

        var list = await client.GetAsync($"/api/clients/{fixture.ClientId}/form-templates");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listJson = await list.Content.ReadAsStringAsync();
        Assert.Contains(fixture.PublishedVersionId.ToString(), listJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.DraftVersionId.ToString(), listJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.RetiredVersionId.ToString(), listJson, StringComparison.OrdinalIgnoreCase);

        var bindings = await client.GetAsync(
            $"/api/clients/{fixture.ClientId}/form-templates/{fixture.PublishedVersionId}/bindings");
        Assert.Equal(HttpStatusCode.OK, bindings.StatusCode);
        using var document = JsonDocument.Parse(await bindings.Content.ReadAsStringAsync());
        var fields = document.RootElement.GetProperty("fields").EnumerateArray()
            .ToDictionary(
                field => field.GetProperty("pdfFieldName").GetString()!,
                field => field.GetProperty("value").GetString()!);
        Assert.Equal("Morgan", fields["party-first"]);
        Assert.Equal("Rivera", fields["party-last"]);
        Assert.Equal("Morgan Rivera", fields["party-full"]);
        Assert.Equal("Jordan", fields["client-first"]);
        Assert.DoesNotContain("signature-field", fields.Keys);
        Assert.DoesNotContain("attestation-field", fields.Keys);
        Assert.DoesNotContain("ssn-field", fields.Keys);

        var content = await client.GetAsync(
            $"/api/clients/{fixture.ClientId}/form-templates/{fixture.PublishedVersionId}/content");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("no-store", content.Headers.CacheControl?.ToString());
        Assert.Contains("inline", content.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);
        Assert.Equal("nosniff", content.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("%PDF-test", await content.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(
                $"/api/clients/{fixture.ClientId}/form-templates/{fixture.DraftVersionId}/bindings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(
                $"/api/clients/{fixture.ClientId}/form-templates/{fixture.RetiredVersionId}/content")).StatusCode);

        await using var auditScope = factory.Services.CreateAsyncScope();
        var auditContext = auditScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var metadata = await auditContext.Set<AuditLog>().AsNoTracking()
            .Where(log => log.ActorUserId == actor.Id && log.ResourceType == "ClientForm")
            .Select(log => log.MetadataJson ?? string.Empty)
            .ToArrayAsync();
        Assert.NotEmpty(metadata);
        Assert.All(metadata, value =>
        {
            Assert.DoesNotContain("Morgan", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Jordan", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PMI-", value, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task LegacyResponsiblePartyNameIsNotParsedForStructuredBindings()
    {
        var actor = await factory.CreateUserAsync(
            $"forms-legacy-{Guid.NewGuid():N}@example.test", Password);
        await factory.GrantPermissionAsync(actor.Id, Permissions.ClientForm.View);
        var fixture = await CreateFixtureAsync(actor.Id, structuredPartyNames: false);
        var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        var response = await client.GetAsync(
            $"/api/clients/{fixture.ClientId}/form-templates/{fixture.PublishedVersionId}/bindings");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var fields = document.RootElement.GetProperty("fields").EnumerateArray()
            .ToDictionary(
                field => field.GetProperty("pdfFieldName").GetString()!,
                field => field.GetProperty("value").GetString()!);

        Assert.Equal(string.Empty, fields["party-first"]);
        Assert.Equal(string.Empty, fields["party-last"]);
        Assert.Equal("Morgan Rivera", fields["party-full"]);
    }

    [Fact]
    public async Task MissingResponsiblePartyResolvesBlankResponsiblePartyBindings()
    {
        var actor = await CreatePermittedActorAsync("missing-party");
        var fixture = await CreateFixtureAsync(actor.Id, structuredPartyNames: false, includeParty: false);
        var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        var fields = await ReadBindingsAsync(client, fixture.ClientId, fixture.PublishedVersionId);

        Assert.Equal(string.Empty, fields["party-first"]);
        Assert.Equal(string.Empty, fields["party-last"]);
        Assert.Equal(string.Empty, fields["party-full"]);
        Assert.Equal("Jordan", fields["client-first"]);
    }

    [Fact]
    public async Task CrossCompanyLocationScopedAndForgedResourcesAreIndistinguishableNotFound()
    {
        var actor = await CreatePermittedActorAsync("tenant-isolation");
        var fixture = await CreateFixtureAsync(actor.Id, structuredPartyNames: true);
        var inaccessibleLocationId = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        var locationScopedClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId, inaccessibleLocationId);
        var other = await CreateCompanyAsync();
        var crossCompanyClientId = await CreateClientAsync(other.CompanyId, other.LocationId);
        var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        foreach (var inaccessibleClientId in new[] { locationScopedClientId, crossCompanyClientId, Guid.NewGuid() })
        {
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync($"/api/clients/{inaccessibleClientId}/form-templates")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync(
                    $"/api/clients/{inaccessibleClientId}/form-templates/{fixture.PublishedVersionId}/content")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync(
                    $"/api/clients/{inaccessibleClientId}/form-templates/{fixture.PublishedVersionId}/bindings")).StatusCode);
        }

        var forgedVersionId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(
                $"/api/clients/{fixture.ClientId}/form-templates/{forgedVersionId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(
                $"/api/clients/{fixture.ClientId}/form-templates/{forgedVersionId}/bindings")).StatusCode);
    }

    [Fact]
    public async Task PlatformAndOwningCompanyTemplatesAreVisibleButOtherCompanyTemplateIsHidden()
    {
        var actor = await CreatePermittedActorAsync("template-visibility");
        var fixture = await CreateFixtureAsync(actor.Id, structuredPartyNames: true);
        var other = await CreateCompanyAsync();
        var ownVersionId = await AddPublishedTemplateAsync(
            actor.Id, FormTemplateOwnerType.Company, SqlServerApiFactory.DefaultCompanyId, "OWN");
        var otherVersionId = await AddPublishedTemplateAsync(
            actor.Id, FormTemplateOwnerType.Company, other.CompanyId, "OTHER");
        var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        var response = await client.GetAsync($"/api/clients/{fixture.ClientId}/form-templates");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains(fixture.PublishedVersionId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ownVersionId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(otherVersionId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(
                $"/api/clients/{fixture.ClientId}/form-templates/{otherVersionId}/bindings")).StatusCode);
    }

    [Fact]
    public async Task DevelopmentInitializerIsIdempotentAndSeededBindingsResolveExpectedValues()
    {
        var administrator = await factory.CreateUserAsync("admin@carecomply.local", Password);
        await factory.GrantPermissionAsync(administrator.Id, Permissions.ClientForm.View);
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..",
            "form-templates", "DHS_6893F.pdf"));
        Assert.True(File.Exists(sourcePath), $"Fixture PDF was not found at {sourcePath}.");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var initializer = new DevelopmentFormTemplateInitializer(
                scope.ServiceProvider.GetRequiredService<CareComplyDbContext>(),
                scope.ServiceProvider.GetRequiredService<IFormContentStore>(),
                Options.Create(new DevelopmentFormTemplateOptions
                {
                    Enabled = true,
                    SourcePdfPath = sourcePath,
                }),
                TimeProvider.System);
            await initializer.InitializeAsync();
            await initializer.InitializeAsync();
        }

        Guid clientId;
        Guid versionId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            Assert.Equal(1, await context.FormTemplates.CountAsync(item => item.Key == "DHS-6893F"));
            var version = await context.FormTemplateVersions.SingleAsync(item =>
                item.FormTemplateId == context.FormTemplates
                    .Where(template => template.Key == "DHS-6893F")
                    .Select(template => template.Id).Single());
            Assert.Equal(FormTemplateVersionStatus.Published, version.Status);
            Assert.Equal(35, await context.FormTemplateFieldMappings.CountAsync(item =>
                item.FormTemplateVersionId == version.Id));
            var seededClient = await context.Clients.SingleAsync(item => item.PmiNumber == "DEMO-CF-001");
            if (!await context.UserLocations.AnyAsync(item =>
                    item.UserId == administrator.Id && item.LocationId == seededClient.LocationId))
            {
                context.UserLocations.Add(new ApplicationUserLocation
                {
                    UserId = administrator.Id,
                    LocationId = seededClient.LocationId,
                });
                await context.SaveChangesAsync();
            }
            clientId = seededClient.Id;
            versionId = version.Id;
        }

        var client = factory.CreateSecureClient();
        await SignInAsync(client, administrator.Email!);
        var fields = await ReadBindingsAsync(client, clientId, versionId);
        Assert.Equal("Morgan", fields["form1[0].P1[0].sfRespParty[0].NameFirst[0]"]);
        Assert.Equal("Rivera", fields["form1[0].P1[0].sfRespParty[0].NameLast[0]"]);
        Assert.Equal("Jordan", fields["form1[0].P1[0].sfRespParty[0].NameFirst[1]"]);
        Assert.Equal("Rivera", fields["form1[0].P1[0].sfRespParty[0].NameLast[1]"]);
        Assert.Equal("L", fields["form1[0].P1[0].sfRespParty[0].NameMI[0]"]);
        Assert.Equal("DEMO-CF-001", fields["form1[0].P1[0].sfRespParty[0].PMI[0]"]);
    }

    [Fact]
    public async Task DevelopmentCommandRejectsNonDevelopmentEnvironment()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DevelopmentFormTemplateOptions.SectionName}:Enabled"] = "true",
            })
            .Build();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Staging };
        using var services = new ServiceCollection().BuildServiceProvider();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentFormTemplateCommand.TryRunAsync(
                services, environment, configuration,
                [DevelopmentFormTemplateCommand.Argument]));

        Assert.Contains("only in Development", exception.Message, StringComparison.Ordinal);
    }

    private async Task<FormFixture> CreateFixtureAsync(
        Guid actorId,
        bool structuredPartyNames,
        bool includeParty = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var locationId = await context.CompanyLocations
            .Where(location => location.CompanyId == SqlServerApiFactory.DefaultCompanyId)
            .OrderByDescending(location => location.IsMain)
            .Select(location => location.Id)
            .FirstAsync();
        if (!await context.UserLocations.AnyAsync(item =>
                item.UserId == actorId && item.LocationId == locationId))
            context.UserLocations.Add(new ApplicationUserLocation { UserId = actorId, LocationId = locationId });

        var now = DateTimeOffset.UtcNow;
        var clientId = Guid.CreateVersion7();
        var client = Client.Create(
            clientId, SqlServerApiFactory.DefaultCompanyId, "Jordan", "Lee", "Rivera", null,
            new DateOnly(1987, 4, 12), ClientGender.NonBinary, $"PMI-{Guid.NewGuid():N}",
            new DateOnly(2026, 1, 15), null, ClientStatus.Active, now);
        client.AssignLocation(locationId, "Main", now);
        context.Clients.Add(client);

        if (includeParty)
        {
            var party = new ClientContact(
                Guid.CreateVersion7(), clientId, ClientContactType.ResponsibleParty,
                "Morgan Rivera", "Parent", "(612) 555-0175", null,
                true, false, null, null, null);
            party.UpdateResponsibleParty(
                "Morgan Rivera", "Parent", "(612) 555-0175", null, null,
                false, false, true,
                structuredPartyNames ? "Morgan" : null,
                null,
                structuredPartyNames ? "Rivera" : null);
            context.ClientContacts.Add(party);
        }

        var templateId = Guid.CreateVersion7();
        var publishedId = Guid.CreateVersion7();
        var draftId = Guid.CreateVersion7();
        var retiredId = Guid.CreateVersion7();
        var objectKey = Guid.NewGuid().ToString("N");
        context.FormTemplates.Add(FormTemplate.Create(
            templateId, $"TEST-{Guid.NewGuid():N}", "Test form", "", "Test",
            FormTemplateOwnerType.Platform, null, actorId, now));
        var published = FormTemplateVersion.CreateDraft(
            publishedId, templateId, 1, "test.pdf", objectKey, "application/pdf", 9,
            CreateUniqueSha256(publishedId), 1, FormTechnology.AcroForm, null, null, actorId, now);
        published.Publish(actorId, now);
        context.FormTemplateVersions.Add(published);
        context.FormTemplateVersions.Add(FormTemplateVersion.CreateDraft(
            draftId, templateId, 2, "draft.pdf", Guid.NewGuid().ToString("N"), "application/pdf", 9,
            CreateUniqueSha256(draftId), 1, FormTechnology.AcroForm, null, null, actorId, now));
        var retired = FormTemplateVersion.CreateDraft(
            retiredId, templateId, 3, "retired.pdf", Guid.NewGuid().ToString("N"), "application/pdf", 9,
            CreateUniqueSha256(retiredId), 1, FormTechnology.AcroForm, null, null, actorId, now);
        retired.Publish(actorId, now);
        retired.Retire();
        context.FormTemplateVersions.Add(retired);

        AddMapping(context, publishedId, "party-first", ClientFormBindingKeys.ResponsiblePartyFirstName,
            FormFieldPurpose.Data, now);
        AddMapping(context, publishedId, "party-last", ClientFormBindingKeys.ResponsiblePartyLastName,
            FormFieldPurpose.Data, now);
        AddMapping(context, publishedId, "party-full", ClientFormBindingKeys.ResponsiblePartyFullName,
            FormFieldPurpose.Data, now);
        AddMapping(context, publishedId, "client-first", ClientFormBindingKeys.ClientFirstName,
            FormFieldPurpose.Data, now);
        AddMapping(context, publishedId, "signature-field", null, FormFieldPurpose.Signature, now);
        AddMapping(context, publishedId, "attestation-field", null, FormFieldPurpose.Attestation, now);
        AddMapping(context, publishedId, "ssn-field", "client.ssn", FormFieldPurpose.Data, now);
        await context.SaveChangesAsync();

        var store = scope.ServiceProvider.GetRequiredService<IFormContentStore>();
        await using var content = new MemoryStream("%PDF-test"u8.ToArray(), writable: false);
        await store.WriteAsync(objectKey, content, CancellationToken.None);
        return new(clientId, publishedId, draftId, retiredId);
    }

    private async Task<ApplicationUser> CreatePermittedActorAsync(string prefix)
    {
        var actor = await factory.CreateUserAsync(
            $"forms-{prefix}-{Guid.NewGuid():N}@example.test", Password);
        await factory.GrantPermissionAsync(actor.Id, Permissions.ClientForm.View);
        return actor;
    }

    private async Task<Guid> CreateLocationAsync(Guid companyId)
    {
        var id = Guid.CreateVersion7();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        context.CompanyLocations.Add(CompanyLocation.Create(
            id, companyId, $"Forms {id:N}", false, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        return id;
    }

    private async Task<(Guid CompanyId, Guid LocationId)> CreateCompanyAsync()
    {
        var companyId = Guid.CreateVersion7();
        var locationId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        context.Companies.Add(Company.Create(
            companyId, $"Forms Company {companyId:N}", $"F-{companyId:N}", now));
        context.CompanyLocations.Add(CompanyLocation.Create(locationId, companyId, "Main", true, now));
        await context.SaveChangesAsync();
        return (companyId, locationId);
    }

    private async Task<Guid> CreateClientAsync(Guid companyId, Guid locationId)
    {
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var client = Client.Create(
            id, companyId, "Scoped", null, "Client", null,
            new DateOnly(1990, 1, 1), ClientGender.Unknown, $"PMI-{Guid.NewGuid():N}",
            new DateOnly(2026, 1, 1), null, ClientStatus.Active, now);
        client.AssignLocation(locationId, "Scoped location", now);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> AddPublishedTemplateAsync(
        Guid actorId,
        FormTemplateOwnerType ownerType,
        Guid? companyId,
        string prefix)
    {
        var templateId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var version = FormTemplateVersion.CreateDraft(
            versionId, templateId, 1, $"{prefix}.pdf", Guid.NewGuid().ToString("N"),
            "application/pdf", 9, CreateUniqueSha256(versionId), 1,
            FormTechnology.AcroForm, null, null, actorId, now);
        version.Publish(actorId, now);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        context.FormTemplates.Add(FormTemplate.Create(
            templateId, $"{prefix}-{Guid.NewGuid():N}", $"{prefix} form", "", "Test",
            ownerType, companyId, actorId, now));
        context.FormTemplateVersions.Add(version);
        await context.SaveChangesAsync();
        return versionId;
    }

    private static async Task<Dictionary<string, string>> ReadBindingsAsync(
        HttpClient client,
        Guid clientId,
        Guid versionId)
    {
        var response = await client.GetAsync(
            $"/api/clients/{clientId}/form-templates/{versionId}/bindings");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("fields").EnumerateArray()
            .ToDictionary(
                field => field.GetProperty("pdfFieldName").GetString()!,
                field => field.GetProperty("value").GetString()!);
    }

    private static void AddMapping(
        CareComplyDbContext context,
        Guid versionId,
        string pdfName,
        string? bindingKey,
        FormFieldPurpose purpose,
        DateTimeOffset now) => context.FormTemplateFieldMappings.Add(
        FormTemplateFieldMapping.Create(
            Guid.CreateVersion7(), versionId, pdfName, FormFieldType.Text, purpose,
            bindingKey, null, true, now));

    private static string CreateUniqueSha256(Guid id)
    {
        var hexadecimalId = id.ToString("N");
        return hexadecimalId + hexadecimalId;
    }

    private static async Task SignInAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private sealed record FormFixture(
        Guid ClientId,
        Guid PublishedVersionId,
        Guid DraftVersionId,
        Guid RetiredVersionId);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "CareComply.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
