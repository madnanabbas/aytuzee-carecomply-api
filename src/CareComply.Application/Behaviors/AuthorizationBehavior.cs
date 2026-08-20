using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Behaviors;

public sealed class AuthorizationBehavior<TMessage, TResponse>(
    ICurrentActor currentActor,
    IUserPermissionResolver permissionResolver,
    IEnumerable<IAuthorizationFailureResultFactory<TResponse>> failureFactories)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage, IRequirePermission
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var failureFactory = GetFailureFactory();

        if (!Permissions.IsDefined(message.RequiredPermission))
        {
            throw new InvalidOperationException(
                "The request declares an unknown permission requirement.");
        }

        if (!currentActor.IsAuthenticated || currentActor.UserId is not { } userId)
        {
            return failureFactory.Create(AuthorizationFailure.Unauthenticated);
        }

        var permissions = await permissionResolver.ResolveAsync(userId, cancellationToken);

        if (!permissions.Contains(message.RequiredPermission))
        {
            return failureFactory.Create(AuthorizationFailure.Forbidden);
        }

        return await next(message, cancellationToken);
    }

    private IAuthorizationFailureResultFactory<TResponse> GetFailureFactory()
    {
        using var enumerator = failureFactories.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            throw new InvalidOperationException(
                "A protected response type must have exactly one authorization failure factory.");
        }

        var factory = enumerator.Current;

        if (enumerator.MoveNext())
        {
            throw new InvalidOperationException(
                "A protected response type must not have multiple authorization failure factories.");
        }

        return factory;
    }
}
