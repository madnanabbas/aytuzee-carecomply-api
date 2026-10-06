using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Queries.GetUser;

public sealed record GetUserQuery(Guid UserId) : IQuery<GetUserResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.User.View;
}
