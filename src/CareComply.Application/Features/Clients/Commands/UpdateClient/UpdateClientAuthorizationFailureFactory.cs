using CareComply.Application.Authorization;

namespace CareComply.Application.Features.Clients.Commands.UpdateClient;

public sealed class UpdateClientAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<UpdateClientResult>
{
    public UpdateClientResult Create(AuthorizationFailure failure) => UpdateClientResult.Denied(failure);
}
