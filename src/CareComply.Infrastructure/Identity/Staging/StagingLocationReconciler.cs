using CareComply.Domain.Companies;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Identity.Staging;

internal sealed class StagingLocationReconciler(
    CareComplyDbContext context,
    TimeProvider timeProvider)
{
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var companyIds = await context.Companies
            .Where(company => company.IsActive)
            .Select(company => company.Id)
            .ToArrayAsync(cancellationToken);
        var existingLocations = await context.CompanyLocations
            .Where(location => companyIds.Contains(location.CompanyId))
            .Select(location => new { location.CompanyId, location.Name })
            .ToArrayAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        foreach (var companyId in companyIds)
        {
            var existing = existingLocations
                .Where(location => location.CompanyId == companyId)
                .Select(location => location.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var name in DemoLocationFixtures.Names.Where(name => !existing.Contains(name)))
            {
                context.CompanyLocations.Add(CompanyLocation.Create(
                    Guid.CreateVersion7(),
                    companyId,
                    name,
                    false,
                    now));
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
