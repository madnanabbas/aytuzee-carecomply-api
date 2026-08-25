namespace CareComply.Api.Contracts.AccessManagement;

public sealed record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool RequiresMfa,
    string MfaMethod,
    IReadOnlyList<Guid>? RoleIds);

public sealed record CreateUserResponse(Guid Id);
