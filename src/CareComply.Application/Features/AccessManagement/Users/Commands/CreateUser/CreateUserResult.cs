using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.CreateUser;

public sealed record CreateUserResult(
    UserProvisioningResult? Provisioning,
    AuthorizationFailure? AuthorizationFailure)
{
    public string ReasonCode => AuthorizationFailure is not null
        ? "authorization-denied"
        : Provisioning?.Status switch
        {
            UserProvisioningStatus.DuplicateEmail => "duplicate-email",
            UserProvisioningStatus.RoleNotFound => "role-not-found",
            UserProvisioningStatus.RoleAssignmentFailed => "role-assignment-failed",
            UserProvisioningStatus.Unavailable => "identity-unavailable",
            _ => "none",
        };

    public static CreateUserResult Completed(UserProvisioningResult provisioning) =>
        new(provisioning, null);

    public static CreateUserResult Denied(AuthorizationFailure failure) =>
        new(null, failure);
}
