namespace CareComply.Infrastructure.Navigation;

internal static class NavigationSeedData
{
    public static IReadOnlyList<NavigationModuleSeed> Modules { get; } =
    [
        new(1, "overview", "Overview", 10, true),
        new(2, "people", "People", 20, true),
        new(3, "compliance", "Compliance", 30, true),
        new(4, "operations", "Operations", 40, true),
        new(5, "administration", "Administration", 50, true),
    ];

    public static IReadOnlyList<NavigationFeatureSeed> Features { get; } =
    [
        new(1, 1, null, "dashboard", "Dashboard", "/dashboard", "dashboard", 10, true),
        new(2, 2, null, "clients", "Clients", "/clients", "clients", 10, true),
        new(3, 2, null, "employees", "Employees", "/employees", "employees", 20, true),
        new(4, 3, null, "dhs-library", "DHS Library", "/dhs-library", "dhs-library", 10, true),
        new(5, 3, null, "documents", "Documents", "/documents", "documents", 20, true),
        new(6, 3, null, "expiry-review", "Expiry & Review", "/expiry-review", "expiry-review", 30, true),
        new(7, 3, null, "training", "Training", "/training", "training", 40, true),
        new(8, 3, null, "background-checks", "Background Checks", "/background-checks", "background-checks", 50, true),
        new(9, 4, null, "notifications", "Notifications", "/notifications", "notifications", 10, true),
        new(10, 4, null, "reports", "Reports", "/reports", "reports", 20, true),
        new(11, 5, null, "access-management", "Access Management", "/access-control", "access-management", 10, true),
        new(12, 5, 11, "users", "Users", "/access-control/users", "users", 10, true),
        new(13, 5, 11, "roles", "Roles", "/access-control/roles", "roles", 20, true),
        new(14, 5, 11, "permissions", "Permissions", "/access-control/permissions", "permissions", 30, true),
        new(15, 5, 11, "audit-logs", "Audit Logs", "/access-control/audit-logs", "audit-logs", 40, true),
        new(16, 5, null, "companies", "Companies", "/access-control/companies", "companies", 5, true),
    ];

    public static IReadOnlyList<NavigationFeaturePermissionSeed> FeaturePermissions { get; } =
    [
        new(1, 1),
        new(2, 2),
        new(2, 49),
        new(3, 11),
        new(4, 32),
        new(5, 6),
        new(5, 15),
        new(5, 24),
        new(5, 30),
        new(6, 37),
        new(7, 20),
        new(8, 26),
        new(9, 39),
        new(10, 41),
        new(11, 42),
        new(11, 43),
        new(11, 44),
        new(11, 45),
        new(11, 46),
        new(11, 47),
        new(11, 48),
        new(12, 42),
        new(13, 44),
        new(14, 46),
        new(15, 48),
        new(16, 81),
    ];
}

internal readonly record struct NavigationModuleSeed(
    int Id,
    string Key,
    string Name,
    int SortOrder,
    bool IsActive);

internal readonly record struct NavigationFeatureSeed(
    int Id,
    int ModuleId,
    int? ParentFeatureId,
    string Key,
    string Name,
    string Route,
    string IconKey,
    int SortOrder,
    bool IsActive);

internal readonly record struct NavigationFeaturePermissionSeed(
    int FeatureId,
    int PermissionId);
