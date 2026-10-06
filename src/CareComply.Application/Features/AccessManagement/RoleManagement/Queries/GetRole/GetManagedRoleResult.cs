using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetRole;

public sealed record GetManagedRoleResult(RoleManagementDetails? Role, AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<GetManagedRoleResult>
{
    public static GetManagedRoleResult Succeeded(RoleManagementDetails? role) => new(role, null);
    public static GetManagedRoleResult Denied(AuthorizationFailure failure) => new(null, failure);
}
