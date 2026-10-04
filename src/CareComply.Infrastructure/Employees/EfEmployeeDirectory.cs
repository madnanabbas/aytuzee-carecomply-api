using CareComply.Application.Abstractions.Employees;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Employees;

internal sealed class EfEmployeeDirectory(
    CareComplyDbContext context,
    ITenantScopeAccessor tenants,
    EfEmployeeDetailsReader detailsReader) : IEmployeeDirectory
{
    public Task<EmployeeDetailsResponse?> GetAsync(
        Guid employeeId,
        CancellationToken cancellationToken) =>
        detailsReader.GetAsync(employeeId, cancellationToken);

    public async Task<EmployeeDirectoryPage?> ListAsync(
        EmployeeDirectoryRequest request,
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

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var value = request.Search.Trim();
            employees = employees.Where(user =>
                user.FirstName.Contains(value) ||
                user.LastName.Contains(value) ||
                user.Email!.Contains(value));
        }

        var totalCount = await employees.CountAsync(cancellationToken);
        var items = await employees
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .ThenBy(user => user.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
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

        return new(items, request.PageNumber, request.PageSize, totalCount);
    }
}
