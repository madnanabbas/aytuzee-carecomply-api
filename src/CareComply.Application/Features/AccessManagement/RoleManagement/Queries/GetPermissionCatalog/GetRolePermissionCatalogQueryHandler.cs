using CareComply.Application.Abstractions.AccessManagement;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetPermissionCatalog;

public sealed class GetRolePermissionCatalogQueryHandler(IRoleManagement roles)
    : IQueryHandler<GetRolePermissionCatalogQuery, GetRolePermissionCatalogResult>
{
    public async ValueTask<GetRolePermissionCatalogResult> Handle(
        GetRolePermissionCatalogQuery query,
        CancellationToken cancellationToken) =>
        GetRolePermissionCatalogResult.Succeeded(await roles.GetPermissionCatalogAsync(cancellationToken));
}
