using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CareComply.Api.Authorization;

public sealed class PermissionAuthorizationHandler(
    ICurrentActor currentActor,
    IUserPermissionResolver permissionResolver,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!currentActor.IsAuthenticated || currentActor.UserId is not { } userId)
        {
            return;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted
            ?? CancellationToken.None;
        var permissions = await permissionResolver.ResolveAsync(userId, cancellationToken);

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
