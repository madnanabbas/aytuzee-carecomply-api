using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using CareComply.Domain.Companies;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class PayerCoverageIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task PayerCrudIsCompanyScopedSupportsInactiveAndRejectsStaleUpdates()
    {
        var actor = await CreateActorAsync(Permissions.Payer.View, Permissions.Payer.Create,
            Permissions.Payer.Edit);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        var create = await client.PostAsJsonAsync("/api/payers", new { name = "Northstar Health" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createdJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var payerId = createdJson.RootElement.GetProperty("id").GetGuid();
        var version = createdJson.RootElement.GetProperty("editorVersion").GetString();

        var otherCompany = await CreateCompanyAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            db.Payers.Add(Payer.Create(Guid.CreateVersion7(), otherCompany.CompanyId,
                "Other tenant payer", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/payers");
        Assert.Contains(list!, item => item.GetProperty("id").GetGuid() == payerId);
        Assert.DoesNotContain(list!, item =>
            item.GetProperty("name").GetString() == "Other tenant payer");

        var update = await client.PutAsJsonAsync($"/api/payers/{payerId}", new
        {
            name = "Northstar Health Updated",
            isActive = false,
            editorVersion = version,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.DoesNotContain((await client.GetFromJsonAsync<JsonElement[]>("/api/payers"))!,
            item => item.GetProperty("id").GetGuid() == payerId);
        Assert.Contains((await client.GetFromJsonAsync<JsonElement[]>("/api/payers?includeInactive=true"))!,
            item => item.GetProperty("id").GetGuid() == payerId);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/payers/{payerId}", new
        {
            name = "Stale",
            isActive = true,
            editorVersion = version,
        })).StatusCode);
    }

    [Fact]
    public async Task CoverageSupportsMasterAndManualPayersAndKeepsMemberIdentifiersSeparate()
    {
        var setup = await CreateContextAsync();
        var payer = await CreatePayerAsync(setup.HttpClient, "Coverage payer");
        var master = await setup.HttpClient.PostAsJsonAsync(Route(setup.ClientId), new
        {
            payerId = payer.Id,
            manualPayerName = (string?)null,
            memberId = "MEMBER-101",
            medicaidId = "MEDICAID-202",
            effectiveFrom = new DateOnly(2026, 1, 1),
            effectiveTo = new DateOnly(2026, 12, 31),
            isVerified = true,
            isPrimary = true,
        });
        var manual = await setup.HttpClient.PostAsJsonAsync(Route(setup.ClientId), new
        {
            payerId = (Guid?)null,
            manualPayerName = "County manual payer",
            memberId = "MANUAL-MEMBER",
            medicaidId = "MANUAL-MEDICAID",
            effectiveFrom = (DateOnly?)null,
            effectiveTo = (DateOnly?)null,
            isVerified = false,
            isPrimary = false,
        });

        Assert.Equal(HttpStatusCode.Created, master.StatusCode);
        Assert.Equal(HttpStatusCode.Created, manual.StatusCode);
        var items = await setup.HttpClient.GetFromJsonAsync<JsonElement[]>(Route(setup.ClientId));
        Assert.Equal(2, items!.Length);
        var primary = Assert.Single(items, item => item.GetProperty("isPrimary").GetBoolean());
        Assert.Equal("MEMBER-101", primary.GetProperty("memberId").GetString());
        Assert.Equal("MEDICAID-202", primary.GetProperty("medicaidId").GetString());
        Assert.Equal("Coverage payer", primary.GetProperty("payerName").GetString());
        Assert.Contains(items, item => item.GetProperty("manualPayerName").GetString() == "County manual payer");
    }

    [Fact]
    public async Task CoverageRejectsInvalidSelectionDatesDuplicatePrimaryCrossTenantAndInaccessibleClient()
    {
        var setup = await CreateContextAsync();
        var payer = await CreatePayerAsync(setup.HttpClient, "Valid payer");
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.HttpClient.PostAsJsonAsync(Route(setup.ClientId), new
        {
            payerId = (Guid?)null, manualPayerName = (string?)null, memberId = "A", medicaidId = "B",
            effectiveFrom = (DateOnly?)null, effectiveTo = (DateOnly?)null,
            isVerified = false, isPrimary = false,
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.HttpClient.PostAsJsonAsync(Route(setup.ClientId), new
        {
            payerId = payer.Id, manualPayerName = (string?)null, memberId = "A", medicaidId = "B",
            effectiveFrom = new DateOnly(2026, 2, 1), effectiveTo = new DateOnly(2026, 1, 1),
            isVerified = false, isPrimary = false,
        })).StatusCode);

        var primary = CoverageRequest(payer.Id, true);
        Assert.Equal(HttpStatusCode.Created,
            (await setup.HttpClient.PostAsJsonAsync(Route(setup.ClientId), primary)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await setup.HttpClient.PostAsJsonAsync(Route(setup.ClientId), primary)).StatusCode);

        var otherCompany = await CreateCompanyAsync();
        Guid otherPayerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var other = Payer.Create(Guid.CreateVersion7(), otherCompany.CompanyId, "Other payer", DateTimeOffset.UtcNow);
            otherPayerId = other.Id;
            db.Payers.Add(other);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest,
            (await setup.HttpClient.PostAsJsonAsync(Route(setup.ClientId), CoverageRequest(otherPayerId, false))).StatusCode);

        var inaccessibleLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        var inaccessibleClient = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, inaccessibleLocation);
        Assert.Equal(HttpStatusCode.NotFound,
            (await setup.HttpClient.PostAsJsonAsync(Route(inaccessibleClient), CoverageRequest(payer.Id, false))).StatusCode);
    }

    [Fact]
    public async Task CoverageUpdateUsesOptimisticConcurrency()
    {
        var setup = await CreateContextAsync();
        var payer = await CreatePayerAsync(setup.HttpClient, "Concurrency payer");
        var create = await setup.HttpClient.PostAsJsonAsync(Route(setup.ClientId), CoverageRequest(payer.Id, false));
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var coverageId = created.RootElement.GetProperty("id").GetGuid();
        var version = created.RootElement.GetProperty("editorVersion").GetString();
        var updateBody = new
        {
            payerId = payer.Id, manualPayerName = (string?)null, memberId = "Updated member",
            medicaidId = "Separate Medicaid", effectiveFrom = (DateOnly?)null,
            effectiveTo = (DateOnly?)null, isVerified = true, isPrimary = false,
            editorVersion = version,
        };
        Assert.Equal(HttpStatusCode.OK,
            (await setup.HttpClient.PutAsJsonAsync($"{Route(setup.ClientId)}/{coverageId}", updateBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await setup.HttpClient.PutAsJsonAsync($"{Route(setup.ClientId)}/{coverageId}", updateBody)).StatusCode);
    }

    [Fact]
    public async Task ServiceAgreementUsesClientCoverageRejectsMismatchAndLegacyPayerNameStillLoads()
    {
        var setup = await CreateContextAsync();
        var payer = await CreatePayerAsync(setup.HttpClient, "Agreement payer");
        var coverageResponse = await setup.HttpClient.PostAsJsonAsync(
            Route(setup.ClientId), CoverageRequest(payer.Id, false));
        using var coverageJson = JsonDocument.Parse(await coverageResponse.Content.ReadAsStringAsync());
        var coverageId = coverageJson.RootElement.GetProperty("id").GetGuid();

        var agreement = await setup.HttpClient.PostAsJsonAsync($"/api/clients/{setup.ClientId}/service-agreements",
            AgreementRequest(payer.Id, coverageId, null));
        Assert.Equal(HttpStatusCode.Created, agreement.StatusCode);
        using var agreementJson = JsonDocument.Parse(await agreement.Content.ReadAsStringAsync());
        Assert.Equal(payer.Id, agreementJson.RootElement.GetProperty("payerId").GetGuid());
        Assert.Equal(coverageId, agreementJson.RootElement.GetProperty("clientCoverageId").GetGuid());
        Assert.Equal("Agreement payer", agreementJson.RootElement.GetProperty("payerName").GetString());

        var otherPayer = await CreatePayerAsync(setup.HttpClient, "Contradicting payer");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await setup.HttpClient.PostAsJsonAsync($"/api/clients/{setup.ClientId}/service-agreements",
                AgreementRequest(otherPayer.Id, coverageId, null))).StatusCode);

        var otherClient = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, setup.LocationId);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await setup.HttpClient.PostAsJsonAsync($"/api/clients/{otherClient}/service-agreements",
                AgreementRequest(null, coverageId, null))).StatusCode);

        var legacy = await setup.HttpClient.PostAsJsonAsync($"/api/clients/{setup.ClientId}/service-agreements",
            AgreementRequest(null, null, "Legacy free-text payer"));
        Assert.Equal(HttpStatusCode.Created, legacy.StatusCode);
        using var legacyJson = JsonDocument.Parse(await legacy.Content.ReadAsStringAsync());
        Assert.Equal("Legacy free-text payer", legacyJson.RootElement.GetProperty("payerName").GetString());
        Assert.Equal(JsonValueKind.Null, legacyJson.RootElement.GetProperty("payerId").ValueKind);
    }

    private async Task<TestContext> CreateContextAsync()
    {
        var actor = await CreateActorAsync(Permissions.Payer.View, Permissions.Payer.Create,
            Permissions.Payer.Edit, Permissions.ClientCoverage.View, Permissions.ClientCoverage.Create,
            Permissions.ClientCoverage.Edit, Permissions.ClientServiceAgreement.View,
            Permissions.ClientServiceAgreement.Create, Permissions.ClientServiceAgreement.Edit);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        var httpClient = factory.CreateSecureClient();
        await SignInAsync(httpClient, actor.Email!);
        return new(actor, locationId, clientId, httpClient);
    }

    private static async Task<(Guid Id, string EditorVersion)> CreatePayerAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/payers", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("id").GetGuid(), json.RootElement.GetProperty("editorVersion").GetString()!);
    }

    private static object CoverageRequest(Guid payerId, bool primary) => new
    {
        payerId, manualPayerName = (string?)null, memberId = "Member", medicaidId = "Medicaid",
        effectiveFrom = new DateOnly(2026, 1, 1), effectiveTo = new DateOnly(2026, 12, 31),
        isVerified = true, isPrimary = primary,
    };

    private static object AgreementRequest(Guid? payerId, Guid? coverageId, string? payerName) => new
    {
        payerId, clientCoverageId = coverageId, agreementNumber = "AUTH-PAYER", payerName,
        insuredMemberId = "Member", priorAuthorizationNumber = "Prior",
        startDate = new DateOnly(2026, 1, 1), endDate = (DateOnly?)null, status = "Active",
        priority = "Normal", isDummy = false, notes = (string?)null,
        lines = new[] { new { serviceCode = "T1019", serviceName = "Personal care",
            unitType = "FifteenMinutes", minutesPerUnit = (int?)15, authorizedUnits = (decimal?)10,
            ratePerUnit = (decimal?)5, startDate = new DateOnly(2026, 1, 1), endDate = (DateOnly?)null,
            rateEffectiveFrom = (DateOnly?)null, rateEffectiveTo = (DateOnly?)null,
            authorizedBudgetAmount = (decimal?)50, plannedHoursPerVisit = (decimal?)null,
            plannedVisitsPerWeek = (decimal?)null, diagnoses = Array.Empty<object>() } },
    };

    private async Task<ApplicationUser> CreateActorAsync(params string[] permissions)
    {
        var actor = await factory.CreateUserAsync($"payer-coverage-{Guid.NewGuid():N}@example.test", Password);
        foreach (var permission in permissions) await factory.GrantPermissionAsync(actor.Id, permission);
        return actor;
    }

    private async Task<Guid> CreateClientAsync(Guid companyId, Guid locationId)
    {
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var client = Client.Create(id, companyId, "Coverage", null, "Client", null,
            new DateOnly(1990, 1, 1), ClientGender.Unknown, $"PMI-{Guid.NewGuid():N}",
            new DateOnly(2026, 1, 1), null, ClientStatus.Active, now);
        client.AssignLocation(locationId, "Test Location", now);
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
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.Companies.Add(Company.Create(companyId, $"Coverage {companyId:N}", $"COV-{companyId:N}", DateTimeOffset.UtcNow));
        db.CompanyLocations.Add(CompanyLocation.Create(locationId, companyId, "Main", true, DateTimeOffset.UtcNow));
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

    private async Task GiveLocationAccessAsync(Guid userId, Guid locationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.UserLocations.Add(new ApplicationUserLocation { UserId = userId, LocationId = locationId });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> MainLocationIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CareComplyDbContext>().CompanyLocations
            .Where(x => x.CompanyId == SqlServerApiFactory.DefaultCompanyId).Select(x => x.Id).FirstAsync();
    }

    private static async Task SignInAsync(HttpClient client, string email)
    {
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password })).StatusCode);
        using var json = JsonDocument.Parse(await (await client.GetAsync("/api/auth/csrf")).Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", json.RootElement.GetProperty("requestToken").GetString());
    }

    private static string Route(Guid clientId) => $"/api/clients/{clientId}/coverages";
    private sealed record TestContext(ApplicationUser Actor, Guid LocationId, Guid ClientId, HttpClient HttpClient);
}
