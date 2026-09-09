using CareComply.Application.Abstractions.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using CareComply.Application.Abstractions.Tenancy;

namespace CareComply.Infrastructure.Clients;

internal sealed class DataProtectionClientSensitiveData(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantScopeAccessor,
    IDataProtectionProvider dataProtectionProvider) : IClientSensitiveData
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector(
        "CareComply.Client.Ssn.v1");

    public string Protect(string ssn) => protector.Protect(ssn);

    public string Unprotect(string protectedSsn) => protector.Unprotect(protectedSsn);

    public async Task<string?> RevealSsnAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return null;

        var protectedSsn = await context.Clients.AsNoTracking()
            .Where(client => client.Id == clientId
                && (tenant.IsPlatformAdministrator || (client.CompanyId == tenant.CompanyId
                    && context.UserLocations.Any(access => access.UserId == tenant.UserId && access.LocationId == client.LocationId))))
            .Select(client => client.ProtectedSsn)
            .SingleOrDefaultAsync(cancellationToken);
        return string.IsNullOrEmpty(protectedSsn) ? null : Unprotect(protectedSsn);
    }
}
