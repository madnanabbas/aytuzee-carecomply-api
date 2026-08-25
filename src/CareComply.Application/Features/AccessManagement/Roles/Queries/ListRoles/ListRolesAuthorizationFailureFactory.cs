using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Roles.Queries.ListRoles;

public sealed class ListRolesAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<ListRolesResult>
{
    public ListRolesResult Create(AuthorizationFailure failure) =>
        ListRolesResult.Denied(failure);
}
