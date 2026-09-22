using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareComply.Application.Authorization;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class EmployeeIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string Password = "correct horse battery staple";

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
        Assert.Contains(listJson.RootElement.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
        Assert.All(listJson.RootElement.EnumerateArray(), item =>
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
