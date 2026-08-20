using CareComply.Application.Auditing;

namespace CareComply.Infrastructure.Auditing;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    internal static AuditLog Create(
        Guid id,
        DateTimeOffset occurredAtUtc,
        Guid actorUserId,
        AuditEvent auditEvent,
        string? traceId,
        string? metadataJson) => new()
        {
            Id = id,
            OccurredAtUtc = occurredAtUtc,
            ActorUserId = actorUserId,
            Action = auditEvent.Action,
            ResourceType = auditEvent.ResourceType,
            ResourceId = auditEvent.ResourceId,
            Outcome = auditEvent.Outcome,
            TraceId = traceId,
            MetadataJson = metadataJson,
        };

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string ResourceType { get; private set; } = string.Empty;

    public string ResourceId { get; private set; } = string.Empty;

    public AuditOutcome Outcome { get; private set; }

    public string? TraceId { get; private set; }

    public string? MetadataJson { get; private set; }
}
