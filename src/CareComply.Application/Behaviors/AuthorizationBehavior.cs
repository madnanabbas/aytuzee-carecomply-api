using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Behaviors;

public sealed class AuthorizationBehavior<TMessage, TResponse>(
    ICurrentActor currentActor,
    IUserPermissionResolver permissionResolver)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage, IRequirePermission
    where TResponse : IAuthorizationFailureResponse<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!Permissions.IsDefined(message.RequiredPermission))
        {
            throw new InvalidOperationException(
                "The request declares an unknown permission requirement.");
        }

        if (!currentActor.IsAuthenticated || currentActor.UserId is not { } userId)
        {
            return TResponse.Denied(AuthorizationFailure.Unauthenticated);
        }

        var permissions = await permissionResolver.ResolveAsync(userId, cancellationToken);

        if (!permissions.Contains(message.RequiredPermission))
        {
            return TResponse.Denied(AuthorizationFailure.Forbidden);
        }

        return await next(message, cancellationToken);
    }
}
