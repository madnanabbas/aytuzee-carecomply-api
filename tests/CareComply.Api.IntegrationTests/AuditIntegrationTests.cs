using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class AuditIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string ValidPassword = "correct horse battery staple";
    private const string PermissionMigration = "20260816105206_AddPermissionAuthorization";

    [Fact]
    public async Task MigrationUpgradesPermissionSchemaAndCreatesOnlyApprovedAuditModel()
    {
        var connectionString = await factory.CreateIsolatedDatabaseAsync();
        var options = new DbContextOptionsBuilder<CareComplyDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var context = new CareComplyDbContext(options);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(PermissionMigration);
        Assert.False(await TableExistsAsync(context, "AuditLogs"));

        await migrator.MigrateAsync();

        Assert.True(await TableExistsAsync(context, "AuditLogs"));
        Assert.Equal(
            [
                "IX_AuditLogs_ActorUserId_OccurredAtUtc",
                "IX_AuditLogs_OccurredAtUtc",
                "IX_AuditLogs_ResourceType_ResourceId_OccurredAtUtc",
                "IX_AuditLogs_TraceId",
            ],
            await GetNamedValuesAsync(
                context,
                "SELECT [name] FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AuditLogs') AND [name] LIKE 'IX[_]%' ORDER BY [name]"));
        Assert.Equal(
            ["CK_AuditLogs_MetadataJson", "CK_AuditLogs_Outcome"],
            await GetNamedValuesAsync(
                context,
                "SELECT [name] FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.AuditLogs') ORDER BY [name]"));
        Assert.Equal(
            ["FK_AuditLogs_Users_ActorUserId"],
            await GetNamedValuesAsync(
                context,
                "SELECT [name] FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.AuditLogs') ORDER BY [name]"));
    }

    [Fact]
    public async Task SuccessfulMutationCommitsBusinessStateAndOneSafeAuditRow()
    {
        var access = await CreateAuthorizedAccessAsync();
        var resourceId = Guid.CreateVersion7();
        var unapprovedPayload = $"unapproved-request-{Guid.NewGuid():N}";
        var traceId = Guid.NewGuid().ToString("N");
        var before = DateTimeOffset.UtcNow;

        var response = await SendAuditAsync(
            access.Client,
            access.CsrfToken,
            TestAuditModes.Success,
            resourceId,
            unapprovedPayload,
            traceId);

        var after = DateTimeOffset.UtcNow;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.True(await context.Roles.AsNoTracking().AnyAsync(role => role.Id == resourceId));
        var auditLog = Assert.Single(await context.Set<AuditLog>()
            .AsNoTracking()
            .Where(log => log.ResourceId == resourceId.ToString("N"))
            .ToArrayAsync());
        Assert.Equal(access.UserId, auditLog.ActorUserId);
        Assert.Equal("AuditFoundation.Executed", auditLog.Action);
        Assert.Equal("AuditFoundation", auditLog.ResourceType);
        Assert.Equal(AuditOutcome.Succeeded, auditLog.Outcome);
        Assert.Equal(traceId, auditLog.TraceId);
        Assert.InRange(auditLog.OccurredAtUtc, before, after);
        Assert.Contains(TestAuditModes.Success, auditLog.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain(unapprovedPayload, FormatAuditLog(auditLog), StringComparison.Ordinal);
        Assert.DoesNotContain(ValidPassword, FormatAuditLog(auditLog), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpectedFailedResultCommitsAuditWithoutBusinessMutation()
    {
        var access = await CreateAuthorizedAccessAsync();
        var resourceId = Guid.CreateVersion7();

        var response = await SendAuditAsync(
            access.Client,
            access.CsrfToken,
            TestAuditModes.FailedClean,
            resourceId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertFailedAuditWithoutRoleAsync(resourceId, TestAuditModes.FailedClean);
    }

    [Fact]
    public async Task FailedResultRollsBackUnintendedSavedMutationAndCommitsOnlyFailedAudit()
    {
        var access = await CreateAuthorizedAccessAsync();
        var resourceId = Guid.CreateVersion7();

        var response = await SendAuditAsync(
            access.Client,
            access.CsrfToken,
            TestAuditModes.FailedMutated,
            resourceId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertFailedAuditWithoutRoleAsync(resourceId, TestAuditModes.FailedMutated);
    }

    [Fact]
    public async Task UnexpectedExceptionRollsBackBusinessMutationAndCreatesNoAudit()
    {
        var access = await CreateAuthorizedAccessAsync();
        var resourceId = Guid.CreateVersion7();

        var response = await SendAuditAsync(
            access.Client,
            access.CsrfToken,
            TestAuditModes.UnexpectedException,
            resourceId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(
            TestAuditModes.InternalExceptionMessage,
            responseBody,
            StringComparison.Ordinal);
        await AssertNoRoleOrAuditAsync(resourceId);
    }

    [Fact]
    public async Task AuditPersistenceFailureRollsBackBusinessMutation()
    {
        var access = await CreateAuthorizedAccessAsync();
        var resourceId = Guid.CreateVersion7();

        var response = await SendAuditAsync(
            access.Client,
            access.CsrfToken,
            TestAuditModes.AuditPersistenceFailure,
            resourceId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertNoRoleOrAuditAsync(resourceId);
    }

    [Fact]
    public async Task ValidationAndAuthorizationFailuresCreateNoBusinessAudit()
    {
        var authorized = await CreateAuthorizedAccessAsync();
        var invalidResourceId = Guid.CreateVersion7();
        var invalidResponse = await SendAuditAsync(
            authorized.Client,
            authorized.CsrfToken,
            string.Empty,
            invalidResourceId);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        await AssertNoRoleOrAuditAsync(invalidResourceId);

        var user = await factory.CreateUserAsync(
            $"audit-denied-{Guid.NewGuid():N}@example.test",
            ValidPassword);
        using var deniedClient = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await LoginAsync(deniedClient, user.Email!)).StatusCode);
        var deniedResourceId = Guid.CreateVersion7();
        var deniedResponse = await SendAuditAsync(
            deniedClient,
            await GetCsrfTokenAsync(deniedClient),
            TestAuditModes.Success,
            deniedResourceId);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
        await AssertNoRoleOrAuditAsync(deniedResourceId);

        using var anonymousClient = factory.CreateSecureClient();
        var anonymousResponse = await anonymousClient.PostAsJsonAsync(
            "/api/test/audit",
            new TestAuditRequest
            {
                Mode = TestAuditModes.Success,
                ResourceId = Guid.CreateVersion7(),
            });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    [Fact]
    public async Task NonAuditableRequestAndAuthenticationDoNotCreateAuditRows()
    {
        var before = await CountAuditRowsAsync();
        var access = await CreateAuthorizedAccessAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/test/audit/non-auditable");
        request.Headers.Add("X-XSRF-TOKEN", access.CsrfToken);

        var response = await access.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(before, await CountAuditRowsAsync());
    }

    [Fact]
    public async Task ExplicitAuditableReadCreatesOneAuditWithoutBusinessMutation()
    {
        var access = await CreateAuthorizedAccessAsync();
        var resourceId = Guid.CreateVersion7();

        var response = await SendAuditAsync(
            access.Client,
            access.CsrfToken,
            TestAuditModes.Read,
            resourceId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.False(await context.Roles.AsNoTracking().AnyAsync(role => role.Id == resourceId));
        var auditLog = Assert.Single(await context.Set<AuditLog>()
            .AsNoTracking()
            .Where(log => log.ResourceId == resourceId.ToString("N"))
            .ToArrayAsync());
        Assert.Equal(AuditOutcome.Succeeded, auditLog.Outcome);
        Assert.Contains(TestAuditModes.Read, auditLog.MetadataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditRowsAreImmutableAndPreventActorDeletion()
    {
        var access = await CreateAuthorizedAccessAsync();
        var resourceId = Guid.CreateVersion7();
        Assert.Equal(
            HttpStatusCode.OK,
            (await SendAuditAsync(
                access.Client,
                access.CsrfToken,
                TestAuditModes.Success,
                resourceId)).StatusCode);

        await using (var updateScope = factory.Services.CreateAsyncScope())
        {
            var context = updateScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var auditLog = await context.Set<AuditLog>().SingleAsync(
                log => log.ResourceId == resourceId.ToString("N"));
            context.Entry(auditLog).Property(log => log.Action).CurrentValue = "AuditFoundation.Changed";
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }

        await using (var deleteScope = factory.Services.CreateAsyncScope())
        {
            var context = deleteScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var auditLog = await context.Set<AuditLog>().SingleAsync(
                log => log.ResourceId == resourceId.ToString("N"));
            context.Remove(auditLog);
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }

        await using var actorScope = factory.Services.CreateAsyncScope();
        var actorContext = actorScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var actor = await actorContext.Users.SingleAsync(user => user.Id == access.UserId);
        actorContext.Remove(actor);
        await Assert.ThrowsAsync<DbUpdateException>(() => actorContext.SaveChangesAsync());
    }

    [Fact]
    public async Task SensitiveAndArbitraryInputsAreNeverPersisted()
    {
        var access = await CreateAuthorizedAccessAsync();
        var resourceId = Guid.CreateVersion7();
        var sensitiveSentinel = string.Join(
            '-',
            "password",
            "cookie",
            "antiforgery",
            "token",
            "request-body",
            Guid.NewGuid().ToString("N"));

        Assert.Equal(
            HttpStatusCode.OK,
            (await SendAuditAsync(
                access.Client,
                access.CsrfToken,
                TestAuditModes.Success,
                resourceId,
                sensitiveSentinel)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var auditLog = await context.Set<AuditLog>().AsNoTracking().SingleAsync(
            log => log.ResourceId == resourceId.ToString("N"));
        var persisted = FormatAuditLog(auditLog);
        Assert.DoesNotContain(sensitiveSentinel, persisted, StringComparison.Ordinal);
        Assert.DoesNotContain(ValidPassword, persisted, StringComparison.Ordinal);
        Assert.DoesNotContain(TestAuditModes.InternalExceptionMessage, persisted, StringComparison.Ordinal);
    }

    private async Task<AuthorizedAccess> CreateAuthorizedAccessAsync()
    {
        var user = await factory.CreateUserAsync(
            $"audit-user-{Guid.NewGuid():N}@example.test",
            ValidPassword);
        await GrantPermissionAsync(user, Permissions.Client.Create);
        var client = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await LoginAsync(client, user.Email!)).StatusCode);
        return new AuthorizedAccess(user.Id, client, await GetCsrfTokenAsync(client));
    }

    private async Task GrantPermissionAsync(ApplicationUser user, string permissionName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var trackedUser = await userManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(trackedUser);
        var role = new ApplicationRole { Name = $"AuditRole-{Guid.NewGuid():N}" };
        Assert.True((await roleManager.CreateAsync(role)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(trackedUser, role.Name!)).Succeeded);
        var permissionId = await context.Set<Permission>()
            .Where(permission => permission.Name == permissionName)
            .Select(permission => permission.Id)
            .SingleAsync();
        context.Add(new RolePermission(role.Id, permissionId));
        await context.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> SendAuditAsync(
        HttpClient client,
        string csrfToken,
        string mode,
        Guid resourceId,
        string? unapprovedPayload = null,
        string? traceId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/test/audit")
        {
            Content = JsonContent.Create(new TestAuditRequest
            {
                Mode = mode,
                ResourceId = resourceId,
                UnapprovedPayload = unapprovedPayload,
            }),
        };
        request.Headers.Add("X-XSRF-TOKEN", csrfToken);
        if (traceId is not null)
        {
            request.Headers.TryAddWithoutValidation(
                "traceparent",
                $"00-{traceId}-0123456789abcdef-01");
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email) =>
        await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = ValidPassword });

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("requestToken").GetString()
            ?? throw new InvalidOperationException("The CSRF endpoint did not return a token.");
    }

    private async Task AssertFailedAuditWithoutRoleAsync(Guid resourceId, string expectedMode)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.False(await context.Roles.AsNoTracking().AnyAsync(role => role.Id == resourceId));
        var auditLog = Assert.Single(await context.Set<AuditLog>()
            .AsNoTracking()
            .Where(log => log.ResourceId == resourceId.ToString("N"))
            .ToArrayAsync());
        Assert.Equal(AuditOutcome.Failed, auditLog.Outcome);
        Assert.Contains(expectedMode, auditLog.MetadataJson, StringComparison.Ordinal);
    }

    private async Task AssertNoRoleOrAuditAsync(Guid resourceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.False(await context.Roles.AsNoTracking().AnyAsync(role => role.Id == resourceId));
        Assert.False(await context.Set<AuditLog>().AsNoTracking().AnyAsync(
            log => log.ResourceId == resourceId.ToString("N")));
    }

    private async Task<int> CountAuditRowsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await context.Set<AuditLog>().CountAsync();
    }

    private static string FormatAuditLog(AuditLog auditLog) => string.Join(
        '|',
        auditLog.Action,
        auditLog.ResourceType,
        auditLog.ResourceId,
        auditLog.Outcome.ToString(),
        auditLog.TraceId,
        auditLog.MetadataJson);

    private static async Task<bool> TableExistsAsync(
        CareComplyDbContext context,
        string tableName)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE [name] = @tableName AND schema_id = SCHEMA_ID('dbo')";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(),
            CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<string[]> GetNamedValuesAsync(
        CareComplyDbContext context,
        string sql)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values.ToArray();
    }

    private sealed record AuthorizedAccess(
        Guid UserId,
        HttpClient Client,
        string CsrfToken);
}
