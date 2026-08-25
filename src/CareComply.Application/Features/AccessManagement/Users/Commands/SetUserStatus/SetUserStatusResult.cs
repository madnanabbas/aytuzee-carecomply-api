using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.SetUserStatus;

public sealed record SetUserStatusResult(
    UserStatusUpdateResult? Update,
    bool SelfDeactivationDenied,
    AuthorizationFailure? AuthorizationFailure)
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? (SelfDeactivationDenied ? "SelfDeactivationDenied" : null)
        ?? Update?.Status.ToString()
        ?? "Unavailable";

    public static SetUserStatusResult Succeeded(UserStatusUpdateResult update) => new(update, false, null);
    public static SetUserStatusResult SelfDeactivation() => new(null, true, null);
    public static SetUserStatusResult Denied(AuthorizationFailure failure) => new(null, false, failure);
}
