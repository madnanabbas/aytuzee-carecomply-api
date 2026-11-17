using System.Collections.Concurrent;
using CareComply.Api.Development;
using CareComply.Application.Authorization;
using CareComply.Infrastructure;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Identity.Development;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CareComply.Api.IntegrationTests;

public sealed class DevelopmentIdentityIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string SystemAdministratorPassword = "System fixture password 123!";
    private const string ComplianceManagerPassword = "Compliance fixture password 123!";
    private const string HrManagerPassword = "HR fixture password 123!";
    private const string ReadOnlyUserPassword = "Viewer fixture password 123!";

    [Fact]
    public async Task CommandRequiresExplicitArgumentDevelopmentAndEnabledFlag()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        var disabledConfiguration = new ConfigurationBuilder().Build();

        Assert.False(await DevelopmentIdentityCommand.TryRunAsync(
            services,
            new TestHostEnvironment(Environments.Development),
            disabledConfiguration,
            []));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentIdentityCommand.TryRunAsync(
                services,
                new TestHostEnvironment(Environments.Production),
                EnabledConfiguration(),
                [DevelopmentIdentityCommand.Argument]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentIdentityCommand.TryRunAsync(
                services,
                new TestHostEnvironment(Environments.Development),
                disabledConfiguration,
                [DevelopmentIdentityCommand.Argument]));
    }

    [Fact]
    public async Task InitializerCreatesExactIdempotentDevelopmentFixturesWithoutSensitiveLogging()
    {
        var logProvider = new CapturingLoggerProvider();
        var configuration = EnabledConfiguration(
            await factory.CreateIsolatedDatabaseAsync());
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.AddProvider(logProvider));
        serviceCollection.AddInfrastructure(configuration);
        await using var services = serviceCollection.BuildServiceProvider();

        await using (var migrationScope = services.CreateAsyncScope())
        {
            var context = migrationScope.ServiceProvider
                .GetRequiredService<CareComplyDbContext>();
            await context.Database.MigrateAsync();
        }

        Assert.True(await DevelopmentIdentityCommand.TryRunAsync(
            services,
            new TestHostEnvironment(Environments.Development),
            configuration,
            [DevelopmentIdentityCommand.Argument]));

        string originalViewerPasswordHash;
        await using (var firstVerificationScope = services.CreateAsyncScope())
        {
            var userManager = firstVerificationScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var viewer = await userManager.FindByEmailAsync("viewer@carecomply.local")
                ?? throw new InvalidOperationException("The viewer fixture was not created.");
            originalViewerPasswordHash = viewer.PasswordHash
                ?? throw new InvalidOperationException("The viewer fixture has no password hash.");
            viewer.IsActive = false;
            Assert.True((await userManager.UpdateAsync(viewer)).Succeeded);
        }

        Assert.True(await DevelopmentIdentityCommand.TryRunAsync(
            services,
            new TestHostEnvironment(Environments.Development),
            configuration,
            [DevelopmentIdentityCommand.Argument]));

        await using var verificationScope = services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<CareComplyDbContext>();
        var verificationUserManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Equal(4, await verificationContext.Users.CountAsync());
        Assert.Equal(4, await verificationContext.Roles.CountAsync());
        Assert.Equal(4, await verificationContext.Set<IdentityUserRole<Guid>>().CountAsync());
        Assert.Equal(
            ExpectedRolePermissions.Values.Sum(permissions => permissions.Count),
            await verificationContext.Set<RolePermission>().CountAsync());
        Assert.Equal(Permissions.All.Count, await verificationContext.Set<Permission>().CountAsync());
        Assert.Equal(0, await verificationContext.Set<AuditLog>().CountAsync());

        var canonicalPermissions = await verificationContext.Set<Permission>()
            .AsNoTracking()
            .OrderBy(permission => permission.Id)
            .Select(permission => permission.Name)
            .ToArrayAsync();
        Assert.Equal(
            Permissions.All.Order(StringComparer.Ordinal),
            canonicalPermissions.Order(StringComparer.Ordinal));

        var actualRolePermissions = await (
                from role in verificationContext.Roles.AsNoTracking()
                join assignment in verificationContext.Set<RolePermission>().AsNoTracking()
                    on role.Id equals assignment.RoleId
                join permission in verificationContext.Set<Permission>().AsNoTracking()
                    on assignment.PermissionId equals permission.Id
                select new { RoleName = role.Name!, PermissionName = permission.Name })
            .ToArrayAsync();

        foreach (var expectedRole in ExpectedRolePermissions)
        {
            Assert.Equal(
                expectedRole.Value.Order(StringComparer.Ordinal),
                actualRolePermissions
                    .Where(item => item.RoleName == expectedRole.Key)
                    .Select(item => item.PermissionName)
                    .Order(StringComparer.Ordinal));
        }

        foreach (var expectedUser in ExpectedUserRoles)
        {
            var user = await verificationUserManager.FindByEmailAsync(expectedUser.Key)
                ?? throw new InvalidOperationException("An expected fixture user is missing.");
            Assert.Equal([expectedUser.Value], await verificationUserManager.GetRolesAsync(user));
        }

        var disabledViewer = await verificationUserManager.FindByEmailAsync(
            "viewer@carecomply.local")
            ?? throw new InvalidOperationException("The viewer fixture is missing.");
        Assert.False(disabledViewer.IsActive);
        Assert.Equal(originalViewerPasswordHash, disabledViewer.PasswordHash);

        var combinedLogs = string.Join(Environment.NewLine, logProvider.Messages);
        Assert.DoesNotContain(SystemAdministratorPassword, combinedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(ComplianceManagerPassword, combinedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(HrManagerPassword, combinedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(ReadOnlyUserPassword, combinedLogs, StringComparison.Ordinal);
    }

    private static IConfiguration EnabledConfiguration(string? connectionString = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["FormContentStorage:LocalRootPath"] = Path.GetTempPath(),
            [$"{DevelopmentIdentityOptions.SectionName}:Enabled"] = "true",
            [$"{DevelopmentIdentityOptions.SectionName}:Users:SystemAdministratorPassword"] =
                SystemAdministratorPassword,
            [$"{DevelopmentIdentityOptions.SectionName}:Users:ComplianceManagerPassword"] =
                ComplianceManagerPassword,
            [$"{DevelopmentIdentityOptions.SectionName}:Users:HrManagerPassword"] =
                HrManagerPassword,
            [$"{DevelopmentIdentityOptions.SectionName}:Users:ReadOnlyUserPassword"] =
                ReadOnlyUserPassword,
        };

        if (connectionString is not null)
        {
            values["ConnectionStrings:CareComply"] = connectionString;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ExpectedRolePermissions { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["System Administrator"] = Permissions.All,
            ["Compliance Manager"] =
            [
                Permissions.Dashboard.View,
                Permissions.Client.View,
                Permissions.Client.Create,
                Permissions.Client.Edit,
                Permissions.Client.Archive,
                Permissions.ClientDocument.View,
                Permissions.ClientDocument.Upload,
                Permissions.ClientDocument.Download,
                Permissions.ClientDocument.Replace,
                Permissions.ClientDocument.Archive,
                Permissions.Employee.View,
                Permissions.Employee.Create,
                Permissions.Employee.Edit,
                Permissions.Employee.Archive,
                Permissions.EmployeeDocument.View,
                Permissions.EmployeeDocument.Upload,
                Permissions.EmployeeDocument.Download,
                Permissions.EmployeeDocument.Replace,
                Permissions.EmployeeDocument.Archive,
                Permissions.Training.View,
                Permissions.Training.Create,
                Permissions.Training.Edit,
                Permissions.Training.Manage,
                Permissions.TrainingDocument.View,
                Permissions.TrainingDocument.Upload,
                Permissions.BackgroundCheck.View,
                Permissions.BackgroundCheck.Create,
                Permissions.BackgroundCheck.Edit,
                Permissions.BackgroundCheck.Manage,
                Permissions.BackgroundCheckDocument.View,
                Permissions.BackgroundCheckDocument.Upload,
                Permissions.DHSLibrary.View,
                Permissions.DHSLibrary.Create,
                Permissions.DHSLibrary.Edit,
                Permissions.DHSLibrary.PublishVersion,
                Permissions.DHSLibrary.Archive,
                Permissions.ExpiryReview.View,
                Permissions.ExpiryReview.Manage,
                Permissions.Notification.View,
                Permissions.Notification.Manage,
                Permissions.FinancialReport.View,
            ],
            ["HR Manager"] =
            [
                Permissions.Dashboard.View,
                Permissions.Employee.View,
                Permissions.Employee.Create,
                Permissions.Employee.Edit,
                Permissions.Employee.Archive,
                Permissions.EmployeeDocument.View,
                Permissions.EmployeeDocument.Upload,
                Permissions.EmployeeDocument.Download,
                Permissions.EmployeeDocument.Replace,
                Permissions.EmployeeDocument.Archive,
                Permissions.Training.View,
                Permissions.Training.Create,
                Permissions.Training.Edit,
                Permissions.Training.Manage,
                Permissions.TrainingDocument.View,
                Permissions.TrainingDocument.Upload,
                Permissions.BackgroundCheck.View,
                Permissions.BackgroundCheck.Create,
                Permissions.BackgroundCheck.Edit,
                Permissions.BackgroundCheckDocument.View,
                Permissions.BackgroundCheckDocument.Upload,
                Permissions.ExpiryReview.View,
                Permissions.Notification.View,
            ],
            ["Read Only User"] =
            [
                Permissions.Dashboard.View,
                Permissions.Client.View,
                Permissions.ClientDocument.View,
                Permissions.Employee.View,
                Permissions.EmployeeDocument.View,
                Permissions.Training.View,
                Permissions.TrainingDocument.View,
                Permissions.BackgroundCheck.View,
                Permissions.BackgroundCheckDocument.View,
                Permissions.DHSLibrary.View,
                Permissions.ExpiryReview.View,
                Permissions.Notification.View,
                Permissions.FinancialReport.View,
            ],
        };

    private static IReadOnlyDictionary<string, string> ExpectedUserRoles { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["admin@carecomply.local"] = "System Administrator",
            ["compliance@carecomply.local"] = "Compliance Manager",
            ["hr@carecomply.local"] = "HR Manager",
            ["viewer@carecomply.local"] = "Read Only User",
        };

    private sealed class TestHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CareComply.Api.IntegrationTests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; } = environmentName;

        public string WebRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            messages.Enqueue(formatter(state, exception));
    }
}
