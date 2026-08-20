using System.Diagnostics;
using CareComply.Application.Abstractions.Auditing;

namespace CareComply.Api.Auditing;

public sealed class HttpAuditTraceContext : IAuditTraceContext
{
    public string? TraceId => Activity.Current?.TraceId.ToString();
}
