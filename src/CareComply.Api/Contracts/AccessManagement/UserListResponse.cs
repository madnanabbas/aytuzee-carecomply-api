namespace CareComply.Api.Contracts.AccessManagement;

public sealed record UserListResponse(
    IReadOnlyList<UserListItemResponse> Items,
    IReadOnlyList<UserListRoleResponse> AvailableRoles,
    int PageNumber,
    int PageSize,
    int TotalCount);

public sealed record UserListItemResponse(
    Guid Id,
    string DisplayName,
    string Email,
    bool IsActive,
    string MfaStatus,
    IReadOnlyList<UserListRoleResponse> Roles);

public sealed record UserListRoleResponse(Guid Id, string Name);
