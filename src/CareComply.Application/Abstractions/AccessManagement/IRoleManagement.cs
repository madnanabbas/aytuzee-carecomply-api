namespace CareComply.Application.Abstractions.AccessManagement;

public interface IRoleManagement
{
    Task<IReadOnlyList<RoleManagementListItem>> ListAsync(CancellationToken cancellationToken);

    Task<RoleManagementDetails?> GetAsync(Guid roleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PermissionCatalogModule>> GetPermissionCatalogAsync(
        CancellationToken cancellationToken);

    Task<RoleMutationResult> CreateAsync(
        string name,
        string? description,
        IReadOnlyList<int> permissionIds,
        CancellationToken cancellationToken);

    Task<RoleMutationResult> UpdateGeneralAsync(
        Guid roleId,
        string name,
        string? description,
        string editorVersion,
        CancellationToken cancellationToken);

    Task<RolePermissionMutationResult> ReplacePermissionsAsync(
        Guid roleId,
        IReadOnlyList<int> permissionIds,
        string editorVersion,
        CancellationToken cancellationToken);
}

public sealed record RoleManagementListItem(
    Guid Id,
    string Name,
    string Description,
    int AssignedUserCount,
    int PermissionCount,
    string EditorVersion);

public sealed record RoleManagementDetails(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<int> PermissionIds,
    int AssignedUserCount,
    string EditorVersion);

public sealed record PermissionCatalogModule(
    int Id,
    string Key,
    string Name,
    int SortOrder,
    IReadOnlyList<PermissionCatalogFeature> Features);

public sealed record PermissionCatalogFeature(
    int Id,
    string Key,
    string Name,
    int SortOrder,
    IReadOnlyList<PermissionCatalogPermissions> Permissions);

public sealed record PermissionCatalogPermissions(
    int Id,
    string Code,
    string Action);

public sealed record RoleMutationResult(
    RoleMutationStatus Status,
    RoleManagementDetails? Role);

public sealed record RolePermissionMutationResult(
    RoleMutationStatus Status,
    RoleManagementDetails? Role,
    IReadOnlyList<int> AddedPermissionIds,
    IReadOnlyList<int> RemovedPermissionIds);

public enum RoleMutationStatus
{
    Succeeded,
    NoChanges,
    NotFound,
    DuplicateName,
    PermissionNotFound,
    Conflict,
    LastRoleManager,
    Unavailable,
}
