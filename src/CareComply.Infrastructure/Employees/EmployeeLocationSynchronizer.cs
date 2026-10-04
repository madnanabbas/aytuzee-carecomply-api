using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Employees;

internal sealed class EmployeeLocationSynchronizer(CareComplyDbContext context)
{
    public async Task ReplaceAsync(
        Guid employeeId,
        IReadOnlyList<Guid> locationIds,
        CancellationToken cancellationToken)
    {
        var existingLocations = await context.UserLocations
            .Where(location => location.UserId == employeeId)
            .ToArrayAsync(cancellationToken);
        context.UserLocations.RemoveRange(existingLocations);
        context.UserLocations.AddRange(locationIds
            .Distinct()
            .Select(locationId => new ApplicationUserLocation
            {
                UserId = employeeId,
                LocationId = locationId,
            }));
    }
}
