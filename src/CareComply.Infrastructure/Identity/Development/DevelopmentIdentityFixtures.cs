using CareComply.Application.Authorization;

namespace CareComply.Infrastructure.Identity.Development;

internal static class DevelopmentIdentityFixtures
{
    public static IReadOnlyList<DevelopmentRoleFixture> Roles { get; } =
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

    public static IReadOnlyList<DevelopmentUserFixture> Users { get; } =
    [
        new(
            "admin@carecomply.local",
            "System",
            "Administrator",
            "System Administrator",
            DevelopmentPasswordKey.SystemAdministrator),
        new(
            "compliance@carecomply.local",
            "Compliance",
            "Manager",
            "Compliance Manager",
            DevelopmentPasswordKey.ComplianceManager),
        new(
            "hr@carecomply.local",
            "HR",
            "Manager",
            "HR Manager",
            DevelopmentPasswordKey.HrManager),
        new(
            "viewer@carecomply.local",
            "Read Only",
            "User",
            "Read Only User",
            DevelopmentPasswordKey.ReadOnlyUser),
    ];
}

internal sealed record DevelopmentRoleFixture(
    string Name,
    IReadOnlyList<string> Permissions);

internal sealed record DevelopmentUserFixture(
    string Email,
    string FirstName,
    string LastName,
    string RoleName,
    DevelopmentPasswordKey PasswordKey);

internal enum DevelopmentPasswordKey
{
    SystemAdministrator,
    ComplianceManager,
    HrManager,
    ReadOnlyUser,
}
