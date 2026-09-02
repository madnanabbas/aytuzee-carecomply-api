using CareComply.Application.Authorization;

namespace CareComply.Application.Features.Clients.Queries.ListClients;

public sealed class ListClientsAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<ListClientsResult>
{
    public ListClientsResult Create(AuthorizationFailure failure) => ListClientsResult.Denied(failure);
}
