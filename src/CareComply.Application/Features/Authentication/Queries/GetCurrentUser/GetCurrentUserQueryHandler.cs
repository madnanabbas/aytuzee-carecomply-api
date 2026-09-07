using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(
    ICurrentActor currentActor,
    IIdentityUserReader identityUserReader,
    IUserPermissionResolver permissionResolver)
    : IQueryHandler<GetCurrentUserQuery, CurrentUser?>
{
    public async ValueTask<CurrentUser?> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentActor.IsAuthenticated || currentActor.UserId is not { } userId)
        {
            return null;
        }

        var user = await identityUserReader.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var permissions = await permissionResolver.ResolveAsync(userId, cancellationToken);
        return new CurrentUser(
            user.Id,
            user.Email,
            user.DisplayName,
            user.CompanyId,
            user.CompanyName,
            user.IsPlatformAdministrator,
            permissions.Order(StringComparer.Ordinal).ToArray());
    }
}
