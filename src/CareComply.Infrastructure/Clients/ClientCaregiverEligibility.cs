using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class ClientCaregiverEligibility(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantScopeAccessor)
{
    public async Task<Client?> ResolveAccessibleClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return null;
        return await context.Clients.SingleOrDefaultAsync(
            client => client.Id == clientId
                && (tenant.IsPlatformAdministrator || (client.CompanyId == tenant.CompanyId
                    && context.UserLocations.Any(access =>
                        access.UserId == tenant.UserId && access.LocationId == client.LocationId))),
            cancellationToken);
    }

    public IQueryable<ApplicationUser> EligibleUsers(Client client) =>
        context.Users.Where(user =>
            user.CompanyId == client.CompanyId
            && user.IsActive
            && user.IsEmployee
            && context.UserLocations.Any(access =>
                access.UserId == user.Id && access.LocationId == client.LocationId));

    public Task<bool> IsEligibleAsync(Client client, Guid userId, CancellationToken cancellationToken) =>
        EligibleUsers(client).AnyAsync(user => user.Id == userId, cancellationToken);
}
