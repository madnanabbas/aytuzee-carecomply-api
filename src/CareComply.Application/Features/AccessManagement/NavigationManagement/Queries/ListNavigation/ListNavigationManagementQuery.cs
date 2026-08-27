using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Queries.ListNavigation;

public sealed record ListNavigationManagementQuery
    : IQuery<ListNavigationManagementResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.Permission.View;
}
