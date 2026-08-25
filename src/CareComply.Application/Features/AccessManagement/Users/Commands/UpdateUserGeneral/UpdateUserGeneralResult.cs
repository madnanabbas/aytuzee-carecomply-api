using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.UpdateUserGeneral;

public sealed record UpdateUserGeneralResult(
    UserGeneralUpdateResult? Update,
    AuthorizationFailure? AuthorizationFailure)
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Update?.Status.ToString()
        ?? "Unavailable";

    public static UpdateUserGeneralResult Succeeded(UserGeneralUpdateResult update) => new(update, null);
    public static UpdateUserGeneralResult Denied(AuthorizationFailure failure) => new(null, failure);
}
