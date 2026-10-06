using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Companies;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class EmployeeIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task CreateEmployeeEmitsSafeSuccessfulAuditEvent()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = EmployeeRequest(UniqueEmail("audit-create"), await MainLocationIdAsync());
        request["evvPassword"] = "audit-evv-secret";
        request["ivrPin"] = "9274";

        var response = await client.PostAsJsonAsync("/api/employees", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var responseJson = await ReadJsonAsync(response);
        var employeeId = responseJson.RootElement.GetProperty("id").GetGuid();
        var audit = await ReadEmployeeAuditAsync(actor.Id, "Employee.Created", employeeId);
        Assert.Equal("Employee", audit.ResourceType);
        Assert.Equal(employeeId.ToString("N"), audit.ResourceId);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        using var metadata = JsonDocument.Parse(audit.MetadataJson!);
        Assert.Equal(SqlServerApiFactory.DefaultCompanyId.ToString("N"), metadata.RootElement.GetProperty("companyId").GetString());
        Assert.Equal("1", metadata.RootElement.GetProperty("locationCount").GetString());
        Assert.Equal(2, metadata.RootElement.EnumerateObject().Count());
        AssertSensitiveAuditValuesAreAbsent(audit, "123456789", "audit-evv-secret", "9274");
    }

    [Fact]
    public async Task UpdateEmployeeEmitsSafeSuccessfulAuditEvent()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create, Permissions.Employee.Edit);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var locationId = await MainLocationIdAsync();
        var request = EmployeeRequest(UniqueEmail("audit-update"), locationId);
        var (employeeId, editorVersion) = await CreateEmployeeWithVersionAsync(client, request);
        request["editorVersion"] = editorVersion;
        request["lastName"] = "Updated";
        request["ssn"] = "987654321";
        request["evvPassword"] = "updated-evv-secret";
        request["ivrPin"] = "3141";

        var response = await client.PutAsJsonAsync($"/api/employees/{employeeId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var audit = await ReadEmployeeAuditAsync(actor.Id, "Employee.Updated", employeeId);
        Assert.Equal("Employee", audit.ResourceType);
        Assert.Equal(employeeId.ToString("N"), audit.ResourceId);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        using var metadata = JsonDocument.Parse(audit.MetadataJson!);
        Assert.Equal(SqlServerApiFactory.DefaultCompanyId.ToString("N"), metadata.RootElement.GetProperty("companyId").GetString());
        Assert.Equal("1", metadata.RootElement.GetProperty("locationCount").GetString());
        Assert.Equal(2, metadata.RootElement.EnumerateObject().Count());
        AssertSensitiveAuditValuesAreAbsent(audit, "987654321", "updated-evv-secret", "3141");
    }

    [Fact]
    public async Task FailedEmployeeCreateEmitsOnlyFailedAuditOutcome()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = EmployeeRequest(UniqueEmail("audit-failed"), Guid.NewGuid());
        request["evvPassword"] = "failed-evv-secret";
        request["ivrPin"] = "2718";

        var response = await client.PostAsJsonAsync("/api/employees", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var audits = await context.Set<AuditLog>().AsNoTracking()
            .Where(log => log.ActorUserId == actor.Id && log.Action == "Employee.Created")
            .OrderByDescending(log => log.OccurredAtUtc)
            .ToListAsync();
        var audit = Assert.Single(audits);
        Assert.Equal(AuditOutcome.Failed, audit.Outcome);
        Assert.Equal("not-created", audit.ResourceId);
        Assert.Equal("{\"reasonCode\":\"validation-failed\"}", audit.MetadataJson);
        Assert.DoesNotContain(audits, log => log.Outcome == AuditOutcome.Succeeded);
        AssertSensitiveAuditValuesAreAbsent(audit, "123456789", "failed-evv-secret", "2718");
    }

    [Fact]
    public async Task EmployeeCanBeCreatedListedReadAndUpdatedWithCurrentContracts()
    {
        var actor = await factory.CreateUserAsync($"employee-editor-{Guid.NewGuid():N}@example.test", Password);
        await factory.GrantPermissionAsync(actor.Id, Permissions.Employee.Create);
        await factory.GrantPermissionAsync(actor.Id, Permissions.Employee.View);
        await factory.GrantPermissionAsync(actor.Id, Permissions.Employee.Edit);

        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var locationId = await MainLocationIdAsync();
        var email = $"workforce-{Guid.NewGuid():N}@example.test";
        var request = EmployeeRequest(email, locationId);

        var created = await client.PostAsJsonAsync("/api/employees", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdJson.RootElement.GetProperty("id").GetGuid();
        request["editorVersion"] = createdJson.RootElement.GetProperty("editorVersion").GetString();
        Assert.Equal(email, createdJson.RootElement.GetProperty("email").GetString());
        Assert.True(createdJson.RootElement.GetProperty("hasSsn").GetBoolean());
        Assert.DoesNotContain("123456789", createdJson.RootElement.ToString());

        var read = await client.GetAsync($"/api/employees/{id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var readJson = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Equal(locationId, readJson.RootElement.GetProperty("locationIds")[0].GetGuid());

        var listed = await client.GetAsync($"/api/employees?search={Uri.EscapeDataString(email)}");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listJson = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var listedItems = listJson.RootElement.GetProperty("items").EnumerateArray();
        Assert.Contains(listedItems, item => item.GetProperty("id").GetGuid() == id);
        Assert.All(listJson.RootElement.GetProperty("items").EnumerateArray(), item =>
            Assert.Equal(SqlServerApiFactory.DefaultCompanyId, item.GetProperty("companyId").GetGuid()));

        var secondRequest = EmployeeRequest($"second-{Guid.NewGuid():N}@example.test", locationId);
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/employees", secondRequest)).StatusCode);

        request["lastName"] = "Updated";
        var updated = await client.PutAsJsonAsync($"/api/employees/{id}", request);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedJson = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal("Updated", updatedJson.RootElement.GetProperty("lastName").GetString());
    }

    [Fact]
    public async Task GetEmployeeReturnsOpaqueEditorVersion()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create, Permissions.Employee.View);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var (employeeId, createdVersion) = await CreateEmployeeWithVersionAsync(client);

        var response = await client.GetAsync($"/api/employees/{employeeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var editorVersion = json.RootElement.GetProperty("editorVersion").GetString();
        Assert.Equal(createdVersion, editorVersion);
        Assert.Matches("^[0-9A-F]{64}$", editorVersion!);
    }

    [Fact]
    public async Task CurrentEditorVersionUpdatesEmployeeAndReturnsNewVersion()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create, Permissions.Employee.Edit);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var locationId = await MainLocationIdAsync();
        var request = EmployeeRequest(UniqueEmail("concurrency-valid"), locationId);
        var (employeeId, editorVersion) = await CreateEmployeeWithVersionAsync(client, request);
        request["editorVersion"] = editorVersion;
        request["lastName"] = "Current version";

        var response = await client.PutAsJsonAsync($"/api/employees/{employeeId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("Current version", json.RootElement.GetProperty("lastName").GetString());
        var newVersion = json.RootElement.GetProperty("editorVersion").GetString();
        Assert.NotEqual(editorVersion, newVersion);
        Assert.Matches("^[0-9A-F]{64}$", newVersion!);
    }

    [Fact]
    public async Task UpdateRequiresEditorVersion()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create, Permissions.Employee.Edit);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = EmployeeRequest(UniqueEmail("concurrency-required"), await MainLocationIdAsync());
        var employeeId = await CreateEmployeeAsync(client, request);
        request["lastName"] = "Must not update";

        var response = await client.PutAsJsonAsync($"/api/employees/{employeeId}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await ReadJsonAsync(response);
        Assert.Equal(
            "Editor version is required and must contain 64 characters.",
            problem.RootElement.GetProperty("errors").GetProperty("EditorVersion")[0].GetString());
    }

    [Fact]
    public async Task StaleEditorVersionIsRepeatedlyRejectedWithoutOverwritingEmployee()
    {
        var actor = await CreateActorAsync(
            Permissions.Employee.Create,
            Permissions.Employee.View,
            Permissions.Employee.Edit);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = EmployeeRequest(UniqueEmail("concurrency-stale"), await MainLocationIdAsync());
        var (employeeId, staleVersion) = await CreateEmployeeWithVersionAsync(client, request);

        request["editorVersion"] = staleVersion;
        request["lastName"] = "Winning update";
        var successful = await client.PutAsJsonAsync($"/api/employees/{employeeId}", request);
        Assert.Equal(HttpStatusCode.OK, successful.StatusCode);
        using var successfulJson = await ReadJsonAsync(successful);
        var currentVersion = successfulJson.RootElement.GetProperty("editorVersion").GetString();

        request["editorVersion"] = staleVersion;
        request["lastName"] = "Stale overwrite";
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var stale = await client.PutAsJsonAsync($"/api/employees/{employeeId}", request);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            using var problem = await ReadJsonAsync(stale);
            Assert.Equal("urn:carecomply:problem:employee-conflict", problem.RootElement.GetProperty("type").GetString());
            Assert.Equal(409, problem.RootElement.GetProperty("status").GetInt32());
        }

        var read = await client.GetAsync($"/api/employees/{employeeId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var readJson = await ReadJsonAsync(read);
        Assert.Equal("Winning update", readJson.RootElement.GetProperty("lastName").GetString());
        Assert.Equal(currentVersion, readJson.RootElement.GetProperty("editorVersion").GetString());
    }

    [Fact]
    public async Task EmployeeListReturnsFirstPageAndTotalCount()
    {
        var (client, token) = await CreatePagingClientAsync();
        using (client)
        {
            await CreatePagingEmployeesAsync(client, token, "Able", "Baker", "Clark");

            var response = await client.GetAsync($"/api/employees?search={token}&page=1&pageSize=2");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            Assert.Equal(1, json.RootElement.GetProperty("pageNumber").GetInt32());
            Assert.Equal(2, json.RootElement.GetProperty("pageSize").GetInt32());
            Assert.Equal(3, json.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal(2, json.RootElement.GetProperty("totalPages").GetInt32());
            Assert.Equal(["Able", "Baker"], ListLastNames(json.RootElement));
        }
    }

    [Fact]
    public async Task EmployeeListReturnsSubsequentPage()
    {
        var (client, token) = await CreatePagingClientAsync();
        using (client)
        {
            await CreatePagingEmployeesAsync(client, token, "Able", "Baker", "Clark");

            var response = await client.GetAsync($"/api/employees?search={token}&page=2&pageSize=2");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            Assert.Equal(2, json.RootElement.GetProperty("pageNumber").GetInt32());
            Assert.Equal(["Clark"], ListLastNames(json.RootElement));
        }
    }

    [Fact]
    public async Task EmployeeListAppliesSearchBeforePaging()
    {
        var (client, token) = await CreatePagingClientAsync();
        using (client)
        {
            await CreatePagingEmployeesAsync(client, token, "One", "Two", "Three");
            await CreateEmployeeAsync(client, EmployeeRequest(UniqueEmail("unrelated-paging"), await MainLocationIdAsync()));

            var response = await client.GetAsync($"/api/employees?search={token}&page=1&pageSize=1");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            Assert.Equal(3, json.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal(3, json.RootElement.GetProperty("totalPages").GetInt32());
            Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        }
    }

    [Fact]
    public async Task EmployeeListPreservesCompanyIsolationWithPaging()
    {
        var token = $"paging-isolation-{Guid.NewGuid():N}";
        var (otherCompanyId, otherLocationId) = await CreateCompanyAsync();
        var administrator = await CreatePlatformAdministratorAsync(Permissions.Employee.Create);
        using var administratorClient = factory.CreateSecureClient();
        await SignInAsync(administratorClient, administrator.Email!);
        var otherRequest = EmployeeRequest($"{token}@example.test", otherLocationId);
        otherRequest["companyId"] = otherCompanyId;
        await CreateEmployeeAsync(administratorClient, otherRequest);

        var companyActor = await CreateActorAsync(Permissions.Employee.View);
        using var companyClient = factory.CreateSecureClient();
        await SignInAsync(companyClient, companyActor.Email!);
        var response = await companyClient.GetAsync($"/api/employees?search={token}&page=1&pageSize=25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task EmployeeListRejectsPageSizeAboveMaximum()
    {
        var actor = await CreateActorAsync(Permissions.Employee.View);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        var response = await client.GetAsync("/api/employees?page=1&pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(
            "Page size must be between 1 and 100.",
            json.RootElement.GetProperty("errors").GetProperty("pageSize")[0].GetString());
    }

    [Fact]
    public async Task EmployeeListOrderingIsDeterministicForEqualNames()
    {
        var (client, token) = await CreatePagingClientAsync();
        using (client)
        {
            await CreatePagingEmployeesAsync(client, token, "Same", "Same", "Same");

            var first = await client.GetAsync($"/api/employees?search={token}&page=1&pageSize=2");
            var second = await client.GetAsync($"/api/employees?search={token}&page=1&pageSize=2");

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            using var firstJson = await ReadJsonAsync(first);
            using var secondJson = await ReadJsonAsync(second);
            Assert.Equal(ListIds(firstJson.RootElement), ListIds(secondJson.RootElement));
            Assert.Equal(2, ListIds(firstJson.RootElement).Length);
        }
    }

    [Fact]
    public async Task EmployeeCreationRequiresPermissionAndValidCompanyLocation()
    {
        var actor = await factory.CreateUserAsync($"employee-denied-{Guid.NewGuid():N}@example.test", Password);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        var request = EmployeeRequest($"workforce-{Guid.NewGuid():N}@example.test", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/employees", request)).StatusCode);

        await factory.GrantPermissionAsync(actor.Id, Permissions.Employee.Create);
        // A fresh session observes the newly granted permission.
        using var authorizedClient = factory.CreateSecureClient();
        await SignInAsync(authorizedClient, actor.Email!);
        var invalid = await authorizedClient.PostAsJsonAsync("/api/employees", request);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task EmployeeValidationPreservesExistingKeysAndMessages()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = EmployeeRequest("not-an-email", Guid.NewGuid());
        request["firstName"] = string.Empty;
        request["ssn"] = string.Empty;

        var response = await client.PostAsJsonAsync("/api/employees", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var errors = json.RootElement.GetProperty("errors");
        Assert.Equal("First name is required.", errors.GetProperty("FirstName")[0].GetString());
        Assert.Equal("A valid email is required.", errors.GetProperty("Email")[0].GetString());
        Assert.Equal(
            "SSN is required when creating an employee.",
            errors.GetProperty("Ssn")[0].GetString());
        Assert.Equal(
            "Select at least one valid company location.",
            errors.GetProperty("LocationIds")[0].GetString());
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("ABC123456")]
    [InlineData("123-45-6789")]
    public async Task EmployeeSsnRequiresExactlyNineAsciiDigits(string ssn)
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = EmployeeRequest(UniqueEmail("invalid-ssn"), await MainLocationIdAsync());
        request["ssn"] = ssn;

        var response = await client.PostAsJsonAsync("/api/employees", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(
            "SSN must contain exactly 9 digits.",
            json.RootElement.GetProperty("errors").GetProperty("Ssn")[0].GetString());
    }

    [Fact]
    public async Task EmployeeAcceptsCanonicalSsnAndFormattedUsPhoneWithoutEvvOrIvr()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = EmployeeRequest(UniqueEmail("canonical-sensitive"), await MainLocationIdAsync());
        request["ssn"] = "123456789";
        request["phone"] = "(612) 555-0102";

        var response = await client.PostAsJsonAsync("/api/employees", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.True(json.RootElement.GetProperty("hasSsn").GetBoolean());
        Assert.False(json.RootElement.GetProperty("hasEvvPassword").GetBoolean());
        Assert.False(json.RootElement.GetProperty("hasIvrPin").GetBoolean());
        var employeeId = json.RootElement.GetProperty("id").GetGuid();
        var protectedValues = await ReadProtectedValuesAsync(employeeId);
        Assert.NotEqual("123456789", protectedValues.Ssn);
    }

    [Theory]
    [InlineData("612555010")]
    [InlineData("+1 (612) 555-0100")]
    [InlineData("612-CALL-NOW")]
    public async Task EmployeePhoneUsesSharedUsPhoneValidation(string phone)
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = EmployeeRequest(UniqueEmail("invalid-phone"), await MainLocationIdAsync());
        request["phone"] = phone;

        var response = await client.PostAsJsonAsync("/api/employees", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(
            "Enter a valid 10-digit US phone number.",
            json.RootElement.GetProperty("errors").GetProperty("Phone")[0].GetString());
    }

    [Fact]
    public async Task OmittedOptionalEmployeePhonesRemainValid()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = EmployeeRequest(UniqueEmail("blank-phones"), await MainLocationIdAsync());
        request["secondaryPhone"] = string.Empty;
        request["emergencyContactPhone"] = string.Empty;
        request["payerPhone"] = string.Empty;

        var response = await client.PostAsJsonAsync("/api/employees", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeViewPermissionIsRequiredForListAndGet()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var employeeId = await CreateEmployeeAsync(client);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/employees/{employeeId}")).StatusCode);
    }

    [Fact]
    public async Task EmployeeEditPermissionIsRequired()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create, Permissions.Employee.View);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var locationId = await MainLocationIdAsync();
        var request = EmployeeRequest(UniqueEmail("edit-denied"), locationId);
        var employeeId = await CreateEmployeeAsync(client, request);

        request["lastName"] = "Denied update";
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"/api/employees/{employeeId}", request)).StatusCode);
    }

    [Fact]
    public async Task CompanyUserCannotReadOrUpdateAnotherCompanyEmployee()
    {
        var (otherCompanyId, otherLocationId) = await CreateCompanyAsync();
        var administrator = await CreatePlatformAdministratorAsync(
            Permissions.Employee.Create,
            Permissions.Employee.View,
            Permissions.Employee.Edit);
        using var administratorClient = factory.CreateSecureClient();
        await SignInAsync(administratorClient, administrator.Email!);
        var otherRequest = EmployeeRequest(UniqueEmail("other-company"), otherLocationId);
        otherRequest["companyId"] = otherCompanyId;
        var employeeId = await CreateEmployeeAsync(administratorClient, otherRequest);

        var companyActor = await CreateActorAsync(Permissions.Employee.View, Permissions.Employee.Edit);
        using var companyClient = factory.CreateSecureClient();
        await SignInAsync(companyClient, companyActor.Email!);

        Assert.Equal(HttpStatusCode.NotFound, (await companyClient.GetAsync($"/api/employees/{employeeId}")).StatusCode);
        otherRequest["lastName"] = "Cross-company update";
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await companyClient.PutAsJsonAsync($"/api/employees/{employeeId}", otherRequest)).StatusCode);
    }

    [Fact]
    public async Task PlatformAdministratorCanSelectEmployeeCompanyAndItsLocation()
    {
        var (companyId, locationId) = await CreateCompanyAsync();
        var administrator = await CreatePlatformAdministratorAsync(
            Permissions.Employee.Create,
            Permissions.Employee.View);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, administrator.Email!);
        var request = EmployeeRequest(UniqueEmail("platform-company"), locationId);
        request["companyId"] = companyId;

        var response = await client.PostAsJsonAsync("/api/employees", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(companyId, json.RootElement.GetProperty("companyId").GetGuid());
        Assert.Equal(locationId, json.RootElement.GetProperty("locationIds")[0].GetGuid());
    }

    [Fact]
    public async Task DuplicateEmployeeEmailIsRejected()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var locationId = await MainLocationIdAsync();
        var email = UniqueEmail("duplicate");

        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/employees", EmployeeRequest(email, locationId))).StatusCode);
        var duplicate = await client.PostAsJsonAsync("/api/employees", EmployeeRequest(email, locationId));

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using var json = await ReadJsonAsync(duplicate);
        Assert.Contains(
            json.RootElement.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, "Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SensitiveValuesAreNotReturnedAndBlankUpdatePreservesStoredValues()
    {
        var actor = await CreateActorAsync(
            Permissions.Employee.Create,
            Permissions.Employee.View,
            Permissions.Employee.Edit);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var locationId = await MainLocationIdAsync();
        var request = EmployeeRequest(UniqueEmail("sensitive"), locationId);
        request["evvPassword"] = "Secret EVV password";
        request["ivrPin"] = "8462";
        var created = await client.PostAsJsonAsync("/api/employees", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = await ReadJsonAsync(created);
        var employeeId = createdJson.RootElement.GetProperty("id").GetGuid();
        request["editorVersion"] = createdJson.RootElement.GetProperty("editorVersion").GetString();

        AssertSensitiveResponseIsRedacted(createdJson.RootElement);
        var before = await ReadProtectedValuesAsync(employeeId);

        request["ssn"] = string.Empty;
        request["evvPassword"] = string.Empty;
        request["ivrPin"] = string.Empty;
        var updated = await client.PutAsJsonAsync($"/api/employees/{employeeId}", request);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedJson = await ReadJsonAsync(updated);
        AssertSensitiveResponseIsRedacted(updatedJson.RootElement);
        var after = await ReadProtectedValuesAsync(employeeId);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task OmittedEvvAndIvrFieldsPreserveHistoricalValuesDuringEdit()
    {
        var actor = await CreateActorAsync(
            Permissions.Employee.Create,
            Permissions.Employee.Edit);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = EmployeeRequest(UniqueEmail("preserve-evv"), await MainLocationIdAsync());
        request["allowAggregator"] = true;
        request["overrideRestriction"] = true;
        request["evvOnly"] = true;
        request["restrictDateAndTime"] = true;
        request["evvEmail"] = "historical-evv@example.test";
        request["evvPassword"] = "Historical EVV password";
        request["ivrPin"] = "7531";
        request["evvIvrAuthorization"] = "EVV and IVR";
        var created = await client.PostAsJsonAsync("/api/employees", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = await ReadJsonAsync(created);
        var employeeId = createdJson.RootElement.GetProperty("id").GetGuid();
        request["editorVersion"] = createdJson.RootElement.GetProperty("editorVersion").GetString();
        var before = await ReadEvvStateAsync(employeeId);

        foreach (var key in new[]
        {
            "allowAggregator", "overrideRestriction", "evvOnly", "restrictDateAndTime",
            "evvEmail", "evvPassword", "ivrPin", "evvIvrAuthorization",
        })
        {
            request.Remove(key);
        }
        request["lastName"] = "Unrelated update";

        var updated = await client.PutAsJsonAsync($"/api/employees/{employeeId}", request);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(before, await ReadEvvStateAsync(employeeId));
    }

    [Fact]
    public async Task EmployeeCollectionsRoundTripWithCurrentContract()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create, Permissions.Employee.View);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = EmployeeRequest(UniqueEmail("collections"), await MainLocationIdAsync());
        request["contacts"] = new[]
        {
            new { name = "Jordan Contact", phone = "6515550101", relationship = "Sibling" },
        };
        request["trainingInformation"] = new
        {
            courses = new[]
            {
                new { courseName = "HIPAA and privacy", qualifyingPoints = 4.5m, completedOn = "2026-08-01", obtainedPoints = 4m, notes = "Complete" },
            },
            trainings = new[]
            {
                new { trainingName = "PCA certification", jobTitle = "Caregiver", qualifyingPoints = 10m, completedOn = "2026-08-02", expiresOn = "2027-08-02", obtainedPoints = 9m, notes = "Current", allowedTraining = true },
            },
        };
        request["backgroundInformation"] = new
        {
            checks = new[]
            {
                new { checkDate = "2026-07-01", receivedDate = "2026-07-03", licenseType = "Driver license", studyType = "Driving record", received = true },
            },
            mantouxGivenDate = "2026-07-04",
            mantouxCheckedDate = "2026-07-06",
            mantouxReceived = true,
            medicalTestingDate = "2026-07-07",
            medicalTestingReceived = true,
        };
        request["insuranceAffiliationRequired"] = true;
        request["payerAffiliations"] = new[]
        {
            new { company = "Test Insurance", payer = "Test Payer", payerId = "PAY-100", applicationFaxedDate = "2026-06-01", affiliationDate = "2026-06-02", affiliationEndDate = "2027-06-02" },
        };
        request["previousAddresses"] = new[]
        {
            new { street = "200 Previous Street", postalCode = "55101", city = "Saint Paul", county = "Ramsey", state = "MN" },
        };

        var employeeId = await CreateEmployeeAsync(client, request);
        var response = await client.GetAsync($"/api/employees/{employeeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;
        Assert.Equal("Jordan Contact", root.GetProperty("contacts")[0].GetProperty("name").GetString());
        Assert.Equal("HIPAA and privacy", root.GetProperty("trainingInformation").GetProperty("courses")[0].GetProperty("courseName").GetString());
        Assert.Equal("PCA certification", root.GetProperty("trainingInformation").GetProperty("trainings")[0].GetProperty("trainingName").GetString());
        Assert.Equal("Driving record", root.GetProperty("backgroundInformation").GetProperty("checks")[0].GetProperty("studyType").GetString());
        Assert.Equal("Test Payer", root.GetProperty("payerAffiliations")[0].GetProperty("payer").GetString());
        Assert.Equal("200 Previous Street", root.GetProperty("previousAddresses")[0].GetProperty("street").GetString());
    }

    [Fact]
    public async Task MultipleLocationAssignmentsArePreserved()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create, Permissions.Employee.View);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var mainLocationId = await MainLocationIdAsync();
        var additionalLocationId = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        var request = EmployeeRequest(UniqueEmail("locations"), mainLocationId);
        request["locationIds"] = new[] { mainLocationId, additionalLocationId };

        var employeeId = await CreateEmployeeAsync(client, request);
        var response = await client.GetAsync($"/api/employees/{employeeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var actual = json.RootElement.GetProperty("locationIds").EnumerateArray().Select(item => item.GetGuid()).ToHashSet();
        Assert.Equal(new HashSet<Guid> { mainLocationId, additionalLocationId }, actual);
    }

    private async Task<ApplicationUser> CreateActorAsync(params string[] permissions)
    {
        var actor = await factory.CreateUserAsync(UniqueEmail("employee-actor"), Password);
        foreach (var permission in permissions)
        {
            await factory.GrantPermissionAsync(actor.Id, permission);
        }

        return actor;
    }

    private async Task<ApplicationUser> CreatePlatformAdministratorAsync(params string[] permissions)
    {
        var actor = await factory.CreateUserAsync(UniqueEmail("platform-actor"), Password);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var storedActor = await db.Users.SingleAsync(user => user.Id == actor.Id);
            storedActor.CompanyId = null;
            storedActor.IsPlatformAdministrator = true;
            await db.SaveChangesAsync();
        }

        foreach (var permission in permissions)
        {
            await factory.GrantPermissionAsync(actor.Id, permission);
        }

        return actor;
    }

    private async Task<Guid> CreateEmployeeAsync(
        HttpClient client,
        Dictionary<string, object?>? request = null)
    {
        var (employeeId, _) = await CreateEmployeeWithVersionAsync(client, request);
        return employeeId;
    }

    private async Task<(Guid EmployeeId, string EditorVersion)> CreateEmployeeWithVersionAsync(
        HttpClient client,
        Dictionary<string, object?>? request = null)
    {
        request ??= EmployeeRequest(UniqueEmail("workforce"), await MainLocationIdAsync());
        var response = await client.PostAsJsonAsync("/api/employees", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return (
            json.RootElement.GetProperty("id").GetGuid(),
            json.RootElement.GetProperty("editorVersion").GetString()!);
    }

    private async Task<(Guid CompanyId, Guid LocationId)> CreateCompanyAsync()
    {
        var companyId = Guid.CreateVersion7();
        var locationId = Guid.CreateVersion7();
        var suffix = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.Companies.Add(Company.Create(companyId, $"Employee Test {suffix}", $"EMP-{suffix}", now));
        db.CompanyLocations.Add(CompanyLocation.Create(locationId, companyId, "Main", true, now));
        await db.SaveChangesAsync();
        return (companyId, locationId);
    }

    private async Task<(HttpClient Client, string SearchToken)> CreatePagingClientAsync()
    {
        var actor = await CreateActorAsync(Permissions.Employee.Create, Permissions.Employee.View);
        var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        return (client, $"paging-{Guid.NewGuid():N}");
    }

    private async Task CreatePagingEmployeesAsync(
        HttpClient client,
        string searchToken,
        params string[] lastNames)
    {
        var locationId = await MainLocationIdAsync();
        for (var index = 0; index < lastNames.Length; index++)
        {
            var request = EmployeeRequest($"{searchToken}-{index}@example.test", locationId);
            request["firstName"] = "Paging";
            request["lastName"] = lastNames[index];
            await CreateEmployeeAsync(client, request);
        }
    }

    private static string[] ListLastNames(JsonElement response) => response
        .GetProperty("items")
        .EnumerateArray()
        .Select(item => item.GetProperty("lastName").GetString()!)
        .ToArray();

    private static Guid[] ListIds(JsonElement response) => response
        .GetProperty("items")
        .EnumerateArray()
        .Select(item => item.GetProperty("id").GetGuid())
        .ToArray();

    private async Task<Guid> CreateLocationAsync(Guid companyId)
    {
        var locationId = Guid.CreateVersion7();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.CompanyLocations.Add(CompanyLocation.Create(
            locationId,
            companyId,
            $"Employee Location {Guid.NewGuid():N}",
            false,
            DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return locationId;
    }

    private async Task<(string Ssn, string EvvPassword, string IvrPin)> ReadProtectedValuesAsync(Guid employeeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await db.Users.AsNoTracking()
            .Where(user => user.Id == employeeId)
            .Select(user => new ValueTuple<string, string, string>(
                user.SsnProtected,
                user.EvvPasswordProtected,
                user.IvrPinProtected))
            .SingleAsync();
    }

    private async Task<(bool AllowAggregator, bool OverrideRestriction, bool EvvOnly,
        bool RestrictDateAndTime, string EvvEmail, string EvvPassword, string IvrPin,
        string Authorization)> ReadEvvStateAsync(Guid employeeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var state = await db.Users.AsNoTracking()
            .Where(user => user.Id == employeeId)
            .Select(user => new
            {
                user.AllowAggregator,
                user.OverrideRestriction,
                user.EvvOnly,
                user.RestrictDateAndTime,
                user.EvvEmail,
                EvvPassword = user.EvvPasswordProtected,
                IvrPin = user.IvrPinProtected,
                Authorization = user.EvvIvrAuthorization,
            })
            .SingleAsync();
        return (
            state.AllowAggregator,
            state.OverrideRestriction,
            state.EvvOnly,
            state.RestrictDateAndTime,
            state.EvvEmail,
            state.EvvPassword,
            state.IvrPin,
            state.Authorization);
    }

    private async Task<AuditLog> ReadEmployeeAuditAsync(
        Guid actorUserId,
        string action,
        Guid employeeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await context.Set<AuditLog>().AsNoTracking().SingleAsync(log =>
            log.ActorUserId == actorUserId
            && log.Action == action
            && log.ResourceId == employeeId.ToString("N"));
    }

    private static void AssertSensitiveAuditValuesAreAbsent(
        AuditLog audit,
        params string[] sensitiveValues)
    {
        var serializedAudit = string.Join('|',
            audit.Action,
            audit.ResourceType,
            audit.ResourceId,
            audit.MetadataJson ?? string.Empty);
        foreach (var sensitiveValue in sensitiveValues)
        {
            Assert.DoesNotContain(sensitiveValue, serializedAudit, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("ssn", serializedAudit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", serializedAudit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ivr", serializedAudit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", serializedAudit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("firstName", serializedAudit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lastName", serializedAudit, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertSensitiveResponseIsRedacted(JsonElement response)
    {
        Assert.True(response.GetProperty("hasSsn").GetBoolean());
        Assert.True(response.GetProperty("hasEvvPassword").GetBoolean());
        Assert.True(response.GetProperty("hasIvrPin").GetBoolean());
        var json = response.GetRawText();
        Assert.DoesNotContain("123456789", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret EVV password", json, StringComparison.Ordinal);
        Assert.DoesNotContain("8462", json, StringComparison.Ordinal);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@example.test";

    private async Task<Guid> MainLocationIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await db.CompanyLocations.Where(location => location.CompanyId == SqlServerApiFactory.DefaultCompanyId)
            .OrderByDescending(location => location.IsMain).Select(location => location.Id).FirstAsync();
    }

    private static async Task SignInAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await client.GetAsync("/api/auth/csrf");
        csrf.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", document.RootElement.GetProperty("requestToken").GetString());
    }

    private static Dictionary<string, object?> EmployeeRequest(string email, Guid locationId) => new()
    {
        ["locationIds"] = new[] { locationId },
        ["firstName"] = "Taylor",
        ["lastName"] = "Workforce",
        ["email"] = email,
        ["jobTitle"] = "Caregiver",
        ["isActive"] = true,
        ["hireDate"] = "2026-09-01",
        ["dateOfBirth"] = "1990-04-12",
        ["gender"] = "Female",
        ["phone"] = "6125550102",
        ["streetAddress"] = "100 Test Street",
        ["ssn"] = "123456789",
        ["canWorkWithSmokers"] = true,
        ["canWorkWithPets"] = true,
    };
}
