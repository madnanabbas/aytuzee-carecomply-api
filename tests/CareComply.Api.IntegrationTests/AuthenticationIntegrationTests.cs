using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using CareComply.Api.Contracts.Authentication;
using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;

namespace CareComply.Api.IntegrationTests;

public sealed class AuthenticationIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string ValidPassword = "correct horse battery staple";

    [Fact]
    public async Task IdentityHashesAndVerifiesPasswordsInSqlServer()
    {
        var user = await CreateUserAsync();
        var storedUser = await factory.GetUserAsync(user.Id);

        Assert.NotNull(storedUser.PasswordHash);
        Assert.NotEqual(ValidPassword, storedUser.PasswordHash);

        using var scope = factory.Services.CreateScope();
        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<ApplicationUser>>();
        var verification = passwordHasher.VerifyHashedPassword(
            storedUser,
            storedUser.PasswordHash,
            ValidPassword);

        Assert.NotEqual(PasswordVerificationResult.Failed, verification);
    }

    [Fact]
    public async Task ValidLoginIssuesSecureCookieAndMeReturnsApprovedContract()
    {
        var user = await CreateUserAsync();
        using var client = factory.CreateSecureClient();

        var loginResponse = await LoginAsync(client, user.Email!, ValidPassword);

        Assert.Equal(HttpStatusCode.NoContent, loginResponse.StatusCode);
        var cookie = Assert.Single(
            loginResponse.Headers.GetValues("Set-Cookie"), 
            value => value.StartsWith("__Host-CareComply.Auth=", StringComparison.Ordinal));
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=none", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Permissions.Client.View, cookie, StringComparison.Ordinal);

        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.DoesNotContain(
            meResponse.Headers.TryGetValues("Set-Cookie", out var values) ? values : [],
            value => value.StartsWith("__Host-CareComply.Auth=", StringComparison.Ordinal));
        var currentUser = await meResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(currentUser);
        Assert.Equal(user.Id, currentUser.Id);
        Assert.Equal(user.Email, currentUser.Email);
        Assert.Equal("Test User", currentUser.DisplayName);
        Assert.Empty(currentUser.Permissions);
    }

    [Fact]
    public async Task InvalidAndNonexistentUsersReceiveSameSafeFailure()
    {
        var user = await CreateUserAsync();
        using var invalidPasswordClient = factory.CreateSecureClient();
        using var nonexistentUserClient = factory.CreateSecureClient();

        var invalidPasswordResponse = await LoginAsync(
            invalidPasswordClient,
            user.Email!,
            "this password is incorrect");
        var nonexistentUserResponse = await LoginAsync(
            nonexistentUserClient,
            $"missing-{Guid.NewGuid():N}@example.test",
            "this password is incorrect");

        await AssertSafeAuthenticationFailureAsync(invalidPasswordResponse);
        await AssertSafeAuthenticationFailureAsync(nonexistentUserResponse);
    }

    [Fact]
    public async Task InactiveUserCannotLoginWithCorrectPassword()
    {
        var user = await CreateUserAsync(isActive: false);
        using var client = factory.CreateSecureClient();

        var response = await LoginAsync(client, user.Email!, ValidPassword);

        await AssertSafeAuthenticationFailureAsync(response);
    }

    [Fact]
    public async Task FiveInvalidPasswordsLockTheAccount()
    {
        var user = await CreateUserAsync();
        using var client = factory.CreateSecureClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await LoginAsync(client, user.Email!, "this password is incorrect");
            await AssertSafeAuthenticationFailureAsync(response);
        }

        var storedUser = await factory.GetUserAsync(user.Id);
        Assert.NotNull(storedUser.LockoutEnd);
        Assert.True(storedUser.LockoutEnd > DateTimeOffset.UtcNow);

        var correctPasswordResponse = await LoginAsync(client, user.Email!, ValidPassword);
        await AssertSafeAuthenticationFailureAsync(correctPasswordResponse);
    }

    [Fact]
    public async Task MeRequiresAuthentication()
    {
        using var client = factory.CreateSecureClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "urn:carecomply:problem:authentication-required",
            document.RootElement.GetProperty("type").GetString());
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task AuthenticatedActorWhoseUserNoLongerExistsReceivesProblemDetails()
    {
        using var missingUserFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIdentityUserReader>();
                services.AddScoped<IIdentityUserReader, MissingIdentityUserReader>();
            }));
        using var client = missingUserFactory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
        var user = await CreateUserAsync();
        var loginResponse = await LoginAsync(client, user.Email!, ValidPassword);
        Assert.Equal(HttpStatusCode.NoContent, loginResponse.StatusCode);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "urn:carecomply:problem:authentication-required",
            document.RootElement.GetProperty("type").GetString());
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task LogoutRequiresCsrfAndEndsOnlyCurrentSession()
    {
        var user = await CreateUserAsync();
        using var client = factory.CreateSecureClient();
        var loginResponse = await LoginAsync(client, user.Email!, ValidPassword);
        Assert.Equal(HttpStatusCode.NoContent, loginResponse.StatusCode);
        var stampBeforeLogout = (await factory.GetUserAsync(user.Id)).SecurityStamp;

        var unprotectedLogout = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, unprotectedLogout.StatusCode);
        using (var document = JsonDocument.Parse(
            await unprotectedLogout.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Equal(
                "urn:carecomply:problem:antiforgery-validation",
                root.GetProperty("type").GetString());
            Assert.Equal(400, root.GetProperty("status").GetInt32());
            Assert.True(root.TryGetProperty("traceId", out _));
        }

        var requestToken = await GetCsrfTokenAsync(client);
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("X-XSRF-TOKEN", requestToken);
        var protectedLogout = await client.SendAsync(logoutRequest);

        Assert.Equal(HttpStatusCode.NoContent, protectedLogout.StatusCode);
        Assert.Equal(stampBeforeLogout, (await factory.GetUserAsync(user.Id)).SecurityStamp);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task SecurityStampChangeAloneRejectsExistingSession()
    {
        var user = await CreateUserAsync();
        using var client = factory.CreateSecureClient();
        var loginResponse = await LoginAsync(client, user.Email!, ValidPassword);
        Assert.Equal(HttpStatusCode.NoContent, loginResponse.StatusCode);
        Assert.True((await factory.GetUserAsync(user.Id)).IsActive);

        await factory.UpdateSecurityStampAsync(user.Id);

        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True((await factory.GetUserAsync(user.Id)).IsActive);
    }

    [Fact]
    public async Task InactiveFlagAloneRejectsExistingSession()
    {
        var user = await CreateUserAsync();
        using var client = factory.CreateSecureClient();
        var loginResponse = await LoginAsync(client, user.Email!, ValidPassword);
        Assert.Equal(HttpStatusCode.NoContent, loginResponse.StatusCode);
        var securityStamp = (await factory.GetUserAsync(user.Id)).SecurityStamp;

        await factory.DisableUserAsync(user.Id);

        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var storedUser = await factory.GetUserAsync(user.Id);
        Assert.False(storedUser.IsActive);
        Assert.Equal(securityStamp, storedUser.SecurityStamp);
    }

    [Fact]
    public async Task InvalidLoginInputReturnsValidationProblemDetails()
    {
        using var client = factory.CreateSecureClient();

        var response = await LoginAsync(client, string.Empty, string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("urn:carecomply:problem:validation", root.GetProperty("type").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.True(root.GetProperty("errors").TryGetProperty("Email", out _));
        Assert.True(root.GetProperty("errors").TryGetProperty("Password", out _));
        Assert.True(root.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task LoginRateLimitIsEnforcedWithProblemDetails()
    {
        using var limitedFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Authentication:LoginRateLimit:PermitLimit", "2"));
        using var client = limitedFactory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
        });

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var permittedResponse = await LoginAsync(
                client,
                $"missing-{Guid.NewGuid():N}@example.test",
                "invalid credential");
            Assert.Equal(HttpStatusCode.Unauthorized, permittedResponse.StatusCode);
        }

        var rejectedResponse = await LoginAsync(
            client,
            $"missing-{Guid.NewGuid():N}@example.test",
            "invalid credential");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        using var document = JsonDocument.Parse(await rejectedResponse.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(
            "urn:carecomply:problem:authentication-rate-limit",
            root.GetProperty("type").GetString());
        Assert.Equal(429, root.GetProperty("status").GetInt32());
        Assert.True(root.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task UnexpectedAuthenticationFailureReturnsSanitizedProblemDetails()
    {
        using var failingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIdentityAuthentication>();
                services.AddScoped<IIdentityAuthentication, ThrowingIdentityAuthentication>();
            }));
        using var client = failingFactory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
        });

        var response = await LoginAsync(client, "person@example.test", ValidPassword);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        Assert.Equal(
            "urn:carecomply:problem:internal-server-error",
            root.GetProperty("type").GetString());
        Assert.Equal("An unexpected error occurred.", root.GetProperty("title").GetString());
        Assert.True(root.TryGetProperty("traceId", out _));
        Assert.False(root.TryGetProperty("detail", out _));
        Assert.False(root.TryGetProperty("exception", out _));
        Assert.False(root.TryGetProperty("stackTrace", out _));
        Assert.DoesNotContain(
            nameof(InvalidOperationException),
            responseBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ThrowingIdentityAuthentication.InternalMessage,
            responseBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigrationsCreateOnlyApprovedIdentityAndAuthorizationSchema()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText =
            "SELECT [name] FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') ORDER BY [name]";
        var tables = new List<string>();
        await using (var reader = await tableCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Equal(
            [
                    "__EFMigrationsHistory",
                    "AuditLogs",
                    "ClientAdditionalProfiles",
                    "ClientCaregivers",
                    "ClientContacts",
                    "ClientGuestInvitations",
                    "ClientGuestSessions",
                    "ClientHospitalizations",
                    "ClientLanguagePreferences",
                    "Clients",
                    "ClientServiceAgreementLines",
                    "ClientServiceAgreements",
                    "ClientServices",
                    "ClientTodos",
                    "Companies",
                    "CompanyLocations",
                    "NavigationFeaturePermissions",
                    "NavigationFeatures",
                    "NavigationModules",
                    "Permissions",
                    "RoleClaims",
                    "RolePermissionFeatureLinks",
                    "RolePermissions",
                    "RoleProfiles",
                    "Roles",
                    "UserClaims",
                    "UserLocations",
                    "UserLogins",
                    "UserRoles",
                    "Users",
                    "UserTokens",
            ],
            tables);

        await using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText =
            "SELECT [name] FROM sys.indexes WHERE [name] LIKE 'UX[_]%' ORDER BY [name]";
        var uniqueIndexes = new List<string>();
        await using (var reader = await indexCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                uniqueIndexes.Add(reader.GetString(0));
            }
        }

        Assert.Equal(
            [
                "UX_ClientContacts_Client_Type_Primary",
                "UX_ClientGuestInvitations_CreatedClientId",
                "UX_ClientGuestInvitations_TokenHash",
                "UX_ClientGuestSessions_InvitationId",
                "UX_ClientGuestSessions_SessionTokenHash",
                "UX_ClientLanguagePreferences_Primary",
                "UX_Clients_PmiNumber",
                "UX_ClientServices_Client_Code_Active",
                "UX_ClientServices_Client_Code_StartDate",
                "UX_Companies_Code",
                "UX_NavigationFeatures_Key",
                "UX_NavigationFeatures_Route",
                "UX_NavigationModules_Key",
                "UX_Permissions_Name",
                "UX_RolePermissionFeatureLinks_PermissionId",
                "UX_Roles_NormalizedName",
                "UX_Users_NormalizedEmail",
                "UX_Users_NormalizedUserName",
            ],
            uniqueIndexes);

        

        await using var foreignKeyCommand = connection.CreateCommand();
        foreignKeyCommand.CommandText =
            "SELECT COUNT(*) FROM sys.foreign_keys WHERE schema_id = SCHEMA_ID('dbo')";
        Assert.Equal(
            44,
            Convert.ToInt32(
                await foreignKeyCommand.ExecuteScalarAsync(),
                CultureInfo.InvariantCulture));
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password) => await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password });

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("requestToken").GetString()
            ?? throw new InvalidOperationException("The CSRF endpoint did not return a token.");
    }

    private static async Task AssertSafeAuthenticationFailureAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("Authentication failed.", root.GetProperty("title").GetString());
        Assert.Equal(401, root.GetProperty("status").GetInt32());
        Assert.Equal(
            "urn:carecomply:problem:authentication-failed",
            root.GetProperty("type").GetString());
        Assert.DoesNotContain("password", root.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inactive", root.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("locked", root.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(root.TryGetProperty("traceId", out _));
    }

    private async Task<ApplicationUser> CreateUserAsync(bool isActive = true) =>
        await factory.CreateUserAsync(
            $"user-{Guid.NewGuid():N}@example.test",
            ValidPassword,
            isActive);

    private sealed class ThrowingIdentityAuthentication : IIdentityAuthentication
    {
        public const string InternalMessage = "internal database details";

        public Task<IdentityAuthenticationOutcome> AuthenticateAsync(
            string email,
            string password,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(InternalMessage);
    }

    private sealed class MissingIdentityUserReader : IIdentityUserReader
    {
        public Task<IdentityUserSnapshot?> FindByIdAsync(
            Guid userId,
            CancellationToken cancellationToken) => Task.FromResult<IdentityUserSnapshot?>(null);
    }
}
