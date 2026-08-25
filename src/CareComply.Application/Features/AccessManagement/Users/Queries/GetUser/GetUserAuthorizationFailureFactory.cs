using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Queries.GetUser;

public sealed class GetUserAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<GetUserResult>
{
    public GetUserResult Create(AuthorizationFailure failure) => GetUserResult.Denied(failure);
}
