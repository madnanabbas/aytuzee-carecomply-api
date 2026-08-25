using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Queries.ListUsers;

public sealed record ListUsersQuery(
    string? Search,
    bool? IsActive,
    Guid? RoleId,
    string? MfaStatus,
    string? SortBy,
    string? SortDirection,
    int PageNumber,
    int PageSize)
    : IQuery<ListUsersResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.User.View;
}
