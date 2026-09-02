using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.Clients.Commands.ChangeClientStatus;

public sealed record ChangeClientStatusResult(
    ClientAdministrationResult? Administration,
    AuthorizationFailure? AuthorizationFailure)
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Administration?.Status.ToString()
        ?? "Unavailable";

    public static ChangeClientStatusResult Completed(ClientAdministrationResult administration) =>
        new(administration, null);

    public static ChangeClientStatusResult Denied(AuthorizationFailure failure) => new(null, failure);
}
