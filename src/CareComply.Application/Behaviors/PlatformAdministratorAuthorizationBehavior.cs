using CareComply.Application.Abstractions.Tenancy;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Behaviors;

public sealed class PlatformAdministratorAuthorizationBehavior<TMessage, TResponse>(
    ITenantScopeAccessor tenantScopeAccessor)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage, IRequirePlatformAdministrator
    where TResponse : IAuthorizationFailureResponse<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant?.IsPlatformAdministrator != true)
        {
            return TResponse.Denied(AuthorizationFailure.Forbidden);
        }

        return await next(message, cancellationToken);
    }
}
