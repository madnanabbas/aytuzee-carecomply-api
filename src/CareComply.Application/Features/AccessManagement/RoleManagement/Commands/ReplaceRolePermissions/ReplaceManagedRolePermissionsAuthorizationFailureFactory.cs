using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.ReplaceRolePermissions;

public sealed class ReplaceManagedRolePermissionsAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<ReplaceManagedRolePermissionsResult>
{
    public ReplaceManagedRolePermissionsResult Create(AuthorizationFailure failure) => ReplaceManagedRolePermissionsResult.Denied(failure);
}
