using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetRole;

public sealed class GetManagedRoleAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<GetManagedRoleResult>
{
    public GetManagedRoleResult Create(AuthorizationFailure failure) => GetManagedRoleResult.Denied(failure);
}
