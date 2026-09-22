using CareComply.Application.Authorization;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Persistence;
using CareComply.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Identity.Development;

public sealed class DevelopmentIdentityInitializer(
    CareComplyDbContext context,
    ILogger<DevelopmentIdentityInitializer> logger,
    IOptions<DevelopmentIdentityOptions> options,
    RoleManager<ApplicationRole> roleManager,
    TimeProvider timeProvider,
    UserManager<ApplicationUser> userManager)
{
    private static readonly Action<ILogger, int, int, Exception?> LogFixturesInitialized =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(1, "DevelopmentIdentityFixturesInitialized"),
            "Development identity fixtures initialized with {RoleCount} roles and {UserCount} users.");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        if (!configuration.Enabled)
        {
            throw new InvalidOperationException(
                "Development identity fixture initialization is not enabled.");
        }

        var passwords = ReadRequiredPasswords(configuration.Users);
        var permissionIds = await LoadCanonicalPermissionIdsAsync(cancellationToken);
        var roles = await EnsureRolesAsync(permissionIds, cancellationToken);

        foreach (var fixture in UserFixtures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureUserAsync(
                fixture,
                passwords[fixture.PasswordKey],
                roles[fixture.RoleName]);
        }

        LogFixturesInitialized(
            logger,
            RoleFixtures.Count,
            UserFixtures.Count,
            null);
    }

    private async Task<Dictionary<string, int>> LoadCanonicalPermissionIdsAsync(
        CancellationToken cancellationToken)
    {
        var persistedPermissions = await context.Set<Permission>()
            .AsNoTracking()
            .ToDictionaryAsync(
                permission => permission.Name,
                permission => permission.Id,
                StringComparer.Ordinal,
                cancellationToken);

        if (persistedPermissions.Count != Permissions.All.Count
            || Permissions.All.Any(permission => !persistedPermissions.ContainsKey(permission)))
        {
            throw new InvalidOperationException(
                "The canonical permission reference data is unavailable or inconsistent.");
        }

        return persistedPermissions;
    }

    private async Task<Dictionary<string, ApplicationRole>> EnsureRolesAsync(
        Dictionary<string, int> permissionIds,
        CancellationToken cancellationToken)
    {
        var roles = new Dictionary<string, ApplicationRole>(StringComparer.Ordinal);

        foreach (var fixture in RoleFixtures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var role = await roleManager.FindByNameAsync(fixture.Name);

            if (role is null)
            {
                role = new ApplicationRole
                {
                    CompanyId = TenantDefaults.DefaultCompanyId,
                    Name = fixture.Name,
                };
                EnsureSucceeded(
                    await roleManager.CreateAsync(role),
                    $"create the development role '{fixture.Name}'");
            }

            roles.Add(fixture.Name, role);

            var desiredPermissionIds = fixture.Permissions
                .Select(permission => permissionIds[permission])
                .ToHashSet();
            var existingAssignments = await context.Set<RolePermission>()
                .Where(assignment => assignment.RoleId == role.Id)
                .ToListAsync(cancellationToken);

            context.RemoveRange(existingAssignments.Where(
                assignment => !desiredPermissionIds.Contains(assignment.PermissionId)));

            var existingPermissionIds = existingAssignments
                .Select(assignment => assignment.PermissionId)
                .ToHashSet();
            await context.AddRangeAsync(
                desiredPermissionIds
                    .Where(permissionId => !existingPermissionIds.Contains(permissionId))
                    .Select(permissionId => new RolePermission(role.Id, permissionId)),
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        return roles;
    }

    private async Task EnsureUserAsync(
        DevelopmentUserFixture fixture,
        string password,
        ApplicationRole role)
    {
        var user = await userManager.FindByEmailAsync(fixture.Email);

        if (user is null)
        {
            var now = timeProvider.GetUtcNow();
            user = new ApplicationUser
            {
                CompanyId = TenantDefaults.DefaultCompanyId,
                Email = fixture.Email,
                UserName = fixture.Email,
                EmailConfirmed = true,
                FirstName = fixture.FirstName,
                LastName = fixture.LastName,
                IsActive = true,
                LockoutEnabled = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            EnsureSucceeded(
                await userManager.CreateAsync(user, password),
                $"create the development user '{fixture.Email}'");
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles
            .Where(currentRole => !string.Equals(
                currentRole,
                role.Name,
                StringComparison.Ordinal))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            EnsureSucceeded(
                await userManager.RemoveFromRolesAsync(user, rolesToRemove),
                $"synchronize roles for the development user '{fixture.Email}'");
        }

        if (!currentRoles.Contains(role.Name!, StringComparer.Ordinal))
        {
            EnsureSucceeded(
                await userManager.AddToRoleAsync(user, role.Name!),
                $"assign the development role for '{fixture.Email}'");
        }
    }

    private static Dictionary<DevelopmentPasswordKey, string> ReadRequiredPasswords(
        DevelopmentIdentityUserPasswords passwords) =>
        new()
        {
            [DevelopmentPasswordKey.SystemAdministrator] = RequirePassword(
                passwords.SystemAdministratorPassword,
                "Users:SystemAdministratorPassword"),
            [DevelopmentPasswordKey.ComplianceManager] = RequirePassword(
                passwords.ComplianceManagerPassword,
                "Users:ComplianceManagerPassword"),
            [DevelopmentPasswordKey.HrManager] = RequirePassword(
                passwords.HrManagerPassword,
                "Users:HrManagerPassword"),
            [DevelopmentPasswordKey.ReadOnlyUser] = RequirePassword(
                passwords.ReadOnlyUserPassword,
                "Users:ReadOnlyUserPassword"),
        };

    private static string RequirePassword(string? password, string relativeKey)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"The required development identity secret '{DevelopmentIdentityOptions.SectionName}:{relativeKey}' is not configured.");
        }

        return password;
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errorCodes = string.Join(
            ", ",
            result.Errors.Select(error => error.Code).Order(StringComparer.Ordinal));
        throw new InvalidOperationException(
            $"Identity could not {operation}. Error codes: {errorCodes}.");
    }

    private static IReadOnlyList<DevelopmentRoleFixture> RoleFixtures { get; } =
    [
        new("System Administrator", Permissions.All),
        new(
            "Compliance Manager",
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
            ]),
        new(
            "HR Manager",
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
            ]),
        new(
            "Read Only User",
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
            ]),
    ];

    private static IReadOnlyList<DevelopmentUserFixture> UserFixtures { get; } =
    [
        new("admin@carecomply.local", "System", "Administrator", "System Administrator", DevelopmentPasswordKey.SystemAdministrator),
        new("compliance@carecomply.local", "Compliance", "Manager", "Compliance Manager", DevelopmentPasswordKey.ComplianceManager),
        new("hr@carecomply.local", "HR", "Manager", "HR Manager", DevelopmentPasswordKey.HrManager),
        new("viewer@carecomply.local", "Read Only", "User", "Read Only User", DevelopmentPasswordKey.ReadOnlyUser),
    ];

    private sealed record DevelopmentRoleFixture(
        string Name,
        IReadOnlyList<string> Permissions);

    private sealed record DevelopmentUserFixture(
        string Email,
        string FirstName,
        string LastName,
        string RoleName,
        DevelopmentPasswordKey PasswordKey);

    private enum DevelopmentPasswordKey
    {
        SystemAdministrator,
        ComplianceManager,
        HrManager,
        ReadOnlyUser,
    }
}
