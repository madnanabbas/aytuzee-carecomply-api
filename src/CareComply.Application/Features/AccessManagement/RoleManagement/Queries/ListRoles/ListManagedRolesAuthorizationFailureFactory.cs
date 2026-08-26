using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.ListRoles;

public sealed class ListManagedRolesAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<ListManagedRolesResult>
{
    public ListManagedRolesResult Create(AuthorizationFailure failure) => ListManagedRolesResult.Denied(failure);
}
