namespace CareComply.Application.Abstractions.Identity;

public interface IUserProvisioning
{
    Task<UserProvisioningResult> CreateAsync(
        UserProvisioningRequest request,
        CancellationToken cancellationToken);
}

public sealed record UserProvisioningRequest(
    Guid? CompanyId,
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? PhoneNumber,
    bool IsActive,
    bool RequiresMfa,
    UserMfaMethod MfaMethod,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<Guid> LocationIds);

public sealed record UserProvisioningResult(
    UserProvisioningStatus Status,
    Guid? UserId);

public enum UserProvisioningStatus
{
    Succeeded,
    DuplicateEmail,
    InvalidPassword,
    RoleNotFound,
    RoleAssignmentFailed,
    CompanyNotFound,
    Unavailable,
}
