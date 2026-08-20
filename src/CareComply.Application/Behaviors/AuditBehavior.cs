using CareComply.Application.Abstractions.Auditing;
using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Auditing;
using Mediator;

namespace CareComply.Application.Behaviors;

public sealed class AuditBehavior<TMessage, TResponse>(
    ICurrentActor currentActor,
    IAuditTraceContext traceContext,
    IAuditExecution auditExecution)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage, IAuditableRequest<TResponse>
{
    public ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!currentActor.IsAuthenticated || currentActor.UserId is not { } actorUserId)
        {
            throw new InvalidOperationException(
                "An auditable request requires an authenticated human actor.");
        }

        return auditExecution.ExecuteAsync(
            actorUserId,
            traceContext.TraceId,
            token => next(message, token),
            message.CreateAuditEvent,
            cancellationToken);
    }
}
