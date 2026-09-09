namespace CareComply.Api.Contracts.AccessManagement;

public sealed record CreateUserRequest(
    Guid? CompanyId,
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? PhoneNumber,
    bool IsActive,
    bool RequiresMfa,
    string MfaMethod,
    IReadOnlyList<Guid>? RoleIds,
    IReadOnlyList<Guid>? LocationIds);

public sealed record CreateUserResponse(Guid Id);
