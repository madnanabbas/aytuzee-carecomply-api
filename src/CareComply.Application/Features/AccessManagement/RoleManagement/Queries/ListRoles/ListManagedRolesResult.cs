using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.ListRoles;

public sealed record ListManagedRolesResult(
    IReadOnlyList<RoleManagementListItem>? Roles,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ListManagedRolesResult>
{
    public static ListManagedRolesResult Succeeded(IReadOnlyList<RoleManagementListItem> roles) => new(roles, null);
    public static ListManagedRolesResult Denied(AuthorizationFailure failure) => new(null, failure);
}
