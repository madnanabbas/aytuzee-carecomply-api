using CareComply.Application.Authorization;

namespace CareComply.Application.Features.Clients.Commands.CreateClient;

public sealed class CreateClientAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<CreateClientResult>
{
    public CreateClientResult Create(AuthorizationFailure failure) => CreateClientResult.Denied(failure);
}
