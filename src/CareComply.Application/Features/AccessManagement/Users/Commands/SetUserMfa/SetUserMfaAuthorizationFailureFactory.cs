using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.SetUserMfa;

public sealed class SetUserMfaAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<SetUserMfaResult>
{
    public SetUserMfaResult Create(AuthorizationFailure failure) => SetUserMfaResult.Denied(failure);
}
