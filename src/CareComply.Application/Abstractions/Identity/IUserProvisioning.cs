namespace CareComply.Application.Abstractions.Identity;

public interface IUserProvisioning
{
    Task<UserProvisioningResult> CreateAsync(
        UserProvisioningRequest request,
        CancellationToken cancellationToken);
}

public sealed record UserProvisioningRequest(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool RequiresMfa,
    UserMfaMethod MfaMethod,
    IReadOnlyList<Guid> RoleIds);

public sealed record UserProvisioningResult(
    UserProvisioningStatus Status,
    Guid? UserId);

public enum UserProvisioningStatus
{
    Succeeded,
    DuplicateEmail,
    RoleNotFound,
    RoleAssignmentFailed,
    Unavailable,
}
