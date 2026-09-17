namespace CareComply.Api.Contracts.AccessManagement;

public sealed record UserDetailsResponse(
    Guid Id,
    Guid? CompanyId,
    string? CompanyName,
    bool IsPlatformAdministrator,
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
    IReadOnlyList<Guid> LocationIds,
    string EditorVersion);

public sealed record UpdateUserGeneralRequest(
    Guid? CompanyId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Password,
    IReadOnlyList<Guid>? LocationIds,
    string EditorVersion);

public sealed record UpdateUserStatusRequest(string EditorVersion);

public sealed record UpdateUserMfaRequest(
    bool IsEnabled,
    string Method,
    string EditorVersion);

public sealed record UpdateUserRolesRequest(
    IReadOnlyList<Guid> RoleIds,
    string EditorVersion);
