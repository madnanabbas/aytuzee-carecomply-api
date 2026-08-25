using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Queries.ListUsers;

public sealed class ListUsersAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<ListUsersResult>
{
    public ListUsersResult Create(AuthorizationFailure failure) => ListUsersResult.Denied(failure);
}
