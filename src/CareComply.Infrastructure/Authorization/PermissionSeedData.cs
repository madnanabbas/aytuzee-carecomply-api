using CareComply.Application.Authorization;

namespace CareComply.Infrastructure.Authorization;

internal static class PermissionSeedData
{
    public static IReadOnlyList<PermissionSeed> All { get; } = Array.AsReadOnly<PermissionSeed>(
        [
            new(1, Permissions.Dashboard.View),
            new(2, Permissions.Client.View),
            new(3, Permissions.Client.Create),
            new(4, Permissions.Client.Edit),
            new(5, Permissions.Client.Archive),
            new(6, Permissions.ClientDocument.View),
            new(7, Permissions.ClientDocument.Upload),
            new(8, Permissions.ClientDocument.Download),
            new(9, Permissions.ClientDocument.Replace),
            new(10, Permissions.ClientDocument.Archive),
            new(11, Permissions.Employee.View),
            new(12, Permissions.Employee.Create),
            new(13, Permissions.Employee.Edit),
            new(14, Permissions.Employee.Archive),
            new(15, Permissions.EmployeeDocument.View),
            new(16, Permissions.EmployeeDocument.Upload),
            new(17, Permissions.EmployeeDocument.Download),
            new(18, Permissions.EmployeeDocument.Replace),
            new(19, Permissions.EmployeeDocument.Archive),
            new(20, Permissions.Training.View),
            new(21, Permissions.Training.Create),
            new(22, Permissions.Training.Edit),
            new(23, Permissions.Training.Manage),
            new(24, Permissions.TrainingDocument.View),
            new(25, Permissions.TrainingDocument.Upload),
            new(26, Permissions.BackgroundCheck.View),
            new(27, Permissions.BackgroundCheck.Create),
            new(28, Permissions.BackgroundCheck.Edit),
            new(29, Permissions.BackgroundCheck.Manage),
            new(30, Permissions.BackgroundCheckDocument.View),
            new(31, Permissions.BackgroundCheckDocument.Upload),
            new(32, Permissions.DHSLibrary.View),
            new(33, Permissions.DHSLibrary.Create),
            new(34, Permissions.DHSLibrary.Edit),
            new(35, Permissions.DHSLibrary.PublishVersion),
            new(36, Permissions.DHSLibrary.Archive),
            new(37, Permissions.ExpiryReview.View),
            new(38, Permissions.ExpiryReview.Manage),
            new(39, Permissions.Notification.View),
            new(40, Permissions.Notification.Manage),
            new(41, Permissions.FinancialReport.View),
            new(42, Permissions.User.View),
            new(43, Permissions.User.Manage),
            new(44, Permissions.Role.View),
            new(45, Permissions.Role.Manage),
            new(46, Permissions.Permission.View),
            new(47, Permissions.Permission.Manage),
            new(48, Permissions.AuditLog.View),
            new(49, Permissions.Client.RevealSsn),
        ]);
}

internal readonly record struct PermissionSeed(int Id, string Name);
