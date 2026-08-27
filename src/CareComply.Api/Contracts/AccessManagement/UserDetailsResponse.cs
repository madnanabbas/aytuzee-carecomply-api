namespace CareComply.Api.Contracts.AccessManagement;

public sealed record UserDetailsResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string DisplayName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool IsMfaEnabled,
    bool RequiresMfa,
    bool IsMfaRequired,
    bool IsMfaRequiredByPermissions,
    string MfaMethod,
    IReadOnlyList<UserListRoleResponse> Roles,
    string EditorVersion);

public sealed record UpdateUserGeneralRequest(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Password,
    string EditorVersion);

public sealed record UpdateUserStatusRequest(string EditorVersion);

public sealed record UpdateUserMfaRequest(
    bool IsEnabled,
    string Method,
    string EditorVersion);

public sealed record UpdateUserRolesRequest(
    IReadOnlyList<Guid> RoleIds,
    string EditorVersion);
