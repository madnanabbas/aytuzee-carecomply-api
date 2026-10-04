using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Roles.Queries.ListRoles;

public sealed record ListRolesResult(
    IReadOnlyList<RoleDirectoryItem>? Roles,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ListRolesResult>
{
    public static ListRolesResult Succeeded(IReadOnlyList<RoleDirectoryItem> roles) =>
        new(roles, null);

    public static ListRolesResult Denied(AuthorizationFailure failure) =>
        new(null, failure);
}
