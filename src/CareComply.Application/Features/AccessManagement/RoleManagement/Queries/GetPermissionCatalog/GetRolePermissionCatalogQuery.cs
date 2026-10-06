using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetPermissionCatalog;

public sealed record GetRolePermissionCatalogQuery : IQuery<GetRolePermissionCatalogResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.Role.View;
}
