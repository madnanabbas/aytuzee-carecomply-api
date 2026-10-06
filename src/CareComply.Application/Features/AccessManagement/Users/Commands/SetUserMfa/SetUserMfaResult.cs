using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.SetUserMfa;

public sealed record SetUserMfaResult(
    UserMfaUpdateResult? Update,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<SetUserMfaResult>
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Update?.Status.ToString()
        ?? "Unavailable";

    public static SetUserMfaResult Succeeded(UserMfaUpdateResult update) => new(update, null);
    public static SetUserMfaResult Denied(AuthorizationFailure failure) => new(null, failure);
}
