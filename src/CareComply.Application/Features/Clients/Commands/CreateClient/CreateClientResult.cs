using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.Clients.Commands.CreateClient;

public sealed record CreateClientResult(
    ClientAdministrationResult? Administration,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<CreateClientResult>
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Administration?.Status.ToString()
        ?? "Unavailable";

    public static CreateClientResult Completed(ClientAdministrationResult administration) =>
        new(administration, null);

    public static CreateClientResult Denied(AuthorizationFailure failure) => new(null, failure);
}
