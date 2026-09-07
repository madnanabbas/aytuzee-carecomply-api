namespace CareComply.Api.Contracts.AccessManagement;

public sealed record ManagedRoleListItemResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Name,
    string Description,
    int AssignedUsers,
    int PermissionCount,
    string EditorVersion);

public sealed record ManagedRoleDetailsResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Name,
    string Description,
    IReadOnlyList<int> PermissionIds,
    int AssignedUsers,
    string EditorVersion);

public sealed record RolePermissionCatalogModuleResponse(
    int Id,
    string Key,
    string Name,
    int SortOrder,
    IReadOnlyList<RolePermissionCatalogFeatureResponse> Features);

public sealed record RolePermissionCatalogFeatureResponse(
    int Id,
    string Key,
    string Name,
    int SortOrder,
    IReadOnlyList<RolePermissionCatalogPermissionResponse> Permissions);

public sealed record RolePermissionCatalogPermissionResponse(int Id, string Code, string Action);

public sealed record CreateManagedRoleRequest(
    Guid? CompanyId,
    string Name,
    string? Description,
    IReadOnlyList<int>? PermissionIds);

public sealed record UpdateManagedRoleGeneralRequest(string Name, string? Description, string EditorVersion);

public sealed record ReplaceManagedRolePermissionsRequest(IReadOnlyList<int>? PermissionIds, string EditorVersion);
