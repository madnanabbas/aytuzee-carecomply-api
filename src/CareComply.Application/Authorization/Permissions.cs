using System.Diagnostics.CodeAnalysis;

namespace CareComply.Application.Authorization;

public static class Permissions
{
    public static class Dashboard
    {
        public const string View = "Dashboard.View";
    }

    public static class Client
    {
        public const string View = "Client.View";
        public const string Create = "Client.Create";
        public const string Edit = "Client.Edit";
        public const string Archive = "Client.Archive";
        public const string RevealSsn = "Client.RevealSsn";
    }

    public static class ClientDocument
    {
        public const string View = "ClientDocument.View";
        public const string Upload = "ClientDocument.Upload";
        public const string Download = "ClientDocument.Download";
        public const string Replace = "ClientDocument.Replace";
        public const string Archive = "ClientDocument.Archive";
    }

    public static class Employee
    {
        public const string View = "Employee.View";
        public const string Create = "Employee.Create";
        public const string Edit = "Employee.Edit";
        public const string Archive = "Employee.Archive";
    }

    public static class EmployeeDocument
    {
        public const string View = "EmployeeDocument.View";
        public const string Upload = "EmployeeDocument.Upload";
        public const string Download = "EmployeeDocument.Download";
        public const string Replace = "EmployeeDocument.Replace";
        public const string Archive = "EmployeeDocument.Archive";
    }

    public static class Training
    {
        public const string View = "Training.View";
        public const string Create = "Training.Create";
        public const string Edit = "Training.Edit";
        public const string Manage = "Training.Manage";
    }

    public static class TrainingDocument
    {
        public const string View = "TrainingDocument.View";
        public const string Upload = "TrainingDocument.Upload";
    }

    public static class BackgroundCheck
    {
        public const string View = "BackgroundCheck.View";
        public const string Create = "BackgroundCheck.Create";
        public const string Edit = "BackgroundCheck.Edit";
        public const string Manage = "BackgroundCheck.Manage";
    }

    public static class BackgroundCheckDocument
    {
        public const string View = "BackgroundCheckDocument.View";
        public const string Upload = "BackgroundCheckDocument.Upload";
    }

    public static class DHSLibrary
    {
        public const string View = "DHSLibrary.View";
        public const string Create = "DHSLibrary.Create";
        public const string Edit = "DHSLibrary.Edit";
        public const string PublishVersion = "DHSLibrary.PublishVersion";
        public const string Archive = "DHSLibrary.Archive";
    }

    public static class ExpiryReview
    {
        public const string View = "ExpiryReview.View";
        public const string Manage = "ExpiryReview.Manage";
    }

    public static class Notification
    {
        public const string View = "Notification.View";
        public const string Manage = "Notification.Manage";
    }

    public static class FinancialReport
    {
        public const string View = "FinancialReport.View";
    }

    public static class User
    {
        public const string View = "User.View";
        public const string Manage = "User.Manage";
    }

    public static class Role
    {
        public const string View = "Role.View";
        public const string Manage = "Role.Manage";
    }

    [SuppressMessage(
        "Naming",
        "CA1711:Identifiers should not have incorrect suffix",
        Justification = "Permission matches the canonical Permission.View and Permission.Manage resource name.")]
    public static class Permission
    {
        public const string View = "Permission.View";
        public const string Manage = "Permission.Manage";
    }

    public static class AuditLog
    {
        public const string View = "AuditLog.View";
    }

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
        [
            Dashboard.View,
            Client.View,
            Client.Create,
            Client.Edit,
            Client.Archive,
            Client.RevealSsn,
            ClientDocument.View,
            ClientDocument.Upload,
            ClientDocument.Download,
            ClientDocument.Replace,
            ClientDocument.Archive,
            Employee.View,
            Employee.Create,
            Employee.Edit,
            Employee.Archive,
            EmployeeDocument.View,
            EmployeeDocument.Upload,
            EmployeeDocument.Download,
            EmployeeDocument.Replace,
            EmployeeDocument.Archive,
            Training.View,
            Training.Create,
            Training.Edit,
            Training.Manage,
            TrainingDocument.View,
            TrainingDocument.Upload,
            BackgroundCheck.View,
            BackgroundCheck.Create,
            BackgroundCheck.Edit,
            BackgroundCheck.Manage,
            BackgroundCheckDocument.View,
            BackgroundCheckDocument.Upload,
            DHSLibrary.View,
            DHSLibrary.Create,
            DHSLibrary.Edit,
            DHSLibrary.PublishVersion,
            DHSLibrary.Archive,
            ExpiryReview.View,
            ExpiryReview.Manage,
            Notification.View,
            Notification.Manage,
            FinancialReport.View,
            User.View,
            User.Manage,
            Role.View,
            Role.Manage,
            Permission.View,
            Permission.Manage,
            AuditLog.View,
        ]);

    private static readonly HashSet<string> DefinedPermissions =
        new(All, StringComparer.Ordinal);

    public static bool IsDefined(string? permission) =>
        permission is not null && DefinedPermissions.Contains(permission);
}
