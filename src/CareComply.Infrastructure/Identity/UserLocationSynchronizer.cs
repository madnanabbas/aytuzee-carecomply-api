using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Identity;

internal sealed class UserLocationSynchronizer(CareComplyDbContext context)
{
    public async Task ReplaceAsync(
        Guid userId,
        IReadOnlyList<Guid> locationIds,
        CancellationToken cancellationToken)
    {
        var existingLocations = await context.UserLocations
            .Where(location => location.UserId == userId)
            .ToArrayAsync(cancellationToken);
        context.UserLocations.RemoveRange(
            existingLocations.Where(location => !locationIds.Contains(location.LocationId)));

        var existingIds = existingLocations
            .Select(location => location.LocationId)
            .ToHashSet();
        await context.UserLocations.AddRangeAsync(
            locationIds
                .Where(locationId => !existingIds.Contains(locationId))
                .Select(locationId => new ApplicationUserLocation
                {
                    UserId = userId,
                    LocationId = locationId,
                }),
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
