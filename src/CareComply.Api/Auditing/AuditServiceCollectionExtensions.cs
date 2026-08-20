using CareComply.Application.Abstractions.Auditing;

namespace CareComply.Api.Auditing;

public static class AuditServiceCollectionExtensions
{
    public static IServiceCollection AddCareComplyAuditing(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditTraceContext, HttpAuditTraceContext>();
        return services;
    }
}
