using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientSupervisorCandidateReader(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantScopeAccessor) : IClientSupervisorCandidateReader
{
    public async Task<ClientSupervisorCandidateReadResult> ListAsync(
        Guid locationId,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null)
        {
            return ClientSupervisorCandidateReadResult.Unauthenticated();
        }

        var companyId = await context.CompanyLocations
            .AsNoTracking()
            .Where(location => location.Id == locationId && location.IsActive)
            .Select(location => (Guid?)location.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!companyId.HasValue || !await CanAccessLocationAsync(
                tenant,
                companyId.Value,
                locationId,
                cancellationToken))
        {
            return ClientSupervisorCandidateReadResult.NotFound();
        }

        var candidates = await context.Users
            .AsNoTracking()
            .Where(user => user.CompanyId == companyId.Value
                && user.IsEmployee
                && user.IsActive
                && context.UserLocations.Any(access =>
                    access.UserId == user.Id && access.LocationId == locationId))
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .Select(user => new ClientSupervisorCandidate(
                user.Id,
                user.FirstName + " " + user.LastName,
                user.JobTitle))
            .ToArrayAsync(cancellationToken);

        return ClientSupervisorCandidateReadResult.Succeeded(candidates);
    }

    private async Task<bool> CanAccessLocationAsync(
        TenantScope tenant,
        Guid companyId,
        Guid locationId,
        CancellationToken cancellationToken)
    {
        if (tenant.IsPlatformAdministrator)
        {
            return true;
        }

        return tenant.CompanyId == companyId
            && await context.UserLocations.AsNoTracking().AnyAsync(
                access => access.UserId == tenant.UserId && access.LocationId == locationId,
                cancellationToken);
    }
}
