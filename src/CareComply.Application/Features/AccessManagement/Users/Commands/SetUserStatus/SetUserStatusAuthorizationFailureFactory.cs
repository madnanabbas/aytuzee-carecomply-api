using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.SetUserStatus;

public sealed class SetUserStatusAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<SetUserStatusResult>
{
    public SetUserStatusResult Create(AuthorizationFailure failure) => SetUserStatusResult.Denied(failure);
}
