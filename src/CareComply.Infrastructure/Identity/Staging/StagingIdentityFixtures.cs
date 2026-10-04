using CareComply.Application.Authorization;

namespace CareComply.Infrastructure.Identity.Staging;

internal static class StagingIdentityFixtures
{
    public static IReadOnlyList<StagingRoleFixture> Roles { get; } =
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

    public static IReadOnlyList<StagingUserFixture> Users { get; } =
    [
        new(
            "admin@stg.carecomply.local",
            "System",
            "Administrator",
            "System Administrator",
            StagingPasswordKey.SystemAdministrator),
        new(
            "compliance@stg.carecomply.local",
            "Compliance",
            "Manager",
            "Compliance Manager",
            StagingPasswordKey.ComplianceManager),
        new(
            "hr@stg.carecomply.local",
            "HR",
            "Manager",
            "HR Manager",
            StagingPasswordKey.HrManager),
        new(
            "viewer@stg.carecomply.local",
            "Read Only",
            "User",
            "Read Only User",
            StagingPasswordKey.ReadOnlyUser),
    ];
}

internal sealed record StagingRoleFixture(
    string Name,
    IReadOnlyList<string> Permissions);

internal sealed record StagingUserFixture(
    string Email,
    string FirstName,
    string LastName,
    string RoleName,
    StagingPasswordKey PasswordKey);

internal enum StagingPasswordKey
{
    SystemAdministrator,
    ComplianceManager,
    HrManager,
    ReadOnlyUser,
}
