namespace CareComply.Application.Abstractions.Auditing;

public interface IAuditTraceContext
{
    string? TraceId { get; }
}
