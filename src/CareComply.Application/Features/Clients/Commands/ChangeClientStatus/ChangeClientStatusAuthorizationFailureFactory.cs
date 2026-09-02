using CareComply.Application.Authorization;

namespace CareComply.Application.Features.Clients.Commands.ChangeClientStatus;

public sealed class ChangeClientStatusAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<ChangeClientStatusResult>
{
    public ChangeClientStatusResult Create(AuthorizationFailure failure) => ChangeClientStatusResult.Denied(failure);
}
