using CareComply.Application.Authorization;

namespace CareComply.Application.Features.Clients.Queries.GetClient;

public sealed class GetClientAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<GetClientResult>
{
    public GetClientResult Create(AuthorizationFailure failure) => GetClientResult.Denied(failure);
}
