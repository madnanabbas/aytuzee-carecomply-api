namespace CareComply.Application.Abstractions.Tenancy;

public interface ITenantScopeAccessor
{
    Task<TenantScope?> GetAsync(CancellationToken cancellationToken);
}

public sealed record TenantScope(
    Guid UserId,
    Guid CompanyId,
    string CompanyName,
    bool IsPlatformAdministrator);
