using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.CreateUser;

public sealed class CreateUserAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<CreateUserResult>
{
    public CreateUserResult Create(AuthorizationFailure failure) =>
        CreateUserResult.Denied(failure);
}
