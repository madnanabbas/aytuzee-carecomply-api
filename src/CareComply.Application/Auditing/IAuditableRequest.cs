namespace CareComply.Application.Auditing;

public interface IAuditableRequest<in TResponse>
{
    AuditEvent CreateAuditEvent(TResponse response);
}
