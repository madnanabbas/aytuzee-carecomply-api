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

public sealed class ClientServiceAgreementIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task ViewPermissionIsRequiredForListAndDetails()
    {
        var actor = await CreateActorAsync();
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync(Route(clientId))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync($"{Route(clientId)}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task CreateAndEditPermissionsAreRequired()
    {
        var actor = await CreateActorAsync(Permissions.ClientServiceAgreement.View);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(Route(clientId), ValidCreateRequest())).StatusCode);

        await factory.GrantPermissionAsync(actor.Id, Permissions.ClientServiceAgreement.Create);
        using var createClient = factory.CreateSecureClient();
        await SignInAsync(createClient, actor.Email!);
        var created = await CreateAgreementAsync(createClient, clientId);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await createClient.PutAsJsonAsync(
                $"{Route(clientId)}/{created.Id}",
                ValidUpdateRequest(created.EditorVersion, [Line(created.LineIds[0], "Updated")]))).StatusCode);
    }

    [Fact]
    public async Task ListAndDetailsReturnOnlyClientAgreementsWithCalculatedInputValues()
    {
        var setup = await CreateAuthorizedContextAsync(AllPermissions);
        var otherClientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, setup.LocationId);
        var created = await CreateAgreementAsync(setup.HttpClient, setup.ClientId, twoLines: true);

        var list = await setup.HttpClient.GetAsync(Route(setup.ClientId));
        var details = await setup.HttpClient.GetAsync($"{Route(setup.ClientId)}/{created.Id}");
        var wrongClient = await setup.HttpClient.GetAsync($"{Route(otherClientId)}/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrongClient.StatusCode);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Contains(
            listJson.RootElement.EnumerateArray(),
            agreement => agreement.GetProperty("id").GetGuid() == created.Id);
        using var detailsJson = JsonDocument.Parse(await details.Content.ReadAsStringAsync());
        var root = detailsJson.RootElement;
        Assert.Equal(created.Id, root.GetProperty("id").GetGuid());
        Assert.True(root.GetProperty("isDummy").GetBoolean());
        Assert.Equal("High", root.GetProperty("priority").GetString());
        Assert.Equal(2, root.GetProperty("lines").GetArrayLength());
        var calculated = root.GetProperty("lines")[0];
        Assert.Equal(800m, calculated.GetProperty("estimatedBudget").GetDecimal());
        Assert.Equal(6m, calculated.GetProperty("plannedHoursPerWeek").GetDecimal());
        Assert.Equal(24m, calculated.GetProperty("plannedUnitsPerWeek").GetDecimal());
        Assert.Equal(900m, calculated.GetProperty("authorizedBudgetAmount").GetDecimal());
        Assert.Equal("2026-01-01", calculated.GetProperty("startDate").GetString());
        Assert.Equal(2, calculated.GetProperty("diagnoses").GetArrayLength());
        Assert.Single(calculated.GetProperty("diagnoses").EnumerateArray(), diagnosis =>
            diagnosis.GetProperty("isPrimary").GetBoolean());
        Assert.False(calculated.TryGetProperty("usedUnits", out _));
        Assert.False(calculated.TryGetProperty("remainingUnits", out _));
        Assert.False(calculated.TryGetProperty("usedValue", out _));
        Assert.False(calculated.TryGetProperty("remainingValue", out _));
    }

    [Fact]
    public async Task CrossCompanyAndCrossLocationClientsAreNotExposed()
    {
        var actor = await CreateActorAsync(Permissions.ClientServiceAgreement.View);
        var accessibleLocationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, accessibleLocationId);
        var inaccessibleLocationId = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        var locationClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            inaccessibleLocationId);
        var otherCompany = await CreateCompanyAsync();
        var companyClientId = await CreateClientAsync(otherCompany.CompanyId, otherCompany.LocationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Route(locationClientId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Route(companyClientId))).StatusCode);
    }

    [Fact]
    public async Task CreateValidatesLinesDatesAndValuesWithoutPartialPersistence()
    {
        var setup = await CreateAuthorizedContextAsync(Permissions.ClientServiceAgreement.Create);
        var missingLines = await setup.HttpClient.PostAsJsonAsync(
            Route(setup.ClientId),
            ValidCreateRequest(lines: []));
        var invalidDate = await setup.HttpClient.PostAsJsonAsync(
            Route(setup.ClientId),
            ValidCreateRequest(
                startDate: new DateOnly(2026, 2, 2),
                endDate: new DateOnly(2026, 2, 1)));
        var invalidValues = await setup.HttpClient.PostAsJsonAsync(
            Route(setup.ClientId),
            ValidCreateRequest(lines:
            [
                new
                {
                    serviceCode = "T1019",
                    serviceName = "Invalid",
                    minutesPerUnit = (int?)0,
                    authorizedUnits = (decimal?)-1,
                    ratePerUnit = (decimal?)-1,
                    plannedHoursPerVisit = (decimal?)-1,
                    plannedVisitsPerWeek = (decimal?)-1,
                    startDate = default(DateOnly),
                    authorizedBudgetAmount = (decimal?)-1,
                    diagnoses = new[]
                    {
                        new { diagnosisText = "One", icdCode = "A", isPrimary = true },
                        new { diagnosisText = "Two", icdCode = "B", isPrimary = true },
                    },
                },
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, missingLines.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidValues.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.False(await db.ClientServiceAgreements.AnyAsync(row => row.ClientId == setup.ClientId));
        Assert.False(await (
            from line in db.ClientServiceAgreementLines
            join agreement in db.ClientServiceAgreements
                on line.ClientServiceAgreementId equals agreement.Id
            where agreement.ClientId == setup.ClientId
            select line.Id).AnyAsync());
    }

    [Fact]
    public async Task CreatePersistsMultipleLinesAndEmitsRedactedAudit()
    {
        var setup = await CreateAuthorizedContextAsync(
            Permissions.ClientServiceAgreement.Create,
            Permissions.ClientServiceAgreement.View);
        var created = await CreateAgreementAsync(setup.HttpClient, setup.ClientId, twoLines: true);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal(2, await db.ClientServiceAgreementLines.CountAsync(
            line => line.ClientServiceAgreementId == created.Id));
        var audit = await db.Set<AuditLog>().AsNoTracking().SingleAsync(log =>
            log.ActorUserId == setup.Actor.Id
            && log.Action == "ClientServiceAgreement.Created"
            && log.ResourceId == created.Id.ToString("N"));
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(
            $"{{\"clientId\":\"{setup.ClientId:N}\",\"lineCount\":\"2\"}}",
            audit.MetadataJson);
        AssertSensitiveValuesAbsent(audit);
    }

    [Fact]
    public async Task UpdateChangesHeaderAndLineAddsLinePreservesOmittedLineAndRejectsStaleVersion()
    {
        var setup = await CreateAuthorizedContextAsync(AllPermissions);
        var created = await CreateAgreementAsync(setup.HttpClient, setup.ClientId, twoLines: true);
        var originalVersion = created.EditorVersion;
        var update = await setup.HttpClient.PutAsJsonAsync(
            $"{Route(setup.ClientId)}/{created.Id}",
            ValidUpdateRequest(
                originalVersion,
                [Line(created.LineIds[0], "Updated service"), Line(null, "New service", "S5125")],
                agreementNumber: "AUTH-UPDATED"));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var updatedJson = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        var root = updatedJson.RootElement;
        Assert.Equal("AUTH-UPDATED", root.GetProperty("agreementNumber").GetString());
        Assert.NotEqual(originalVersion, root.GetProperty("editorVersion").GetString());
        Assert.Equal(3, root.GetProperty("lines").GetArrayLength());
        Assert.Contains(root.GetProperty("lines").EnumerateArray(), line =>
            line.GetProperty("id").GetGuid() == created.LineIds[0]
            && line.GetProperty("serviceName").GetString() == "Updated service");
        Assert.Contains(root.GetProperty("lines").EnumerateArray(), line =>
            line.GetProperty("id").GetGuid() == created.LineIds[1]);

        var stale = await setup.HttpClient.PutAsJsonAsync(
            $"{Route(setup.ClientId)}/{created.Id}",
            ValidUpdateRequest(originalVersion, [Line(created.LineIds[0], "Stale")]));
        var repeatedStale = await setup.HttpClient.PutAsJsonAsync(
            $"{Route(setup.ClientId)}/{created.Id}",
            ValidUpdateRequest(originalVersion, [Line(created.LineIds[0], "Stale again")]));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, repeatedStale.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var audit = await db.Set<AuditLog>().AsNoTracking().SingleAsync(log =>
            log.ActorUserId == setup.Actor.Id
            && log.Action == "ClientServiceAgreement.Updated"
            && log.ResourceId == created.Id.ToString("N")
            && log.Outcome == AuditOutcome.Succeeded);
        AssertSensitiveValuesAbsent(audit);
    }

    [Fact]
    public async Task AgreementCannotBeUpdatedThroughAnotherClientRoute()
    {
        var setup = await CreateAuthorizedContextAsync(AllPermissions);
        var otherClientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, setup.LocationId);
        var created = await CreateAgreementAsync(setup.HttpClient, setup.ClientId);

        var response = await setup.HttpClient.PutAsJsonAsync(
            $"{Route(otherClientId)}/{created.Id}",
            ValidUpdateRequest(created.EditorVersion, [Line(created.LineIds[0], "Changed")]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ActiveSameServiceOverlapReturnsStructuredWarningAndCreateStillSucceeds()
    {
        var setup = await CreateAuthorizedContextAsync(AllPermissions);
        var first = await setup.HttpClient.PostAsJsonAsync(
            Route(setup.ClientId),
            OverlapCreateRequest("OVERLAP-A", "T-OVERLAP", new(2026, 1, 1), new(2026, 1, 31)));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using (var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync()))
            Assert.Empty(firstJson.RootElement.GetProperty("warnings").EnumerateArray());

        var overlapping = await setup.HttpClient.PostAsJsonAsync(
            Route(setup.ClientId),
            OverlapCreateRequest("OVERLAP-B", "T-OVERLAP", new(2026, 1, 15), new(2026, 2, 15)));

        Assert.Equal(HttpStatusCode.Created, overlapping.StatusCode);
        using var json = JsonDocument.Parse(await overlapping.Content.ReadAsStringAsync());
        var warning = Assert.Single(json.RootElement.GetProperty("warnings").EnumerateArray());
        Assert.Equal("SERVICE_LINE_OVERLAP", warning.GetProperty("code").GetString());
        Assert.Equal("T-OVERLAP", warning.GetProperty("serviceCode").GetString());
        Assert.Equal("2026-01-15", warning.GetProperty("overlapStartDate").GetString());
        Assert.Equal("2026-01-31", warning.GetProperty("overlapEndDate").GetString());
    }

    [Fact]
    public async Task OverlapUsesInclusiveOpenEndedDatesAndOnlySameServiceActiveAgreements()
    {
        var setup = await CreateAuthorizedContextAsync(AllPermissions);

        await AssertCreateWarningCountAsync(setup, "ADJ-A", "T-ADJ", new(2026, 1, 1), new(2026, 1, 31), 0);
        await AssertCreateWarningCountAsync(setup, "ADJ-B", "T-ADJ", new(2026, 2, 1), new(2026, 2, 28), 0);

        await AssertCreateWarningCountAsync(setup, "BOUND-A", "T-BOUND", new(2026, 1, 1), new(2026, 1, 31), 0);
        await AssertCreateWarningCountAsync(setup, "BOUND-B", "T-BOUND", new(2026, 1, 31), new(2026, 2, 28), 1);

        await AssertCreateWarningCountAsync(setup, "OPEN-A", "T-OPEN", new(2026, 1, 1), null, 0);
        await AssertCreateWarningCountAsync(setup, "OPEN-B", "T-OPEN", new(2026, 2, 1), new(2026, 2, 28), 1);

        await AssertCreateWarningCountAsync(setup, "DIFF-A", "T-DIFFERENT-A", new(2026, 1, 1), new(2026, 1, 31), 0);
        await AssertCreateWarningCountAsync(setup, "DIFF-B", "T-DIFFERENT-B", new(2026, 1, 1), new(2026, 1, 31), 0);

        await AssertCreateWarningCountAsync(setup, "EXPIRED", "T-EXPIRED", new(2026, 1, 1), new(2026, 1, 31), 0, "Expired");
        await AssertCreateWarningCountAsync(setup, "EXPIRED-ACTIVE", "T-EXPIRED", new(2026, 1, 1), new(2026, 1, 31), 0);

        await AssertCreateWarningCountAsync(setup, "DRAFT", "T-DRAFT", new(2026, 1, 1), new(2026, 1, 31), 0, "Draft");
        await AssertCreateWarningCountAsync(setup, "DRAFT-ACTIVE", "T-DRAFT", new(2026, 1, 1), new(2026, 1, 31), 0);
    }

    [Fact]
    public async Task UpdateExcludesItselfAndStillSucceedsWhenItIntroducesOverlap()
    {
        var setup = await CreateAuthorizedContextAsync(AllPermissions);
        await AssertCreateWarningCountAsync(setup, "UPDATE-A", "T-UPDATE", new(2026, 1, 1), new(2026, 1, 31), 0);
        var editable = await CreateAgreementWithDatesAsync(
            setup,
            "UPDATE-B",
            "T-UPDATE",
            new(2026, 2, 1),
            new(2026, 2, 28));

        var unchanged = await setup.HttpClient.GetAsync($"{Route(setup.ClientId)}/{editable.Id}");
        using (var unchangedJson = JsonDocument.Parse(await unchanged.Content.ReadAsStringAsync()))
            Assert.Empty(unchangedJson.RootElement.GetProperty("warnings").EnumerateArray());

        var update = await setup.HttpClient.PutAsJsonAsync(
            $"{Route(setup.ClientId)}/{editable.Id}",
            OverlapUpdateRequest(
                editable.EditorVersion,
                editable.LineIds[0],
                "UPDATE-B",
                "T-UPDATE",
                new(2026, 1, 31),
                new(2026, 2, 28)));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var json = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        Assert.Single(json.RootElement.GetProperty("warnings").EnumerateArray());
    }

    [Fact]
    public async Task OverlapIgnoresOtherClientsAndOtherCompaniesWithoutLeakingThem()
    {
        var setup = await CreateAuthorizedContextAsync(AllPermissions);
        var otherClientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, setup.LocationId);
        var otherClientAgreement = await setup.HttpClient.PostAsJsonAsync(
            Route(otherClientId),
            OverlapCreateRequest("OTHER-CLIENT", "T-ISOLATED", new(2026, 1, 1), null));
        Assert.Equal(HttpStatusCode.Created, otherClientAgreement.StatusCode);
        await AssertCreateWarningCountAsync(setup, "CURRENT", "T-ISOLATED", new(2026, 2, 1), null, 0);

        var otherCompany = await CreateCompanyAsync();
        var otherCompanyClientId = await CreateClientAsync(otherCompany.CompanyId, otherCompany.LocationId);
        await InsertAgreementAsync(otherCompanyClientId, "T-TENANT", new(2026, 1, 1), null);
        await AssertCreateWarningCountAsync(setup, "TENANT", "T-TENANT", new(2026, 2, 1), null, 0);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await setup.HttpClient.GetAsync(Route(otherCompanyClientId))).StatusCode);
    }

    private static readonly string[] AllPermissions =
    [
        Permissions.ClientServiceAgreement.View,
        Permissions.ClientServiceAgreement.Create,
        Permissions.ClientServiceAgreement.Edit,
    ];

    private static async Task AssertCreateWarningCountAsync(
        AuthorizedContext setup,
        string agreementNumber,
        string serviceCode,
        DateOnly startDate,
        DateOnly? endDate,
        int expectedCount,
        string status = "Active")
    {
        var response = await setup.HttpClient.PostAsJsonAsync(
            Route(setup.ClientId),
            OverlapCreateRequest(agreementNumber, serviceCode, startDate, endDate, status));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCount, json.RootElement.GetProperty("warnings").GetArrayLength());
    }

    private static async Task<CreatedAgreement> CreateAgreementWithDatesAsync(
        AuthorizedContext setup,
        string agreementNumber,
        string serviceCode,
        DateOnly startDate,
        DateOnly? endDate)
    {
        var response = await setup.HttpClient.PostAsJsonAsync(
            Route(setup.ClientId),
            OverlapCreateRequest(agreementNumber, serviceCode, startDate, endDate));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new(
            json.RootElement.GetProperty("id").GetGuid(),
            json.RootElement.GetProperty("editorVersion").GetString()!,
            json.RootElement.GetProperty("lines").EnumerateArray()
                .Select(line => line.GetProperty("id").GetGuid()).ToArray());
    }

    private async Task InsertAgreementAsync(
        Guid clientId,
        string serviceCode,
        DateOnly startDate,
        DateOnly? endDate)
    {
        var now = DateTimeOffset.UtcNow;
        var agreement = ClientServiceAgreement.Create(
            Guid.CreateVersion7(), clientId, "OTHER-TENANT", "Payer", null, null,
            startDate, endDate, ClientServiceAgreementStatus.Active,
            ClientServiceAgreementPriority.Normal, false, null, now);
        var line = ClientServiceAgreementLine.Create(
            Guid.CreateVersion7(), agreement.Id, serviceCode, "Other tenant service",
            ClientServiceAgreementUnitType.Hour, 60, 1, 1, startDate, endDate,
            null, null, null, null, null, now);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.ClientServiceAgreements.Add(agreement);
        db.ClientServiceAgreementLines.Add(line);
        await db.SaveChangesAsync();
    }

    private async Task<AuthorizedContext> CreateAuthorizedContextAsync(params string[] permissions)
    {
        var actor = await CreateActorAsync(permissions);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        var httpClient = factory.CreateSecureClient();
        await SignInAsync(httpClient, actor.Email!);
        return new(actor, locationId, clientId, httpClient);
    }

    private static async Task<CreatedAgreement> CreateAgreementAsync(
        HttpClient client,
        Guid clientId,
        bool twoLines = false)
    {
        var lines = twoLines
            ? new[] { Line(), Line(null, "Skilled nursing", "T1002") }
            : [Line()];
        var response = await client.PostAsJsonAsync(
            Route(clientId),
            ValidCreateRequest(lines: lines));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        return new(
            root.GetProperty("id").GetGuid(),
            root.GetProperty("editorVersion").GetString()!,
            root.GetProperty("lines").EnumerateArray()
                .Select(line => line.GetProperty("id").GetGuid())
                .ToArray());
    }

    private async Task<ApplicationUser> CreateActorAsync(params string[] permissions)
    {
        var actor = await factory.CreateUserAsync(
            $"service-agreement-{Guid.NewGuid():N}@example.test",
            Password);
        foreach (var permission in permissions)
        {
            await factory.GrantPermissionAsync(actor.Id, permission);
        }

        return actor;
    }

    private async Task<Guid> CreateClientAsync(Guid companyId, Guid locationId)
    {
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var client = Client.Create(
            id, companyId, "Agreement", null, "Client", null,
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
        var now = DateTimeOffset.UtcNow;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.Companies.Add(Company.Create(
            companyId,
            $"Agreement {companyId:N}",
            $"AGR-{companyId:N}",
            now));
        db.CompanyLocations.Add(CompanyLocation.Create(locationId, companyId, "Main", true, now));
        await db.SaveChangesAsync();
        return (companyId, locationId);
    }

    private async Task<Guid> CreateLocationAsync(Guid companyId)
    {
        var id = Guid.CreateVersion7();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.CompanyLocations.Add(CompanyLocation.Create(
            id,
            companyId,
            $"Location {id:N}",
            false,
            DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return id;
    }

    private async Task GiveLocationAccessAsync(Guid userId, Guid locationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.UserLocations.Add(new ApplicationUserLocation
        {
            UserId = userId,
            LocationId = locationId,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> MainLocationIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await db.CompanyLocations
            .Where(location => location.CompanyId == SqlServerApiFactory.DefaultCompanyId)
            .OrderByDescending(location => location.IsMain)
            .Select(location => location.Id)
            .FirstAsync();
    }

    private static async Task SignInAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = Password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await client.GetAsync("/api/auth/csrf");
        csrf.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add(
            "X-XSRF-TOKEN",
            document.RootElement.GetProperty("requestToken").GetString());
    }

    private static object ValidCreateRequest(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        IReadOnlyList<object>? lines = null) => new
        {
            agreementNumber = "AUTH-100",
            payerName = "Payer",
            insuredMemberId = "MEMBER-SENSITIVE",
            priorAuthorizationNumber = "PRIOR-SENSITIVE",
            startDate = startDate ?? new DateOnly(2026, 1, 1),
            endDate,
            status = "Active",
            priority = "High",
            isDummy = true,
            notes = "NOTES-SENSITIVE",
            lines = lines ?? [Line()],
        };

    private static object OverlapCreateRequest(
        string agreementNumber,
        string serviceCode,
        DateOnly startDate,
        DateOnly? endDate,
        string status = "Active") => new
        {
            agreementNumber,
            payerName = "Payer",
            insuredMemberId = (string?)null,
            priorAuthorizationNumber = (string?)null,
            startDate,
            endDate,
            status,
            priority = "Normal",
            isDummy = false,
            notes = (string?)null,
            lines = new[] { OverlapLine(null, serviceCode, startDate, endDate) },
        };

    private static object OverlapUpdateRequest(
        string editorVersion,
        Guid lineId,
        string agreementNumber,
        string serviceCode,
        DateOnly startDate,
        DateOnly? endDate) => new
        {
            agreementNumber,
            payerName = "Payer",
            insuredMemberId = (string?)null,
            priorAuthorizationNumber = (string?)null,
            startDate,
            endDate,
            status = "Active",
            priority = "Normal",
            isDummy = false,
            notes = (string?)null,
            editorVersion,
            lines = new[] { OverlapLine(lineId, serviceCode, startDate, endDate) },
        };

    private static object OverlapLine(
        Guid? id,
        string serviceCode,
        DateOnly startDate,
        DateOnly? endDate) => new
        {
            id,
            serviceCode,
            serviceName = "Overlap test service",
            unitType = "Hour",
            minutesPerUnit = 60,
            authorizedUnits = (decimal?)1,
            ratePerUnit = (decimal?)1,
            startDate,
            endDate,
            rateEffectiveFrom = (DateOnly?)null,
            rateEffectiveTo = (DateOnly?)null,
            authorizedBudgetAmount = (decimal?)null,
            plannedHoursPerVisit = (decimal?)null,
            plannedVisitsPerWeek = (decimal?)null,
            diagnoses = Array.Empty<object>(),
        };

    private static object ValidUpdateRequest(
        string editorVersion,
        IReadOnlyList<object> lines,
        string agreementNumber = "AUTH-100") => new
        {
            agreementNumber,
            payerName = "Updated payer",
            insuredMemberId = "MEMBER-SENSITIVE",
            priorAuthorizationNumber = "PRIOR-SENSITIVE",
            startDate = new DateOnly(2026, 1, 1),
            endDate = (DateOnly?)null,
            status = "Active",
            priority = "Urgent",
            isDummy = false,
            notes = "NOTES-SENSITIVE",
            editorVersion,
            lines,
        };

    private static object Line(
        Guid? id = null,
        string serviceName = "Personal care",
        string serviceCode = "T1019") => new
        {
            id,
            serviceCode,
            serviceName,
            unitType = "FifteenMinutes",
            minutesPerUnit = 15,
            authorizedUnits = 100m,
            ratePerUnit = 8m,
            startDate = new DateOnly(2026, 1, 1),
            endDate = (DateOnly?)null,
            rateEffectiveFrom = new DateOnly(2026, 1, 1),
            rateEffectiveTo = new DateOnly(2026, 12, 31),
            authorizedBudgetAmount = 900m,
            plannedHoursPerVisit = 2m,
            plannedVisitsPerWeek = 3m,
            diagnoses = new[]
            {
                new { diagnosisText = "DIAGNOSIS-SENSITIVE", icdCode = "ICD-SENSITIVE", isPrimary = true },
                new { diagnosisText = "ADDITIONAL-SENSITIVE", icdCode = "ICD-ADDITIONAL", isPrimary = false },
            },
        };

    private static string Route(Guid clientId) =>
        $"/api/clients/{clientId}/service-agreements";

    private static void AssertSensitiveValuesAbsent(AuditLog audit)
    {
        var persisted = string.Join(
            '|',
            audit.Action,
            audit.ResourceType,
            audit.ResourceId,
            audit.MetadataJson ?? string.Empty);
        foreach (var sensitive in new[]
        {
            "MEMBER-SENSITIVE",
            "PRIOR-SENSITIVE",
            "DIAGNOSIS-SENSITIVE",
            "ICD-SENSITIVE",
            "NOTES-SENSITIVE",
        })
        {
            Assert.DoesNotContain(sensitive, persisted, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record AuthorizedContext(
        ApplicationUser Actor,
        Guid LocationId,
        Guid ClientId,
        HttpClient HttpClient);

    private sealed record CreatedAgreement(
        Guid Id,
        string EditorVersion,
        IReadOnlyList<Guid> LineIds);
}
