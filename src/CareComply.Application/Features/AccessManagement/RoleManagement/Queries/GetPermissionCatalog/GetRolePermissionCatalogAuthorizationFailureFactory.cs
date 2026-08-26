using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetPermissionCatalog;

public sealed class GetRolePermissionCatalogAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<GetRolePermissionCatalogResult>
{
    public GetRolePermissionCatalogResult Create(AuthorizationFailure failure) => GetRolePermissionCatalogResult.Denied(failure);
}
