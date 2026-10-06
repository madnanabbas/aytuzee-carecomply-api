using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Queries.GetUser;

public sealed class GetUserQueryHandler(IUserDirectory userDirectory)
    : IQueryHandler<GetUserQuery, GetUserResult>
{
    public async ValueTask<GetUserResult> Handle(
        GetUserQuery query,
        CancellationToken cancellationToken) =>
        GetUserResult.Succeeded(
            await userDirectory.GetByIdAsync(query.UserId, cancellationToken));
}
