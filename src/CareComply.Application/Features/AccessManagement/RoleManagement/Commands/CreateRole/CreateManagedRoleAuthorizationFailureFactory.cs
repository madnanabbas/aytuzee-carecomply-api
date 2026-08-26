using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.CreateRole;

public sealed class CreateManagedRoleAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<CreateManagedRoleResult>
{
    public CreateManagedRoleResult Create(AuthorizationFailure failure) => CreateManagedRoleResult.Denied(failure);
}
