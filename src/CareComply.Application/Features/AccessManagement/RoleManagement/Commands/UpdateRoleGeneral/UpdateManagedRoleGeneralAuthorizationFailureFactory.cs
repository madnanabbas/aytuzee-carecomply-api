using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.UpdateRoleGeneral;

public sealed class UpdateManagedRoleGeneralAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<UpdateManagedRoleGeneralResult>
{
    public UpdateManagedRoleGeneralResult Create(AuthorizationFailure failure) => UpdateManagedRoleGeneralResult.Denied(failure);
}
