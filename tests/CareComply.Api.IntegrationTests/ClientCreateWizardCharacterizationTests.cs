using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using CareComply.Domain.Companies;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class ClientCreateWizardCharacterizationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task StepOneCreatesPendingClientFromBasicDetailsOnly()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var supervisor = await CreateEmployeeAsync(locationId, "Basic Supervisor");
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var pmi = UniquePmi();

        var response = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(pmi, locationId, supervisor.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var clientId = json.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, clientId);
        Assert.Equal(64, json.RootElement.GetProperty("editorVersion").GetString()!.Length);
        Assert.Equal("Pending", json.RootElement.GetProperty("careSetup").GetProperty("status").GetString());
        Assert.True(json.RootElement.GetProperty("identity").GetProperty("hasSsn").GetBoolean());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var client = await db.Clients.AsNoTracking().SingleAsync(x => x.Id == clientId);
        Assert.Equal(SqlServerApiFactory.DefaultCompanyId, client.CompanyId);
        Assert.Equal(locationId, client.LocationId);
        Assert.Equal(supervisor.Id, client.SupervisorUserId);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.NotEqual("123-45-6789", client.ProtectedSsn);
        Assert.False(await db.ClientAdditionalProfiles.AnyAsync(x => x.ClientId == clientId));
        Assert.False(await db.ClientLanguagePreferences.AnyAsync(x => x.ClientId == clientId));
        Assert.Single(await db.ClientServices.Where(x => x.ClientId == clientId).ToArrayAsync());
        var audit = await db.Set<AuditLog>().AsNoTracking().SingleAsync(x =>
            x.ActorUserId == actor.Id && x.Action == "Client.Created" && x.ResourceId == clientId.ToString("N"));
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal("{\"serviceCount\":\"1\",\"status\":\"Pending\"}", audit.MetadataJson);
    }

    [Fact]
    public async Task StepOneRejectsInjectedCompanyAndCrossCompanyLocationWithoutPersistingClient()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create);
        var otherCompany = await CreateCompanyAsync();
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var pmi = UniquePmi();

        var response = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(pmi, otherCompany.LocationId, companyId: otherCompany.CompanyId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.False(await db.Clients.AnyAsync(x => x.PmiNumber == pmi));
        Assert.Single(await db.Set<AuditLog>().Where(x =>
            x.ActorUserId == actor.Id && x.Action == "Client.Created" && x.Outcome == AuditOutcome.Failed).ToArrayAsync());
    }

    [Fact]
    public async Task PendingBasicDetailsUpdateIsFocusedConcurrentAndPreservesProtectedAndLaterStepData()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create, Permissions.Client.Edit);
        var firstLocation = await MainLocationIdAsync();
        var secondLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        await GiveLocationAccessAsync(actor.Id, firstLocation);
        await GiveLocationAccessAsync(actor.Id, secondLocation);
        var supervisor = await CreateEmployeeAsync(secondLocation, "Focused Supervisor");
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var create = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(UniquePmi(), firstLocation));
        create.EnsureSuccessStatusCode();
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var clientId = created.RootElement.GetProperty("id").GetGuid();
        var version = created.RootElement.GetProperty("editorVersion").GetString()!;

        var additional = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/additional-information",
            AdditionalInformationRequest(version, "Must remain"));
        using var additionalJson = JsonDocument.Parse(await additional.Content.ReadAsStringAsync());
        version = additionalJson.RootElement.GetProperty("editorVersion").GetString()!;
        var contact = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/contact-information",
            ContactInformationRequest(version));
        using var contactJson = JsonDocument.Parse(await contact.Content.ReadAsStringAsync());
        version = contactJson.RootElement.GetProperty("editorVersion").GetString()!;

        string protectedSsn;
        Guid companyId;
        await using (var beforeScope = factory.Services.CreateAsyncScope())
        {
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var before = await beforeDb.Clients.AsNoTracking().SingleAsync(x => x.Id == clientId);
            protectedSsn = before.ProtectedSsn;
            companyId = before.CompanyId;
        }

        var newPmi = UniquePmi();
        var update = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/basic-details",
            BasicDetailsUpdateRequest(version, newPmi, secondLocation, supervisor.Id, ssn: ""));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var updated = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        Assert.Equal(clientId, updated.RootElement.GetProperty("id").GetGuid());
        var updatedVersion = updated.RootElement.GetProperty("editorVersion").GetString()!;
        Assert.NotEqual(version, updatedVersion);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var client = await db.Clients.AsNoTracking().SingleAsync(x => x.Id == clientId);
        Assert.Equal(companyId, client.CompanyId);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.Equal("Updated", client.FirstName);
        Assert.Equal(new DateOnly(1988, 2, 3), client.DateOfBirth);
        Assert.Equal(ClientGender.Male, client.Gender);
        Assert.Equal(newPmi, client.PmiNumber);
        Assert.Equal(secondLocation, client.LocationId);
        Assert.Equal(supervisor.Id, client.SupervisorUserId);
        Assert.Equal(protectedSsn, client.ProtectedSsn);
        Assert.Equal(4, client.CreationProgressStep);
        Assert.Equal("client@example.test", client.Email);
        Assert.Equal("(612) 555-0111", client.PrimaryPhone);
        Assert.Equal("100 Main St", client.AddressLine1);
        Assert.Equal("Must remain", (await db.ClientAdditionalProfiles.AsNoTracking()
            .SingleAsync(x => x.ClientId == clientId)).Notes);
        Assert.Equal(2, await db.ClientLanguagePreferences.CountAsync(x => x.ClientId == clientId));
        Assert.Equal("Updated service", (await db.ClientServices.AsNoTracking()
            .SingleAsync(x => x.ClientId == clientId && x.IsActive)).Name);

        var audit = await db.Set<AuditLog>().AsNoTracking()
            .Where(x => x.ActorUserId == actor.Id && x.Action == "Client.Updated"
                && x.ResourceId == clientId.ToString("N"))
            .OrderByDescending(x => x.OccurredAtUtc)
            .FirstAsync();
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal("{\"changedSectionCount\":\"2\",\"changedSections\":\"BasicDetails,Services\"}", audit.MetadataJson);
        Assert.DoesNotContain(newPmi, audit.MetadataJson ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("Updated", audit.MetadataJson ?? string.Empty, StringComparison.Ordinal);

        var replacement = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/basic-details",
            BasicDetailsUpdateRequest(updatedVersion, newPmi, secondLocation, supervisor.Id, "321-54-9876"));
        Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);
        using var replacementJson = JsonDocument.Parse(await replacement.Content.ReadAsStringAsync());
        var replacementVersion = replacementJson.RootElement.GetProperty("editorVersion").GetString()!;
        var afterReplacement = await db.Clients.AsNoTracking().SingleAsync(x => x.Id == clientId);
        Assert.NotEqual(protectedSsn, afterReplacement.ProtectedSsn);
        Assert.NotEqual("321-54-9876", afterReplacement.ProtectedSsn);

        var stale = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/basic-details",
            BasicDetailsUpdateRequest(updatedVersion, UniquePmi(), secondLocation, supervisor.Id, "111-22-3333"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var afterStale = await db.Clients.AsNoTracking().SingleAsync(x => x.Id == clientId);
        Assert.Equal(newPmi, afterStale.PmiNumber);
        Assert.Equal(afterReplacement.ProtectedSsn, afterStale.ProtectedSsn);
        Assert.NotEqual(updatedVersion, replacementVersion);
    }

    [Fact]
    public async Task BasicDetailsUpdateRequiresEditPermission()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var create = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(UniquePmi(), locationId));
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());

        var forbidden = await http.PutAsJsonAsync(
            $"/api/clients/{created.RootElement.GetProperty("id").GetGuid()}/basic-details",
            BasicDetailsUpdateRequest(
                created.RootElement.GetProperty("editorVersion").GetString()!,
                UniquePmi(),
                locationId));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task BasicDetailsDuplicatePmiRollsBackAndNewLocationMustBeAccessible()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create, Permissions.Client.Edit);
        var allowedLocation = await MainLocationIdAsync();
        var inaccessibleLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        await GiveLocationAccessAsync(actor.Id, allowedLocation);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var originalPmi = UniquePmi();
        var duplicatePmi = UniquePmi();
        var first = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(originalPmi, allowedLocation));
        var second = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(duplicatePmi, allowedLocation));
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        var clientId = firstJson.RootElement.GetProperty("id").GetGuid();
        var version = firstJson.RootElement.GetProperty("editorVersion").GetString()!;
        Assert.NotEqual(clientId, secondJson.RootElement.GetProperty("id").GetGuid());

        var duplicate = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/basic-details",
            BasicDetailsUpdateRequest(version, duplicatePmi, allowedLocation));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var inaccessible = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/basic-details",
            BasicDetailsUpdateRequest(version, UniquePmi(), inaccessibleLocation));
        Assert.Equal(HttpStatusCode.NotFound, inaccessible.StatusCode);

        var otherCompany = await CreateCompanyAsync();
        var otherClient = await CreatePendingClientAsync(otherCompany.CompanyId, otherCompany.LocationId);
        var crossCompany = await http.PutAsJsonAsync(
            $"/api/clients/{otherClient.Id}/basic-details",
            BasicDetailsUpdateRequest(
                otherClient.EditorVersion,
                UniquePmi(),
                otherCompany.LocationId));
        Assert.Equal(HttpStatusCode.NotFound, crossCompany.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var unchanged = await db.Clients.AsNoTracking().SingleAsync(x => x.Id == clientId);
        Assert.Equal(originalPmi, unchanged.PmiNumber);
        Assert.Equal("Step", unchanged.FirstName);
        Assert.Equal(allowedLocation, unchanged.LocationId);
    }

    [Theory]
    [InlineData(ClientStatus.Active)]
    [InlineData(ClientStatus.OnHold)]
    [InlineData(ClientStatus.Discharged)]
    [InlineData(ClientStatus.Inactive)]
    public async Task BasicDetailsUpdateRejectsEveryNonPendingStatus(ClientStatus status)
    {
        var actor = await CreateActorAsync(Permissions.Client.Edit);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var clientId = await CreateClientWithStatusAsync(
            SqlServerApiFactory.DefaultCompanyId,
            locationId,
            status);
        string version;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var rowVersion = await db.Clients.AsNoTracking().Where(x => x.Id == clientId)
                .Select(x => x.RowVersion).SingleAsync();
            version = Convert.ToHexString(SHA256.HashData(rowVersion));
        }
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var response = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/basic-details",
            BasicDetailsUpdateRequest(version, UniquePmi(), locationId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task StepTwoCreatesThenUpdatesSingleProfileAndPreservesCoreAndContactData()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create, Permissions.Client.Edit);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var create = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(UniquePmi(), locationId));
        create.EnsureSuccessStatusCode();
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var clientId = created.RootElement.GetProperty("id").GetGuid();
        var firstVersion = created.RootElement.GetProperty("editorVersion").GetString()!;

        var update = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/additional-information",
            AdditionalInformationRequest(firstVersion, "First notes"));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var updated = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        var secondVersion = updated.RootElement.GetProperty("editorVersion").GetString()!;
        Assert.NotEqual(firstVersion, secondVersion);

        var secondUpdate = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/additional-information",
            AdditionalInformationRequest(secondVersion, "Updated notes"));
        Assert.Equal(HttpStatusCode.OK, secondUpdate.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var client = await db.Clients.AsNoTracking().SingleAsync(x => x.Id == clientId);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.Equal("Step", client.FirstName);
        Assert.Equal(string.Empty, client.Email);
        Assert.Equal(SqlServerApiFactory.DefaultCompanyId, client.CompanyId);
        var profile = await db.ClientAdditionalProfiles.AsNoTracking().SingleAsync(x => x.ClientId == clientId);
        Assert.Equal("Updated notes", profile.Notes);
        Assert.Single(await db.ClientAdditionalProfiles.Where(x => x.ClientId == clientId).ToArrayAsync());
        var audits = await db.Set<AuditLog>().AsNoTracking()
            .Where(x => x.ActorUserId == actor.Id && x.Action == "Client.Updated" && x.ResourceId == clientId.ToString("N"))
            .ToArrayAsync();
        Assert.Equal(2, audits.Length);
        Assert.All(audits, audit =>
        {
            Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
            Assert.DoesNotContain("notes", audit.MetadataJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("First notes", audit.MetadataJson ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("Updated notes", audit.MetadataJson ?? string.Empty, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task StepTwoRejectsStaleVersionWithoutPartialMutationAndEmitsFailedAudit()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create, Permissions.Client.Edit);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var create = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(UniquePmi(), locationId));
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var clientId = created.RootElement.GetProperty("id").GetGuid();
        var version = created.RootElement.GetProperty("editorVersion").GetString()!;
        var first = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/additional-information",
            AdditionalInformationRequest(version, "Committed"));
        first.EnsureSuccessStatusCode();

        var stale = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/additional-information",
            AdditionalInformationRequest(version, "Must not persist"));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal("Committed", (await db.ClientAdditionalProfiles.AsNoTracking()
            .SingleAsync(x => x.ClientId == clientId)).Notes);
        var failedAudit = await db.Set<AuditLog>().AsNoTracking().SingleAsync(x =>
            x.ActorUserId == actor.Id && x.Action == "Client.Updated" && x.Outcome == AuditOutcome.Failed);
        Assert.Equal("{\"reasonCode\":\"Conflict\"}", failedAudit.MetadataJson);
    }

    [Fact]
    public async Task StepTwoRequiresEditPermission()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var create = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(UniquePmi(), locationId));
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());

        var response = await http.PutAsJsonAsync(
            $"/api/clients/{created.RootElement.GetProperty("id").GetGuid()}/additional-information",
            AdditionalInformationRequest(created.RootElement.GetProperty("editorVersion").GetString()!, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StepThreePersistsContactAndSynchronizesLanguagesWithoutChangingEarlierSteps()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create, Permissions.Client.Edit);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var create = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(UniquePmi(), locationId));
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var clientId = created.RootElement.GetProperty("id").GetGuid();
        var version = created.RootElement.GetProperty("editorVersion").GetString()!;
        var additional = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/additional-information",
            AdditionalInformationRequest(version, "Preserve profile"));
        using var additionalJson = JsonDocument.Parse(await additional.Content.ReadAsStringAsync());
        version = additionalJson.RootElement.GetProperty("editorVersion").GetString()!;

        var contact = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/contact-information",
            ContactInformationRequest(version));

        Assert.Equal(HttpStatusCode.OK, contact.StatusCode);
        using var response = JsonDocument.Parse(await contact.Content.ReadAsStringAsync());
        Assert.NotEqual(version, response.RootElement.GetProperty("editorVersion").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var client = await db.Clients.AsNoTracking().SingleAsync(x => x.Id == clientId);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.Equal("Step", client.FirstName);
        Assert.Equal(SqlServerApiFactory.DefaultCompanyId, client.CompanyId);
        Assert.Equal("client@example.test", client.Email);
        Assert.Equal("(612) 555-0111", client.PrimaryPhone);
        Assert.Equal("100 Main St", client.AddressLine1);
        Assert.Equal("Preserve profile", (await db.ClientAdditionalProfiles.AsNoTracking()
            .SingleAsync(x => x.ClientId == clientId)).Notes);
        var languages = await db.ClientLanguagePreferences.AsNoTracking()
            .Where(x => x.ClientId == clientId).OrderByDescending(x => x.IsPrimary).ToArrayAsync();
        Assert.Equal(2, languages.Length);
        Assert.Equal("en", languages[0].LanguageCode);
        Assert.True(languages[0].IsPrimary);
        var audit = await db.Set<AuditLog>().AsNoTracking().SingleAsync(x =>
            x.ActorUserId == actor.Id && x.Action == "Client.Updated"
            && x.ResourceId == clientId.ToString("N")
            && x.MetadataJson!.Contains("ContactInformation"));
        Assert.DoesNotContain("client@example.test", audit.MetadataJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("612", audit.MetadataJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("English", audit.MetadataJson!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompletionAppliesAuthenticatedSelectedStatusAndPreservesPersistedWizardData()
    {
        var actor = await CreateActorAsync(
            Permissions.Client.Create,
            Permissions.Client.Edit,
            Permissions.ClientHospitalization.Create);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var create = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(UniquePmi(), locationId));
        create.EnsureSuccessStatusCode();
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var clientId = created.RootElement.GetProperty("id").GetGuid();
        var version = created.RootElement.GetProperty("editorVersion").GetString()!;

        var additional = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/additional-information",
            AdditionalInformationRequest(version, "Completion profile"));
        additional.EnsureSuccessStatusCode();
        using var additionalJson = JsonDocument.Parse(await additional.Content.ReadAsStringAsync());
        version = additionalJson.RootElement.GetProperty("editorVersion").GetString()!;
        var contact = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/contact-information",
            ContactInformationRequest(version));
        contact.EnsureSuccessStatusCode();
        using var contactJson = JsonDocument.Parse(await contact.Content.ReadAsStringAsync());
        version = contactJson.RootElement.GetProperty("editorVersion").GetString()!;

        var hospitalization = await http.PostAsJsonAsync($"/api/clients/{clientId}/hospitalizations", new
        {
            hospitalName = "Preserved Hospital",
            admittedAtUtc = "2026-09-20T10:00:00Z",
            dischargedAtUtc = (string?)null,
            reason = "Preserved reason",
            diagnosis = "Preserved diagnosis",
            dischargeInstructions = "Preserved instructions",
        });
        hospitalization.EnsureSuccessStatusCode();

        var complete = await http.PostAsJsonAsync($"/api/clients/{clientId}/complete-creation", new
        {
            editorVersion = version,
            status = (int)ClientStatus.Discharged,
            dischargeDate = "2026-10-01",
            dischargeReason = "Care completed",
            companyId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        using var completedJson = JsonDocument.Parse(await complete.Content.ReadAsStringAsync());
        Assert.Equal(clientId, completedJson.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Discharged", completedJson.RootElement.GetProperty("status").GetString());
        var completedVersion = completedJson.RootElement.GetProperty("editorVersion").GetString()!;
        Assert.Equal(64, completedVersion.Length);
        Assert.NotEqual(version, completedVersion);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await http.GetAsync($"/api/clients/{clientId}/creation-state")).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var client = await db.Clients.AsNoTracking().SingleAsync(item => item.Id == clientId);
        Assert.Equal(ClientStatus.Discharged, client.Status);
        Assert.Equal(new DateOnly(2026, 10, 1), client.DischargeDate);
        Assert.Equal("Care completed", client.DischargeReason);
        Assert.Equal(SqlServerApiFactory.DefaultCompanyId, client.CompanyId);
        Assert.Equal(locationId, client.LocationId);
        Assert.Equal("Step", client.FirstName);
        Assert.Equal("client@example.test", client.Email);
        Assert.Equal("100 Main St", client.AddressLine1);
        Assert.Equal("Completion profile", (await db.ClientAdditionalProfiles.AsNoTracking()
            .SingleAsync(item => item.ClientId == clientId)).Notes);
        Assert.Equal(2, await db.ClientLanguagePreferences.CountAsync(item => item.ClientId == clientId));
        Assert.Single(await db.ClientHospitalizations.Where(item => item.ClientId == clientId).ToArrayAsync());
        var audit = await db.Set<AuditLog>().AsNoTracking().SingleAsync(log =>
            log.ActorUserId == actor.Id
            && log.Action == "Client.StatusChanged"
            && log.ResourceId == clientId.ToString("N")
            && log.Outcome == AuditOutcome.Succeeded);
        Assert.Equal(
            "{\"changed\":\"true\",\"newStatus\":\"Discharged\",\"previousStatus\":\"Pending\"}",
            audit.MetadataJson);
        Assert.DoesNotContain("Step", audit.MetadataJson!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client@example.test", audit.MetadataJson!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Preserved", audit.MetadataJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompletionRequiresNoRelatedRecordsButRejectsStaleAndRepeatedRequests()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create, Permissions.Client.Edit);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var create = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(UniquePmi(), locationId));
        create.EnsureSuccessStatusCode();
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var clientId = created.RootElement.GetProperty("id").GetGuid();
        var staleVersion = created.RootElement.GetProperty("editorVersion").GetString()!;
        var additional = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/additional-information",
            AdditionalInformationRequest(staleVersion, null));
        additional.EnsureSuccessStatusCode();
        using var additionalJson = JsonDocument.Parse(await additional.Content.ReadAsStringAsync());
        var currentVersion = additionalJson.RootElement.GetProperty("editorVersion").GetString()!;

        var stale = await http.PostAsJsonAsync(
            $"/api/clients/{clientId}/complete-creation",
            new { editorVersion = staleVersion });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var complete = await http.PostAsJsonAsync(
            $"/api/clients/{clientId}/complete-creation",
            new { editorVersion = currentVersion });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        using var completed = JsonDocument.Parse(await complete.Content.ReadAsStringAsync());

        var repeated = await http.PostAsJsonAsync(
            $"/api/clients/{clientId}/complete-creation",
            new { editorVersion = completed.RootElement.GetProperty("editorVersion").GetString() });
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal(ClientStatus.Active, (await db.Clients.AsNoTracking()
            .SingleAsync(item => item.Id == clientId)).Status);
        Assert.False(await db.ClientCaregivers.AnyAsync(item => item.ClientId == clientId));
        Assert.False(await db.ClientContacts.AnyAsync(item => item.ClientId == clientId));
        Assert.False(await db.ClientHospitalizations.AnyAsync(item => item.ClientId == clientId));
        var failedAudits = await db.Set<AuditLog>().AsNoTracking().Where(log =>
            log.ActorUserId == actor.Id
            && log.Action == "Client.StatusChanged"
            && log.ResourceId == clientId.ToString("N")
            && log.Outcome == AuditOutcome.Failed).ToArrayAsync();
        Assert.Equal(2, failedAudits.Length);
        Assert.Contains(failedAudits, audit => audit.MetadataJson == "{\"reasonCode\":\"Conflict\"}");
        Assert.Contains(failedAudits, audit => audit.MetadataJson == "{\"reasonCode\":\"InvalidState\"}");
    }

    [Fact]
    public async Task CompletionRequiresEditPermissionAndTrustedCompanyLocationAccess()
    {
        var locationId = await MainLocationIdAsync();
        var creator = await CreateActorAsync(Permissions.Client.Create);
        await GiveLocationAccessAsync(creator.Id, locationId);
        using var creatorHttp = factory.CreateSecureClient();
        await SignInAsync(creatorHttp, creator.Email!);
        var create = await creatorHttp.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(UniquePmi(), locationId));
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var clientId = created.RootElement.GetProperty("id").GetGuid();
        var editorVersion = created.RootElement.GetProperty("editorVersion").GetString()!;

        var forbidden = await creatorHttp.PostAsJsonAsync(
            $"/api/clients/{clientId}/complete-creation",
            new { editorVersion });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var actor = await CreateActorAsync(Permissions.Client.Edit);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var inaccessibleLocation = await http.PostAsJsonAsync(
            $"/api/clients/{clientId}/complete-creation",
            new { editorVersion });
        Assert.Equal(HttpStatusCode.NotFound, inaccessibleLocation.StatusCode);

        var otherCompany = await CreateCompanyAsync();
        var otherClient = await CreatePendingClientAsync(otherCompany.CompanyId, otherCompany.LocationId);
        var crossCompany = await http.PostAsJsonAsync(
            $"/api/clients/{otherClient.Id}/complete-creation",
            new { editorVersion = otherClient.EditorVersion });
        Assert.Equal(HttpStatusCode.NotFound, crossCompany.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal(ClientStatus.Pending, (await db.Clients.AsNoTracking()
            .SingleAsync(item => item.Id == clientId)).Status);
        Assert.Equal(ClientStatus.Pending, (await db.Clients.AsNoTracking()
            .SingleAsync(item => item.Id == otherClient.Id)).Status);
    }

    [Fact]
    public async Task CreationStateHydratesPersistedStepsAndProgressAdvancesMonotonically()
    {
        var actor = await CreateActorAsync(
            Permissions.Client.Create,
            Permissions.Client.Edit,
            Permissions.ClientHospitalization.Create);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var create = await http.PostAsJsonAsync(
            "/api/clients/basic-details",
            ValidBasicDetailsRequest(UniquePmi(), locationId));
        create.EnsureSuccessStatusCode();
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var clientId = created.RootElement.GetProperty("id").GetGuid();
        var version = created.RootElement.GetProperty("editorVersion").GetString()!;

        var afterStepOne = await http.GetFromJsonAsync<JsonElement>(
            $"/api/clients/{clientId}/creation-state");
        Assert.Equal(2, afterStepOne.GetProperty("currentStep").GetInt32());
        Assert.Equal("Step", afterStepOne.GetProperty("identity").GetProperty("firstName").GetString());
        Assert.True(afterStepOne.GetProperty("identity").GetProperty("hasSsn").GetBoolean());
        Assert.False(afterStepOne.GetProperty("identity").TryGetProperty("ssn", out _));
        Assert.False(afterStepOne.TryGetProperty("companyId", out _));
        Assert.Equal(version, afterStepOne.GetProperty("editorVersion").GetString());

        var additional = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/additional-information",
            AdditionalInformationRequest(version, "Resume notes"));
        additional.EnsureSuccessStatusCode();
        using var additionalJson = JsonDocument.Parse(await additional.Content.ReadAsStringAsync());
        version = additionalJson.RootElement.GetProperty("editorVersion").GetString()!;
        var afterStepTwo = await http.GetFromJsonAsync<JsonElement>(
            $"/api/clients/{clientId}/creation-state");
        Assert.Equal(3, afterStepTwo.GetProperty("currentStep").GetInt32());
        Assert.Equal("Resume notes", afterStepTwo.GetProperty("additionalProfile")
            .GetProperty("notes").GetString());

        var contact = await http.PutAsJsonAsync(
            $"/api/clients/{clientId}/contact-information",
            ContactInformationRequest(version));
        contact.EnsureSuccessStatusCode();
        using var contactJson = JsonDocument.Parse(await contact.Content.ReadAsStringAsync());
        version = contactJson.RootElement.GetProperty("editorVersion").GetString()!;
        var afterStepThree = await http.GetFromJsonAsync<JsonElement>(
            $"/api/clients/{clientId}/creation-state");
        Assert.Equal(4, afterStepThree.GetProperty("currentStep").GetInt32());
        Assert.Equal("client@example.test", afterStepThree.GetProperty("contact")
            .GetProperty("email").GetString());
        Assert.Equal(2, afterStepThree.GetProperty("languages").GetArrayLength());

        var hospitalization = await http.PostAsJsonAsync($"/api/clients/{clientId}/hospitalizations", new
        {
            hospitalName = "Resume Hospital",
            admittedAtUtc = "2026-09-20T10:00:00Z",
            dischargedAtUtc = (string?)null,
            reason = "Observation",
        });
        hospitalization.EnsureSuccessStatusCode();
        Assert.Equal(4, (await http.GetFromJsonAsync<JsonElement>(
            $"/api/clients/{clientId}/creation-state")).GetProperty("currentStep").GetInt32());

        var advance = await http.PostAsJsonAsync($"/api/clients/{clientId}/creation-progress", new
        {
            step = 5,
            editorVersion = version,
            companyId = Guid.NewGuid(),
        });
        advance.EnsureSuccessStatusCode();
        using var advanced = JsonDocument.Parse(await advance.Content.ReadAsStringAsync());
        version = advanced.RootElement.GetProperty("editorVersion").GetString()!;
        Assert.Equal(5, advanced.RootElement.GetProperty("currentStep").GetInt32());

        var backward = await http.PostAsJsonAsync($"/api/clients/{clientId}/creation-progress", new
        {
            step = 4,
            editorVersion = version,
        });
        backward.EnsureSuccessStatusCode();
        using var unchanged = JsonDocument.Parse(await backward.Content.ReadAsStringAsync());
        Assert.Equal(5, unchanged.RootElement.GetProperty("currentStep").GetInt32());
        Assert.Equal(version, unchanged.RootElement.GetProperty("editorVersion").GetString());

        var skipped = await http.PostAsJsonAsync($"/api/clients/{clientId}/creation-progress", new
        {
            step = 7,
            editorVersion = version,
        });
        Assert.Equal(HttpStatusCode.Conflict, skipped.StatusCode);
        var invalid = await http.PostAsJsonAsync($"/api/clients/{clientId}/creation-progress", new
        {
            step = 11,
            editorVersion = version,
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal(5, (await db.Clients.AsNoTracking().SingleAsync(item => item.Id == clientId))
            .CreationProgressStep);
        Assert.False(await db.Set<AuditLog>().AnyAsync(log =>
            log.ActorUserId == actor.Id && log.Action.Contains("Progress")));
    }

    [Fact]
    public async Task CreationStateIsTenantScopedAndRejectsEveryNonPendingStatus()
    {
        var actor = await CreateActorAsync(Permissions.Client.Edit);
        var allowedLocation = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, allowedLocation);
        var hiddenLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        var hidden = await CreateClientWithStatusAsync(
            SqlServerApiFactory.DefaultCompanyId,
            hiddenLocation,
            ClientStatus.Pending);
        var otherCompany = await CreateCompanyAsync();
        var other = await CreateClientWithStatusAsync(
            otherCompany.CompanyId,
            otherCompany.LocationId,
            ClientStatus.Pending);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        Assert.Equal(HttpStatusCode.NotFound,
            (await http.GetAsync($"/api/clients/{hidden}/creation-state")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await http.GetAsync($"/api/clients/{other}/creation-state")).StatusCode);

        foreach (var status in new[]
                 {
                     ClientStatus.Active,
                     ClientStatus.OnHold,
                     ClientStatus.Discharged,
                     ClientStatus.Inactive,
                 })
        {
            var clientId = await CreateClientWithStatusAsync(
                SqlServerApiFactory.DefaultCompanyId,
                allowedLocation,
                status);
            Assert.Equal(HttpStatusCode.Conflict,
                (await http.GetAsync($"/api/clients/{clientId}/creation-state")).StatusCode);
        }
    }

    [Fact]
    public async Task StepFourCreatesCoreAggregateAndReturnsIdAndEditorVersion()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var supervisor = await CreateEmployeeAsync(locationId, "Step Four Supervisor");
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var pmi = UniquePmi();
        var request = ValidClientRequest(pmi, locationId, supervisor.Id);

        var response = await http.PostAsJsonAsync("/api/clients", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var clientId = json.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, clientId);
        Assert.Equal(64, json.RootElement.GetProperty("editorVersion").GetString()!.Length);
        Assert.Equal("Active", json.RootElement.GetProperty("careSetup").GetProperty("status").GetString());
        Assert.Equal(locationId, json.RootElement.GetProperty("careSetup").GetProperty("locationId").GetGuid());
        Assert.Equal(supervisor.Id, json.RootElement.GetProperty("careSetup").GetProperty("supervisorUserId").GetGuid());
        Assert.True(json.RootElement.GetProperty("identity").GetProperty("hasSsn").GetBoolean());
        Assert.False(json.RootElement.GetProperty("identity").TryGetProperty("ssn", out _));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await db.Clients.AsNoTracking().SingleAsync(x => x.Id == clientId);
        Assert.Equal(SqlServerApiFactory.DefaultCompanyId, persisted.CompanyId);
        Assert.Equal(locationId, persisted.LocationId);
        Assert.Equal(ClientStatus.Active, persisted.Status);
        Assert.NotEqual("123-45-6789", persisted.ProtectedSsn);
        Assert.NotEmpty(persisted.ProtectedSsn);

        var profile = await db.ClientAdditionalProfiles.AsNoTracking().SingleAsync(x => x.ClientId == clientId);
        Assert.Equal(ClientProgramType.Two45D, profile.ProgramType);
        Assert.Equal("Characterization notes", profile.Notes);
        var services = await db.ClientServices.AsNoTracking().Where(x => x.ClientId == clientId).ToArrayAsync();
        Assert.Collection(services, service =>
        {
            Assert.Equal("245D", service.Code);
            Assert.Equal(new DateOnly(2026, 9, 1), service.StartDate);
            Assert.True(service.IsActive);
        });
        var languages = await db.ClientLanguagePreferences.AsNoTracking()
            .Where(x => x.ClientId == clientId).OrderByDescending(x => x.IsPrimary).ToArrayAsync();
        Assert.Equal(2, languages.Length);
        Assert.Equal("en", languages[0].LanguageCode);
        Assert.True(languages[0].IsPrimary);
        Assert.False(languages[1].IsPrimary);

        var audit = await db.Set<AuditLog>().AsNoTracking().SingleAsync(x =>
            x.ActorUserId == actor.Id && x.Action == "Client.Created" && x.ResourceId == clientId.ToString("N"));
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal("Client", audit.ResourceType);
        Assert.Equal("{\"languageCount\":\"2\",\"serviceCount\":\"1\",\"status\":\"Active\"}", audit.MetadataJson);
        Assert.DoesNotContain("123-45-6789", audit.MetadataJson ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CrossCompanyLocationIsRejectedAndCoreAggregateRollsBack()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create);
        var otherCompany = await CreateCompanyAsync();
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var pmi = UniquePmi();

        var response = await http.PostAsJsonAsync(
            "/api/clients",
            ValidClientRequest(pmi, otherCompany.LocationId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.False(await db.Clients.AnyAsync(x => x.PmiNumber == pmi));
        var audit = await db.Set<AuditLog>().AsNoTracking().SingleAsync(x =>
            x.ActorUserId == actor.Id && x.Action == "Client.Created" && x.Outcome == AuditOutcome.Failed);
        Assert.Equal("not-created", audit.ResourceId);
        Assert.Equal("{\"reasonCode\":\"InvalidState\"}", audit.MetadataJson);
    }

    [Fact]
    public async Task SupervisorMustBeAnActiveEmployeeAtTheSelectedCompanyLocation()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create);
        var selectedLocation = await MainLocationIdAsync();
        var otherLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        var wrongLocationSupervisor = await CreateEmployeeAsync(otherLocation, "Wrong Location");
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var response = await http.PostAsJsonAsync(
            "/api/clients",
            ValidClientRequest(UniquePmi(), selectedLocation, wrongLocationSupervisor.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("125-80asdas")]
    [InlineData("123-45-678")]
    [InlineData("1234567890")]
    [InlineData("abc123456")]
    public async Task StepOneRejectsMalformedClientSsnWithoutPersistingClient(string ssn)
    {
        var actor = await CreateActorAsync(Permissions.Client.Create);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var pmi = UniquePmi();
        var request = JsonSerializer.SerializeToNode(
            ValidBasicDetailsRequest(pmi, locationId))!.AsObject();
        request["identity"]!["ssn"] = ssn;

        var response = await http.PostAsJsonAsync("/api/clients/basic-details", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.False(await db.Clients.AnyAsync(client => client.PmiNumber == pmi));
    }

    [Fact]
    public async Task AuthenticatedEditPersistsLocationAndSupervisorAndReturnsAuthoritativeNames()
    {
        var actor = await CreateActorAsync(
            Permissions.Client.Create,
            Permissions.Client.Edit,
            Permissions.Client.View);
        var firstLocation = await MainLocationIdAsync();
        var secondLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        await GiveLocationAccessAsync(actor.Id, firstLocation);
        await GiveLocationAccessAsync(actor.Id, secondLocation);
        var firstSupervisor = await CreateEmployeeAsync(firstLocation, "First Supervisor");
        var secondSupervisor = await CreateEmployeeAsync(secondLocation, "Second Supervisor");
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var create = await http.PostAsJsonAsync(
            "/api/clients",
            ValidClientRequest(UniquePmi(), firstLocation, firstSupervisor.Id));
        create.EnsureSuccessStatusCode();
        var request = JsonNode.Parse(await create.Content.ReadAsStringAsync())!.AsObject();
        var clientId = request["id"]!.GetValue<Guid>();
        request["careSetup"]!["location"] = $"Location {secondLocation:N}";
        request["careSetup"]!["locationId"] = secondLocation;
        request["careSetup"]!["supervisorUserId"] = secondSupervisor.Id;

        var update = await http.PutAsJsonAsync($"/api/clients/{clientId}", request);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var updated = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        var careSetup = updated.RootElement.GetProperty("careSetup");
        Assert.Equal(secondLocation, careSetup.GetProperty("locationId").GetGuid());
        Assert.Equal($"Location {secondLocation:N}", careSetup.GetProperty("location").GetString());
        Assert.Equal(secondSupervisor.Id, careSetup.GetProperty("supervisorUserId").GetGuid());
        Assert.Equal("Second Supervisor", careSetup.GetProperty("supervisorName").GetString());
        Assert.NotEqual("Main", careSetup.GetProperty("location").GetString());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var persisted = await db.Clients.AsNoTracking().SingleAsync(client => client.Id == clientId);
            Assert.Equal(secondLocation, persisted.LocationId);
            Assert.Equal(secondSupervisor.Id, persisted.SupervisorUserId);
            Assert.Equal($"Location {secondLocation:N}", persisted.Location);
        }

        var read = await http.GetAsync($"/api/clients/{clientId}");
        read.EnsureSuccessStatusCode();
        using var readJson = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        var readCareSetup = readJson.RootElement.GetProperty("careSetup");
        Assert.Equal(secondLocation, readCareSetup.GetProperty("locationId").GetGuid());
        Assert.Equal($"Location {secondLocation:N}", readCareSetup.GetProperty("location").GetString());
        Assert.Equal("Second Supervisor", readCareSetup.GetProperty("supervisorName").GetString());
    }

    [Fact]
    public async Task AuthenticatedEditRejectsInvalidLocationAndSupervisorCombinationsWithoutMutation()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create, Permissions.Client.Edit);
        var firstLocation = await MainLocationIdAsync();
        var secondLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        var inaccessibleLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        await GiveLocationAccessAsync(actor.Id, firstLocation);
        await GiveLocationAccessAsync(actor.Id, secondLocation);
        var firstSupervisor = await CreateEmployeeAsync(firstLocation, "Original Supervisor");
        var otherCompany = await CreateCompanyAsync();
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var create = await http.PostAsJsonAsync(
            "/api/clients",
            ValidClientRequest(UniquePmi(), firstLocation, firstSupervisor.Id));
        create.EnsureSuccessStatusCode();
        var original = JsonNode.Parse(await create.Content.ReadAsStringAsync())!.AsObject();
        var clientId = original["id"]!.GetValue<Guid>();

        var ineligibleSupervisor = original.DeepClone().AsObject();
        ineligibleSupervisor["careSetup"]!["locationId"] = secondLocation;
        ineligibleSupervisor["careSetup"]!["location"] = $"Location {secondLocation:N}";
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await http.PutAsJsonAsync($"/api/clients/{clientId}", ineligibleSupervisor)).StatusCode);

        var crossCompany = original.DeepClone().AsObject();
        crossCompany["careSetup"]!["locationId"] = otherCompany.LocationId;
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await http.PutAsJsonAsync($"/api/clients/{clientId}", crossCompany)).StatusCode);

        var inaccessible = original.DeepClone().AsObject();
        inaccessible["careSetup"]!["locationId"] = inaccessibleLocation;
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await http.PutAsJsonAsync($"/api/clients/{clientId}", inaccessible)).StatusCode);

        var invalidLocation = original.DeepClone().AsObject();
        invalidLocation["careSetup"]!["locationId"] = Guid.CreateVersion7();
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await http.PutAsJsonAsync($"/api/clients/{clientId}", invalidLocation)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await db.Clients.AsNoTracking().SingleAsync(client => client.Id == clientId);
        Assert.Equal(firstLocation, persisted.LocationId);
        Assert.Equal(firstSupervisor.Id, persisted.SupervisorUserId);
        Assert.Equal("Main", persisted.Location);
    }

    [Fact]
    public async Task StaleAuthenticatedEditDoesNotPartiallyUpdateLocationOrSupervisor()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create, Permissions.Client.Edit);
        var firstLocation = await MainLocationIdAsync();
        var secondLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        await GiveLocationAccessAsync(actor.Id, firstLocation);
        await GiveLocationAccessAsync(actor.Id, secondLocation);
        var firstSupervisor = await CreateEmployeeAsync(firstLocation, "Stale Supervisor");
        var secondSupervisor = await CreateEmployeeAsync(secondLocation, "Current Supervisor");
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var create = await http.PostAsJsonAsync(
            "/api/clients",
            ValidClientRequest(UniquePmi(), firstLocation, firstSupervisor.Id));
        create.EnsureSuccessStatusCode();
        var staleRequest = JsonNode.Parse(await create.Content.ReadAsStringAsync())!.AsObject();
        var clientId = staleRequest["id"]!.GetValue<Guid>();
        var currentRequest = staleRequest.DeepClone().AsObject();
        currentRequest["careSetup"]!["locationId"] = secondLocation;
        currentRequest["careSetup"]!["location"] = $"Location {secondLocation:N}";
        currentRequest["careSetup"]!["supervisorUserId"] = secondSupervisor.Id;
        (await http.PutAsJsonAsync($"/api/clients/{clientId}", currentRequest)).EnsureSuccessStatusCode();

        var stale = await http.PutAsJsonAsync($"/api/clients/{clientId}", staleRequest);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await db.Clients.AsNoTracking().SingleAsync(client => client.Id == clientId);
        Assert.Equal(secondLocation, persisted.LocationId);
        Assert.Equal(secondSupervisor.Id, persisted.SupervisorUserId);
        Assert.Equal($"Location {secondLocation:N}", persisted.Location);
    }

    [Fact]
    public async Task DuplicatePmiDoesNotCreatePartialRelatedRowsAndEmitsFailedAudit()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var pmi = UniquePmi();

        var first = await http.PostAsJsonAsync("/api/clients", ValidClientRequest(pmi, locationId));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var second = await http.PostAsJsonAsync("/api/clients", ValidClientRequest(pmi, locationId));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var ids = await db.Clients.Where(x => x.CompanyId == SqlServerApiFactory.DefaultCompanyId && x.PmiNumber == pmi)
            .Select(x => x.Id).ToArrayAsync();
        Assert.Single(ids);
        Assert.Single(await db.ClientAdditionalProfiles.Where(x => ids.Contains(x.ClientId)).ToArrayAsync());
        Assert.Single(await db.ClientServices.Where(x => ids.Contains(x.ClientId)).ToArrayAsync());
        Assert.Equal(2, await db.ClientLanguagePreferences.CountAsync(x => ids.Contains(x.ClientId)));
        Assert.Single(await db.Set<AuditLog>().Where(x =>
            x.ActorUserId == actor.Id && x.Action == "Client.Created" && x.Outcome == AuditOutcome.Failed).ToArrayAsync());
    }

    [Fact]
    public async Task PrimaryLanguageRuleFailsBeforeMutationAndAudit()
    {
        var actor = await CreateActorAsync(Permissions.Client.Create);
        var locationId = await MainLocationIdAsync();
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);
        var pmi = UniquePmi();
        var invalid = ValidClientRequest(pmi, locationId, languages:
        [
            new { languageCode = "en", languageName = "English", isPrimary = false },
            new { languageCode = "es", languageName = "Spanish", isPrimary = false },
        ]);

        var response = await http.PostAsJsonAsync("/api/clients", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(
            json.RootElement.GetProperty("errors").EnumerateObject(),
            error => error.Value.EnumerateArray().Any(message =>
                message.GetString() == "Exactly one preferred language must be marked as primary."));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.False(await db.Clients.AnyAsync(x => x.PmiNumber == pmi));
        Assert.False(await db.Set<AuditLog>().AnyAsync(x => x.ActorUserId == actor.Id && x.Action == "Client.Created"));
    }

    [Fact]
    public async Task LocationAndSupervisorLookupsAreOrganizationAndLocationScoped()
    {
        var actor = await CreateActorAsync(Permissions.Client.View);
        var allowedLocation = await MainLocationIdAsync();
        var hiddenLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        await GiveLocationAccessAsync(actor.Id, allowedLocation);
        var visible = await CreateEmployeeAsync(allowedLocation, "Visible Supervisor");
        var hidden = await CreateEmployeeAsync(hiddenLocation, "Hidden Supervisor");
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var locations = await http.GetFromJsonAsync<JsonElement>("/api/locations");
        Assert.Contains(locations.EnumerateArray(), item => item.GetProperty("id").GetGuid() == allowedLocation);
        Assert.Contains(locations.EnumerateArray(), item => item.GetProperty("id").GetGuid() == hiddenLocation);

        var candidates = await http.GetFromJsonAsync<JsonElement>(
            $"/api/clients/supervisor-candidates?locationId={allowedLocation}");
        Assert.Contains(candidates.EnumerateArray(), item => item.GetProperty("id").GetGuid() == visible.Id);
        Assert.DoesNotContain(candidates.EnumerateArray(), item => item.GetProperty("id").GetGuid() == hidden.Id);

        var forbidden = await http.GetAsync($"/api/clients/supervisor-candidates?locationId={hiddenLocation}");
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    private async Task<ApplicationUser> CreateActorAsync(params string[] permissions)
    {
        var actor = await factory.CreateUserAsync($"client-create-{Guid.NewGuid():N}@example.test", Password);
        foreach (var permission in permissions) await factory.GrantPermissionAsync(actor.Id, permission);
        return actor;
    }

    private async Task<ApplicationUser> CreateEmployeeAsync(Guid locationId, string name)
    {
        var employee = await factory.CreateUserAsync($"employee-{Guid.NewGuid():N}@example.test", Password);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await db.Users.SingleAsync(x => x.Id == employee.Id);
        persisted.FirstName = name.Split(' ')[0];
        persisted.LastName = name.Split(' ')[1];
        persisted.IsEmployee = true;
        persisted.JobTitle = "RN";
        db.UserLocations.Add(new ApplicationUserLocation { UserId = persisted.Id, LocationId = locationId });
        await db.SaveChangesAsync();
        return persisted;
    }

    private async Task<(Guid CompanyId, Guid LocationId)> CreateCompanyAsync()
    {
        var companyId = Guid.CreateVersion7();
        var locationId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.Companies.Add(Company.Create(companyId, $"Wizard {companyId:N}", $"WIZ-{companyId:N}", now));
        db.CompanyLocations.Add(CompanyLocation.Create(locationId, companyId, "Main", true, now));
        await db.SaveChangesAsync();
        return (companyId, locationId);
    }

    private async Task<Guid> CreateLocationAsync(Guid companyId)
    {
        var id = Guid.CreateVersion7();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.CompanyLocations.Add(CompanyLocation.Create(id, companyId, $"Location {id:N}", false, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> MainLocationIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await db.CompanyLocations.Where(x => x.CompanyId == SqlServerApiFactory.DefaultCompanyId && x.IsMain)
            .Select(x => x.Id).SingleAsync();
    }

    private async Task GiveLocationAccessAsync(Guid userId, Guid locationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        if (!await db.UserLocations.AnyAsync(x => x.UserId == userId && x.LocationId == locationId))
        {
            db.UserLocations.Add(new ApplicationUserLocation { UserId = userId, LocationId = locationId });
            await db.SaveChangesAsync();
        }
    }

    private static object ValidClientRequest(
        string pmi,
        Guid locationId,
        Guid? supervisorUserId = null,
        object[]? languages = null) => new
        {
            identity = new { firstName = "Guest", middleName = "Q", lastName = "Characterization", preferredName = "G", dateOfBirth = "1990-01-01", gender = "Female", pmiNumber = pmi, ssn = "123-45-6789" },
            contact = new { insuranceProvider = "Medical Assistance", email = "guest@example.test", primaryPhone = "(612) 555-0100", secondaryPhone = "", addressLine1 = "100 Main St", addressLine2 = "", city = "Minneapolis", state = "Minnesota", postalCode = "55401", county = "Hennepin", country = "United States" },
            careSetup = new { intakeDate = "2026-08-15", socDate = "2026-09-01", referralSource = "County", location = "Untrusted display name", isOnboarding = true, evvAuthorizedHoursPerWeek = 32m, locationId, supervisorUserId },
            safety = new { safetyMeasures = "Fall precautions", specialEquipment = "Walker", allergies = "None", otherServices = "None" },
            additionalProfile = new { programType = "Two45D", referralDetails = "Referral", homeCareRating = "Standard", medicalEquipment = "Walker", pcaComplexity = "Moderate", demographics = "Demographics", smokes = false, isVeteran = false, hasPets = true, livingArrangement = "Family", preferredClinic = "Clinic", preferredHospital = "Hospital", preferredAmbulance = "Ambulance", caregiverPreference = "No preference", nutritionalRequirements = "None", diet = "Regular", mealSchedule = "Standard", notes = "Characterization notes", additionalInformation = "Additional" },
            initialStatus = "Active",
            dischargeDate = (string?)null,
            dischargeReason = (string?)null,
            services = new[] { new { code = "245D", name = "245D" } },
            languages = languages ??
            [
                new { languageCode = "en", languageName = "English", isPrimary = true },
                new { languageCode = "es", languageName = "Spanish", isPrimary = false },
            ],
        };

    private static object ValidBasicDetailsRequest(
        string pmi,
        Guid locationId,
        Guid? supervisorUserId = null,
        Guid? companyId = null) => new
        {
            companyId,
            identity = new { firstName = "Step", middleName = "One", lastName = "Client", preferredName = "S", dateOfBirth = "1990-01-01", gender = "Female", pmiNumber = pmi, ssn = "123-45-6789" },
            careSetup = new { intakeDate = "2026-08-15", socDate = "2026-09-01", referralSource = "County", location = "Untrusted display name", isOnboarding = true, evvAuthorizedHoursPerWeek = 32m, locationId, supervisorUserId },
            insuranceProvider = "Medical Assistance",
            services = new[] { new { code = "245D", name = "245D" } },
        };

    private static object AdditionalInformationRequest(string editorVersion, string? notes) => new
    {
        additionalProfile = new
        {
            programType = "Two45D",
            referralDetails = (string?)null,
            homeCareRating = "Moderate",
            medicalEquipment = (string?)null,
            pcaComplexity = "Enhanced",
            demographics = "maritalStatus=Single",
            smokes = (bool?)null,
            isVeteran = false,
            hasPets = true,
            livingArrangement = "type=With family",
            preferredClinic = "North Clinic",
            preferredHospital = "Central Hospital",
            preferredAmbulance = "City Ambulance",
            caregiverPreference = "No preference",
            nutritionalRequirements = (string?)null,
            diet = "Regular",
            mealSchedule = "breakfast=08:00",
            notes,
            additionalInformation = (string?)null,
        },
        editorVersion,
    };

    private static object BasicDetailsUpdateRequest(
        string editorVersion,
        string pmi,
        Guid locationId,
        Guid? supervisorUserId = null,
        string? ssn = null) => new
        {
            identity = new { firstName = "Updated", middleName = "Basic", lastName = "Client", preferredName = "U", dateOfBirth = "1988-02-03", gender = "Male", pmiNumber = pmi, ssn },
            careSetup = new { intakeDate = "2026-08-10", socDate = "2026-09-01", referralSource = "Updated referral", location = "Ignored display", isOnboarding = false, evvAuthorizedHoursPerWeek = 24m, locationId, supervisorUserId },
            insuranceProvider = "Updated insurance",
            services = new[] { new { code = "245D", name = "Updated service" } },
            editorVersion,
        };

    private static object ContactInformationRequest(string editorVersion) => new
    {
        email = " client@example.test ",
        primaryPhone = "(612) 555-0111",
        secondaryPhone = "(612) 555-0222",
        addressLine1 = "100 Main St",
        addressLine2 = "Unit 2",
        city = "Minneapolis",
        state = "Minnesota",
        postalCode = "55401",
        county = "Hennepin",
        country = "United States",
        preferredLanguages = new[]
        {
            new { languageCode = "en", languageName = "English", isPrimary = true },
            new { languageCode = "es", languageName = "Spanish", isPrimary = false },
        },
        editorVersion,
    };

    private async Task<(Guid Id, string EditorVersion)> CreatePendingClientAsync(
        Guid companyId,
        Guid locationId)
    {
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var client = Client.Create(
            id,
            companyId,
            "Other",
            null,
            "Company",
            null,
            new DateOnly(1990, 1, 1),
            ClientGender.Unknown,
            UniquePmi(),
            new DateOnly(2026, 9, 1),
            null,
            ClientStatus.Pending,
            now);
        client.AssignLocation(locationId, "Main", now);
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return (client.Id, Convert.ToHexString(SHA256.HashData(client.RowVersion)));
    }

    private async Task<Guid> CreateClientWithStatusAsync(
        Guid companyId,
        Guid locationId,
        ClientStatus status)
    {
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var socDate = new DateOnly(2026, 9, 1);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var client = Client.Create(
            id,
            companyId,
            "Resume",
            null,
            "Status",
            null,
            new DateOnly(1990, 1, 1),
            ClientGender.Unknown,
            UniquePmi(),
            socDate,
            null,
            ClientStatus.Pending,
            now);
        client.AssignLocation(locationId, "Main", now);
        if (status != ClientStatus.Pending)
        {
            client.ChangeStatus(
                status,
                status == ClientStatus.Discharged ? socDate : null,
                status == ClientStatus.Discharged ? "Completed" : null,
                now);
        }
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return id;
    }

    private static string UniquePmi() =>
        Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);

    private static async Task SignInAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await client.GetAsync("/api/auth/csrf");
        csrf.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", document.RootElement.GetProperty("requestToken").GetString());
    }
}
