using System.Text.Json;
using CareComply.Application.Abstractions.Auditing;
using CareComply.Application.Auditing;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Auditing;

internal sealed class EfAuditExecution(
    CareComplyDbContext context,
    TimeProvider timeProvider) : IAuditExecution
{
    private const string BeforeOperationSavepoint = "BeforeAuditedOperation";

    public async ValueTask<TResponse> ExecuteAsync<TResponse>(
        Guid actorUserId,
        string? traceId,
        Func<CancellationToken, ValueTask<TResponse>> operation,
        Func<TResponse, AuditEvent> eventFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(eventFactory);
        ValidateTraceId(traceId);

        var state = new ExecutionState<TResponse>(
            Guid.CreateVersion7(),
            timeProvider.GetUtcNow(),
            actorUserId,
            traceId,
            operation,
            eventFactory);
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteInTransactionAsync(
            state,
            ExecuteAttemptAsync,
            VerifySucceededAsync,
            cancellationToken);
    }

    private async Task<TResponse> ExecuteAttemptAsync<TResponse>(
        ExecutionState<TResponse> state,
        CancellationToken cancellationToken)
    {
        if (state.AttemptCount++ > 0)
        {
            context.ChangeTracker.Clear();
        }

        var transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "The audit execution strategy did not create its required transaction.");
        await transaction.CreateSavepointAsync(BeforeOperationSavepoint, cancellationToken);

        var response = await state.Operation(cancellationToken);
        var auditEvent = state.EventFactory(response)
            ?? throw new InvalidOperationException(
                "An auditable request must create exactly one audit event.");

        if (auditEvent.Outcome == AuditOutcome.Failed)
        {
            await transaction.RollbackToSavepointAsync(
                BeforeOperationSavepoint,
                cancellationToken);
            context.ChangeTracker.Clear();
        }

        var metadataJson = SerializeMetadata(auditEvent.Metadata);
        context.Add(AuditLog.Create(
            state.AuditId,
            state.OccurredAtUtc,
            state.ActorUserId,
            auditEvent,
            state.TraceId,
            metadataJson));
        await context.SaveChangesAsync(cancellationToken);
        return response;
    }

    private Task<bool> VerifySucceededAsync<TResponse>(
        ExecutionState<TResponse> state,
        CancellationToken cancellationToken) => context.Set<AuditLog>()
        .AsNoTracking()
        .AnyAsync(auditLog => auditLog.Id == state.AuditId, cancellationToken);

    private static string? SerializeMetadata(AuditMetadata metadata)
    {
        if (metadata.Entries.Count == 0)
        {
            return null;
        }

        var values = metadata.Entries.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(values);

        if (json.Length > AuditMetadata.MaximumSerializedLength)
        {
            throw new InvalidOperationException(
                $"Serialized audit metadata exceeds {AuditMetadata.MaximumSerializedLength} characters.");
        }

        return json;
    }

    private static void ValidateTraceId(string? traceId)
    {
        if (traceId is null)
        {
            return;
        }

        if (traceId.Length != 32
            || traceId.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new InvalidOperationException(
                "The audit trace identifier must use the 32-character W3C trace-id format.");
        }
    }

    private sealed class ExecutionState<TResponse>(
        Guid auditId,
        DateTimeOffset occurredAtUtc,
        Guid actorUserId,
        string? traceId,
        Func<CancellationToken, ValueTask<TResponse>> operation,
        Func<TResponse, AuditEvent> eventFactory)
    {
        public Guid AuditId { get; } = auditId;

        public DateTimeOffset OccurredAtUtc { get; } = occurredAtUtc;

        public Guid ActorUserId { get; } = actorUserId;

        public string? TraceId { get; } = traceId;

        public Func<CancellationToken, ValueTask<TResponse>> Operation { get; } = operation;

        public Func<TResponse, AuditEvent> EventFactory { get; } = eventFactory;

        public int AttemptCount { get; set; }
    }
}
