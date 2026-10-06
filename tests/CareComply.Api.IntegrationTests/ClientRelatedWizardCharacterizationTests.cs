using System.Net;
using System.Net.Http.Json;
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

public sealed class ClientRelatedWizardCharacterizationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task PrimaryCaregiverCreateAssociatesClientAndReturnsCurrentContractAndAudit()
    {
        var locationId = await MainLocationIdAsync();
        var actor = await CreateActorAsync(Permissions.ClientCaregiver.Create);
        await GiveLocationAccessAsync(actor.Id, locationId);
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        var caregiver = await CreateEmployeeAsync(SqlServerApiFactory.DefaultCompanyId, locationId, "Care Giver");
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var response = await http.PostAsJsonAsync($"/api/clients/{clientId}/caregivers", new
        {
            userId = caregiver.Id,
            type = "Primary",
            isPreferred = true,
            services = "245D",
            startDate = "2026-09-01",
            endDate = (string?)null,
            notes = "Assigned during intake",
            relationshipToClient = "Caregiver",
            livesWithClient = false,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var assignmentId = json.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(caregiver.Id, json.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal("Care Giver", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("RN", json.RootElement.GetProperty("title").GetString());
        Assert.True(json.RootElement.GetProperty("isPreferred").GetBoolean());
        Assert.Equal(64, json.RootElement.GetProperty("editorVersion").GetString()!.Length);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await db.ClientCaregivers.AsNoTracking().SingleAsync(x => x.Id == assignmentId);
        Assert.Equal(clientId, persisted.ClientId);
        var audit = await db.Set<AuditLog>().AsNoTracking().SingleAsync(x =>
            x.ActorUserId == actor.Id && x.Action == "ClientCaregiver.Assigned" && x.ResourceId == assignmentId.ToString("N"));
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal($"{{\"clientId\":\"{clientId:N}\"}}", audit.MetadataJson);
    }

    [Fact]
    public async Task CaregiverCandidatesAreClientScopedAndKeepFiveFieldContract()
    {
        var locationId = await MainLocationIdAsync();
        var actor = await CreateActorAsync(Permissions.ClientCaregiver.View);
        await GiveLocationAccessAsync(actor.Id, locationId);
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        var other = await CreateCompanyAsync();
        var crossCompany = await CreateEmployeeAsync(other.CompanyId, other.LocationId, "Other Tenant");
        var eligible = await CreateEmployeeAsync(SqlServerApiFactory.DefaultCompanyId, locationId, "Eligible Caregiver");
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var response = await http.GetAsync($"/api/clients/{clientId}/caregivers/candidates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(json.RootElement.EnumerateArray(), x => x.GetProperty("id").GetGuid() == crossCompany.Id);
        var exposed = json.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == eligible.Id);
        Assert.Equal(
            ["email", "id", "name", "phone", "title"],
            exposed.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.False(string.IsNullOrWhiteSpace(exposed.GetProperty("email").GetString()));
    }

    [Fact]
    public async Task CaregiverCreateRejectsCrossCompanyClientAndEmployee()
    {
        var actor = await CreateActorAsync(Permissions.ClientCaregiver.Create);
        var other = await CreateCompanyAsync();
        var clientId = await CreateClientAsync(other.CompanyId, other.LocationId);
        var caregiver = await CreateEmployeeAsync(other.CompanyId, other.LocationId, "Other Caregiver");
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var response = await http.PostAsJsonAsync($"/api/clients/{clientId}/caregivers", new
        {
            userId = caregiver.Id,
            type = "Primary",
            isPreferred = false,
            services = "245D",
            startDate = "2026-09-01",
            endDate = (string?)null,
            notes = "Current isolation characterization",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HospitalizationCreatePersistsAgainstClientAndEmitsCurrentAuditContract()
    {
        var locationId = await MainLocationIdAsync();
        var actor = await CreateActorAsync(Permissions.ClientHospitalization.Create);
        await GiveLocationAccessAsync(actor.Id, locationId);
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var response = await http.PostAsJsonAsync($"/api/clients/{clientId}/hospitalizations", new
        {
            hospitalName = "Hennepin Healthcare",
            admittedAtUtc = "2026-09-20T10:00:00Z",
            dischargedAtUtc = "2026-09-21T10:00:00Z",
            reason = "Observation",
            diagnosis = "Stable",
            dischargeInstructions = "Follow up",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Hennepin Healthcare", json.RootElement.GetProperty("hospitalName").GetString());
        Assert.Equal("Discharged", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(64, json.RootElement.GetProperty("editorVersion").GetString()!.Length);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal(clientId, (await db.ClientHospitalizations.AsNoTracking().SingleAsync(x => x.Id == id)).ClientId);
        var audit = await db.Set<AuditLog>().AsNoTracking().SingleAsync(x =>
            x.ActorUserId == actor.Id && x.Action == "ClientHospitalization.Created");
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(clientId.ToString(), audit.ResourceId);
        Assert.Null(audit.MetadataJson);
    }

    [Fact]
    public async Task HospitalizationEnforcesPermissionValidationNotFoundAndTenantIsolation()
    {
        var locationId = await MainLocationIdAsync();
        var actor = await CreateActorAsync(Permissions.ClientHospitalization.Create);
        await GiveLocationAccessAsync(actor.Id, locationId);
        var other = await CreateCompanyAsync();
        var otherClientId = await CreateClientAsync(other.CompanyId, other.LocationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var invalid = await http.PostAsJsonAsync($"/api/clients/{otherClientId}/hospitalizations", new
        {
            hospitalName = "Hospital",
            admittedAtUtc = "2026-09-21T10:00:00Z",
            dischargedAtUtc = "2026-09-20T10:00:00Z",
            reason = "Observation",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var notFound = await http.PostAsJsonAsync($"/api/clients/{otherClientId}/hospitalizations", new
        {
            hospitalName = "Hospital",
            admittedAtUtc = "2026-09-20T10:00:00Z",
            dischargedAtUtc = (string?)null,
            reason = "Observation",
        });
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);

        var forbiddenActor = await CreateActorAsync();
        using var forbiddenHttp = factory.CreateSecureClient();
        await SignInAsync(forbiddenHttp, forbiddenActor.Email!);
        var forbidden = await forbiddenHttp.PostAsJsonAsync($"/api/clients/{Guid.NewGuid()}/hospitalizations", new
        {
            hospitalName = "Hospital",
            admittedAtUtc = "2026-09-20T10:00:00Z",
            reason = "Observation",
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task HospitalizationListAndUpdateRemainClientScopedAndAuditExcludesMedicalDetails()
    {
        var locationId = await MainLocationIdAsync();
        var actor = await CreateActorAsync(
            Permissions.ClientHospitalization.View,
            Permissions.ClientHospitalization.Create,
            Permissions.ClientHospitalization.Edit);
        await GiveLocationAccessAsync(actor.Id, locationId);
        var owningClientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        var otherClientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        using var http = factory.CreateSecureClient();
        await SignInAsync(http, actor.Email!);

        var create = await http.PostAsJsonAsync($"/api/clients/{owningClientId}/hospitalizations", new
        {
            hospitalName = "Sensitive Hospital",
            admittedAtUtc = "2026-09-20T10:00:00Z",
            dischargedAtUtc = (string?)null,
            reason = "Sensitive medical reason",
            diagnosis = "Sensitive diagnosis",
            dischargeInstructions = "Sensitive instructions",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createdJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var hospitalizationId = createdJson.RootElement.GetProperty("id").GetGuid();
        var editorVersion = createdJson.RootElement.GetProperty("editorVersion").GetString();

        var list = await http.GetAsync($"/api/clients/{owningClientId}/hospitalizations");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Contains(listJson.RootElement.EnumerateArray(), item =>
            item.GetProperty("id").GetGuid() == hospitalizationId);

        var rejected = await http.PutAsJsonAsync(
            $"/api/clients/{otherClientId}/hospitalizations/{hospitalizationId}",
            new
            {
                hospitalName = "Wrong Client Hospital",
                admittedAtUtc = "2026-09-20T10:00:00Z",
                dischargedAtUtc = "2026-09-21T10:00:00Z",
                reason = "Wrong client mutation",
                diagnosis = "Wrong diagnosis",
                dischargeInstructions = "Wrong instructions",
                editorVersion,
            });
        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);

        await using (var rejectedScope = factory.Services.CreateAsyncScope())
        {
            var db = rejectedScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var unchanged = await db.ClientHospitalizations.AsNoTracking()
                .SingleAsync(item => item.Id == hospitalizationId);
            Assert.Equal(owningClientId, unchanged.ClientId);
            Assert.Equal("Sensitive Hospital", unchanged.HospitalName);
            Assert.Null(unchanged.DischargedAtUtc);
        }

        var update = await http.PutAsJsonAsync(
            $"/api/clients/{owningClientId}/hospitalizations/{hospitalizationId}",
            new
            {
                hospitalName = "Updated Hospital",
                admittedAtUtc = "2026-09-20T10:00:00Z",
                dischargedAtUtc = "2026-09-21T10:00:00Z",
                reason = "Updated sensitive reason",
                diagnosis = "Updated sensitive diagnosis",
                dischargeInstructions = "Updated sensitive instructions",
                editorVersion,
            });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var updatedJson = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        Assert.Equal("Updated Hospital", updatedJson.RootElement.GetProperty("hospitalName").GetString());
        Assert.Equal("Discharged", updatedJson.RootElement.GetProperty("status").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var auditDb = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var updateAudit = await auditDb.Set<AuditLog>().AsNoTracking().SingleAsync(log =>
            log.ActorUserId == actor.Id
            && log.Action == "ClientHospitalization.Updated"
            && log.ResourceId == hospitalizationId.ToString()
            && log.Outcome == AuditOutcome.Succeeded);
        Assert.Equal(AuditOutcome.Succeeded, updateAudit.Outcome);
        Assert.Null(updateAudit.MetadataJson);
    }

    private async Task<ApplicationUser> CreateActorAsync(params string[] permissions)
    {
        var actor = await factory.CreateUserAsync($"related-wizard-{Guid.NewGuid():N}@example.test", Password);
        foreach (var permission in permissions) await factory.GrantPermissionAsync(actor.Id, permission);
        return actor;
    }

    private async Task<ApplicationUser> CreateEmployeeAsync(Guid companyId, Guid locationId, string name)
    {
        var user = await factory.CreateUserAsync($"employee-{Guid.NewGuid():N}@example.test", Password);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var entity = await db.Users.SingleAsync(x => x.Id == user.Id);
        entity.CompanyId = companyId;
        entity.FirstName = name.Split(' ')[0];
        entity.LastName = name.Split(' ')[1];
        entity.Email = name == "Other Tenant" ? "other-tenant@example.test" : entity.Email;
        entity.NormalizedEmail = entity.Email!.ToUpperInvariant();
        entity.UserName = entity.Email;
        entity.NormalizedUserName = entity.NormalizedEmail;
        entity.PhoneNumber = "(612) 555-0199";
        entity.IsEmployee = true;
        entity.JobTitle = "RN";
        db.UserLocations.Add(new ApplicationUserLocation { UserId = entity.Id, LocationId = locationId });
        await db.SaveChangesAsync();
        return entity;
    }

    private async Task<Guid> CreateClientAsync(Guid companyId, Guid locationId)
    {
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var client = Client.Create(id, companyId, "Wizard", null, "Client", null,
            new DateOnly(1990, 1, 1), ClientGender.Female, UniquePmi(), new DateOnly(2026, 9, 1),
            null, ClientStatus.Active, now);
        client.AssignLocation(locationId, "Main", now);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<(Guid CompanyId, Guid LocationId)> CreateCompanyAsync()
    {
        var companyId = Guid.CreateVersion7();
        var locationId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.Companies.Add(Company.Create(companyId, $"Related {companyId:N}", $"REL-{companyId:N}", now));
        db.CompanyLocations.Add(CompanyLocation.Create(locationId, companyId, "Main", true, now));
        await db.SaveChangesAsync();
        return (companyId, locationId);
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

    private static string UniquePmi() => Guid.NewGuid().ToString("N")[..8].Replace('a', '1').Replace('b', '2').Replace('c', '3').Replace('d', '4').Replace('e', '5').Replace('f', '6');

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
