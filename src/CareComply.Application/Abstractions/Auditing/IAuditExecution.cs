using CareComply.Application.Auditing;

namespace CareComply.Application.Abstractions.Auditing;

public interface IAuditExecution
{
    ValueTask<TResponse> ExecuteAsync<TResponse>(
        Guid actorUserId,
        string? traceId,
        Func<CancellationToken, ValueTask<TResponse>> operation,
        Func<TResponse, AuditEvent> eventFactory,
        CancellationToken cancellationToken);
}
