using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.Clients.Commands.UpdateClient;

public sealed record UpdateClientResult(
    ClientAdministrationResult? Administration,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<UpdateClientResult>
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Administration?.Status.ToString()
        ?? "Unavailable";

    public static UpdateClientResult Completed(ClientAdministrationResult administration) =>
        new(administration, null);

    public static UpdateClientResult Denied(AuthorizationFailure failure) => new(null, failure);
}
