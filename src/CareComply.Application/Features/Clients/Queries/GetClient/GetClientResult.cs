using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.Clients.Queries.GetClient;

public sealed record GetClientResult(
    ClientDirectoryDetails? Client,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<GetClientResult>
{
    public static GetClientResult Succeeded(ClientDirectoryDetails? client) => new(client, null);
    public static GetClientResult Denied(AuthorizationFailure failure) => new(null, failure);
}
