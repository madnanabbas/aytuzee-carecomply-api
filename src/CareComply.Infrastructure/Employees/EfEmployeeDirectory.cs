using CareComply.Application.Abstractions.Employees;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Employees;

internal sealed class EfEmployeeDirectory(
    CareComplyDbContext context,
    ITenantScopeAccessor tenants) : IEmployeeDirectory
{
    public async Task<IReadOnlyList<EmployeeDirectoryEntry>?> ListAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.GetAsync(cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var employees = context.Users.AsNoTracking().Where(user => user.IsEmployee);
        if (!tenant.IsPlatformAdministrator)
        {
            employees = employees.Where(user => user.CompanyId == tenant.CompanyId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            employees = employees.Where(user =>
                user.FirstName.Contains(value) ||
                user.LastName.Contains(value) ||
                user.Email!.Contains(value));
        }

        return await employees
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .Select(user => new EmployeeDirectoryEntry(
                user.Id,
                user.CompanyId,
                user.FirstName,
                user.LastName,
                user.PreferredName,
                user.Email!,
                user.PhoneNumber ?? string.Empty,
                user.JobTitle,
                user.IsActive,
                user.HireDate,
                context.UserLocations
                    .Where(link => link.UserId == user.Id)
                    .Join(context.CompanyLocations,
                        link => link.LocationId,
                        location => location.Id,
                        (_, location) => location.Name)
                    .OrderBy(name => name)
                    .ToArray()))
            .ToArrayAsync(cancellationToken);
    }
}
