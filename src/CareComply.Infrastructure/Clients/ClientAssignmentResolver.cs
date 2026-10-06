using CareComply.Domain.Companies;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class ClientAssignmentResolver(CareComplyDbContext context)
{
    public async Task<CompanyLocation?> ResolveLocationAsync(
        Guid companyId,
        Guid? locationId,
        string? locationName,
        CancellationToken cancellationToken)
    {
        var query = context.CompanyLocations
            .AsNoTracking()
            .Where(location => location.CompanyId == companyId && location.IsActive);

        if (locationId.HasValue)
        {
            return await query.SingleOrDefaultAsync(
                location => location.Id == locationId.Value,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(locationName))
        {
            var normalizedName = locationName.Trim();
            var namedLocation = await query.SingleOrDefaultAsync(
                location => location.Name == normalizedName,
                cancellationToken);
            if (namedLocation is not null)
            {
                return namedLocation;
            }
        }

        return await query.SingleOrDefaultAsync(
            location => location.IsMain,
            cancellationToken);
    }

    public Task<bool> IsValidSupervisorAsync(
        Guid? supervisorId,
        Guid companyId,
        Guid locationId,
        CancellationToken cancellationToken) =>
        supervisorId is null
            ? Task.FromResult(true)
            : context.Users.AsNoTracking().AnyAsync(
                user => user.Id == supervisorId
                    && user.CompanyId == companyId
                    && user.IsEmployee
                    && user.IsActive
                    && context.UserLocations.Any(access =>
                        access.UserId == user.Id && access.LocationId == locationId),
                cancellationToken);
}
