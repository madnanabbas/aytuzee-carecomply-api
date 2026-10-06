using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetPermissionCatalog;

public sealed record GetRolePermissionCatalogResult(
    IReadOnlyList<PermissionCatalogModule>? Modules,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<GetRolePermissionCatalogResult>
{
    public static GetRolePermissionCatalogResult Succeeded(IReadOnlyList<PermissionCatalogModule> modules) => new(modules, null);
    public static GetRolePermissionCatalogResult Denied(AuthorizationFailure failure) => new(null, failure);
}
