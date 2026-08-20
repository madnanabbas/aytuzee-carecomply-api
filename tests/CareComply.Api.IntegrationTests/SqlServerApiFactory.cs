using CareComply.Application.Authorization;
using CareComply.Application.Behaviors;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Auditing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using Testcontainers.MsSql;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace CareComply.Api.IntegrationTests;

public sealed class SqlServerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SqlServerImage =
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder(SqlServerImage).Build();

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();
        await CreateApplicationDatabaseAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        await context.Database.MigrateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeFixtureAsync();

    public HttpClient CreateSecureClient() => CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

    public async Task<ApplicationUser> CreateUserAsync(
        string email,
        string password,
        bool isActive = true)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            IsActive = isActive,
            LockoutEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));

        return user;
    }

    public async Task<ApplicationUser> GetUserAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("The expected test user does not exist.");
    }

    public async Task DisableUserAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("The expected test user does not exist.");

        user.IsActive = false;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var updateResult = await userManager.UpdateAsync(user);
        Assert.True(updateResult.Succeeded);

    }

    public async Task UpdateSecurityStampAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("The expected test user does not exist.");

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        Assert.True(stampResult.Succeeded);
    }

    public async Task GrantPermissionAsync(Guid userId, string permissionName)
    {
        using var scope = Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("The expected test user does not exist.");
        var role = new ApplicationRole { Name = $"MfaTest-{Guid.NewGuid():N}" };
        Assert.True((await roleManager.CreateAsync(role)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role.Name!)).Succeeded);
        var permission = await context.Set<Permission>()
            .SingleAsync(item => item.Name == permissionName);
        context.Add(new RolePermission(role.Id, permission.Id));
        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<string>> EnableAuthenticatorAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("The expected test user does not exist.");
        Assert.True((await userManager.ResetAuthenticatorKeyAsync(user)).Succeeded);
        var recoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?
            .ToArray() ?? [];
        Assert.Equal(10, recoveryCodes.Length);
        Assert.True((await userManager.SetTwoFactorEnabledAsync(user, true)).Succeeded);
        return recoveryCodes;
    }

    public async Task<string> GenerateAuthenticatorCodeAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("The expected test user does not exist.");
        var key = await userManager.GetAuthenticatorKeyAsync(user)
            ?? throw new InvalidOperationException("The expected authenticator key does not exist.");
        return GenerateTestTotp(key);
    }

    [SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "The test fixture must generate the RFC 6238 HMAC-SHA1 code consumed by ASP.NET Core Identity's built-in authenticator provider.")]
    private static string GenerateTestTotp(string base32Key)
    {
        var key = DecodeBase32(base32Key);
        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(
            counter,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.TryHashData(key, counter, hash, out _);
        var offset = hash[^1] & 0x0f;
        var binaryCode = (hash[offset] & 0x7f) << 24
            | (hash[offset + 1] & 0xff) << 16
            | (hash[offset + 2] & 0xff) << 8
            | hash[offset + 3] & 0xff;
        return (binaryCode % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>(value.Length * 5 / 8);
        var buffer = 0;
        var bitsRemaining = 0;

        foreach (var character in value.ToUpperInvariant())
        {
            var index = alphabet.IndexOf(character);
            if (index < 0)
            {
                throw new InvalidOperationException("The Identity authenticator key is not valid Base32.");
            }

            buffer = (buffer << 5) | index;
            bitsRemaining += 5;
            if (bitsRemaining >= 8)
            {
                bitsRemaining -= 8;
                output.Add((byte)(buffer >> bitsRemaining));
                buffer &= (1 << bitsRemaining) - 1;
            }
        }

        return output.ToArray();
    }

    public async Task<int> CountBusinessAuditLogsAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await context.Set<AuditLog>().CountAsync(log => log.ActorUserId == userId);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:CareComply", GetApplicationConnectionString());
        builder.UseSetting("Authentication:LoginRateLimit:PermitLimit", "100");
        builder.UseSetting("Authentication:Mfa:VerificationRateLimitPermitLimit", "100");
        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(TestPermissionController).Assembly);
            services.AddScoped<
                AuthorizationBehavior<TestPermissionCommand, TestPermissionResult>>();
            services.AddScoped<
                IAuthorizationFailureResultFactory<TestPermissionResult>,
                TestPermissionResultFactory>();
            services.AddScoped<
                ValidationBehavior<TestAuditCommand, TestAuditResult>>();
            services.AddScoped<
                AuthorizationBehavior<TestAuditCommand, TestAuditResult>>();
            services.AddScoped<AuditBehavior<TestAuditCommand, TestAuditResult>>();
            services.AddScoped<IValidator<TestAuditCommand>, TestAuditCommandValidator>();
            services.AddScoped<
                IAuthorizationFailureResultFactory<TestAuditResult>,
                TestAuditAuthorizationFailureResultFactory>();
        });
    }

    public async Task<string> CreateIsolatedDatabaseAsync()
    {
        var databaseName = $"CareComplyTests_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(databaseName);
        return BuildConnectionString(databaseName);
    }

    private async Task CreateApplicationDatabaseAsync()
    {
        await CreateDatabaseAsync("CareComplyTests");
    }

    private async Task CreateDatabaseAsync(string databaseName)
    {
        await using var connection = new SqlConnection(_sqlServer.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync();
    }

    private string GetApplicationConnectionString() =>
        BuildConnectionString("CareComplyTests");

    private string BuildConnectionString(string databaseName)
    {
        var connectionString = new SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
        {
            InitialCatalog = databaseName,
        };

        return connectionString.ConnectionString;
    }

    private async Task DisposeFixtureAsync()
    {
        await base.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }
}
