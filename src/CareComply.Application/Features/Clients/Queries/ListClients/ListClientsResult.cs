using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.Clients.Queries.ListClients;

public sealed record ListClientsResult(
    ClientDirectoryPage? Page,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ListClientsResult>
{
    public static ListClientsResult Succeeded(ClientDirectoryPage page) => new(page, null);
    public static ListClientsResult Denied(AuthorizationFailure failure) => new(null, failure);
}
