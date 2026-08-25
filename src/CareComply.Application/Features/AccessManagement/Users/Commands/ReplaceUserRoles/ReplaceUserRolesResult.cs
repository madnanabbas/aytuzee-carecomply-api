using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.ReplaceUserRoles;

public sealed record ReplaceUserRolesResult(
    UserRoleUpdateResult? Update,
    AuthorizationFailure? AuthorizationFailure)
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Update?.Status.ToString()
        ?? "Unavailable";

    public static ReplaceUserRolesResult Succeeded(UserRoleUpdateResult update) => new(update, null);
    public static ReplaceUserRolesResult Denied(AuthorizationFailure failure) => new(null, failure);
}
