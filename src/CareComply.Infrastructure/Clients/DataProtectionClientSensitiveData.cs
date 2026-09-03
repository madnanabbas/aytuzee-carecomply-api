using CareComply.Application.Abstractions.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class DataProtectionClientSensitiveData(
    CareComplyDbContext context,
    IDataProtectionProvider dataProtectionProvider) : IClientSensitiveData
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector(
        "CareComply.Client.Ssn.v1");

    public string Protect(string ssn) => protector.Protect(ssn);

    public string Unprotect(string protectedSsn) => protector.Unprotect(protectedSsn);

    public async Task<string?> RevealSsnAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var protectedSsn = await context.Clients.AsNoTracking()
            .Where(client => client.Id == clientId)
            .Select(client => client.ProtectedSsn)
            .SingleOrDefaultAsync(cancellationToken);
        return string.IsNullOrEmpty(protectedSsn) ? null : Unprotect(protectedSsn);
    }
}
