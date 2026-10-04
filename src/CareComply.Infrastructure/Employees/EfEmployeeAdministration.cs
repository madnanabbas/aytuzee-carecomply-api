using CareComply.Application.Abstractions.Employees;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Application.Features.Employees;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Employees;

internal sealed class EfEmployeeAdministration(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantScopeAccessor,
    UserManager<ApplicationUser> userManager,
    IEmployeeDirectory employeeDirectory,
    EmployeePersistenceMapper mapper,
    EmployeePersistenceValidator validator,
    EmployeeLocationSynchronizer locationSynchronizer) : IEmployeeAdministration
{
    public async Task<EmployeeAdministrationResult> CreateAsync(
        SaveEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null)
        {
            return Failed(EmployeeAdministrationStatus.Unauthorized);
        }

        var companyId = tenant.IsPlatformAdministrator ? request.CompanyId : tenant.CompanyId;
        if (companyId is null)
        {
            return ValidationFailed(new Dictionary<string, string[]>(StringComparer.Ordinal));
        }

        var validationErrors = await validator.ValidateAsync(
            request,
            companyId.Value,
            currentEmployeeId: null,
            creating: true,
            cancellationToken);
        if (validationErrors.Count > 0)
        {
            return ValidationFailed(validationErrors);
        }

        var employee = new ApplicationUser
        {
            CompanyId = companyId,
            IsEmployee = true,
            CreatedAtUtc = mapper.UtcNow,
        };
        mapper.Apply(employee, request);
        employee.UserName = employee.Email;
        employee.EmailConfirmed = true;

        var identityResult = await userManager.CreateAsync(employee);
        if (!identityResult.Succeeded)
        {
            return ValidationFailed(identityResult.Errors
                .GroupBy(error => error.Code, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Description).ToArray(),
                    StringComparer.Ordinal));
        }

        await locationSynchronizer.ReplaceAsync(employee.Id, request.LocationIds!, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new(
            EmployeeAdministrationStatus.Succeeded,
            await employeeDirectory.GetAsync(employee.Id, cancellationToken));
    }

    public async Task<EmployeeAdministrationResult> UpdateAsync(
        Guid employeeId,
        SaveEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var employee = await FindScopedEmployeeAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return Failed(EmployeeAdministrationStatus.NotFound);
        }

        var validationErrors = await validator.ValidateAsync(
            request,
            employee.CompanyId!.Value,
            employeeId,
            creating: false,
            cancellationToken);
        if (validationErrors.Count > 0)
        {
            return ValidationFailed(validationErrors);
        }

        if (!IdentityEditorVersion.Matches(
                employee.ConcurrencyStamp ?? string.Empty,
                request.EditorVersion!))
        {
            return Failed(EmployeeAdministrationStatus.Conflict);
        }

        mapper.Apply(employee, request);
        employee.NormalizedEmail = userManager.NormalizeEmail(employee.Email);
        employee.NormalizedUserName = userManager.NormalizeName(employee.Email);
        employee.UserName = employee.Email;
        employee.ConcurrencyStamp = Guid.NewGuid().ToString();

        await locationSynchronizer.ReplaceAsync(employeeId, request.LocationIds!, cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failed(EmployeeAdministrationStatus.Conflict);
        }

        return new(
            EmployeeAdministrationStatus.Succeeded,
            await employeeDirectory.GetAsync(employeeId, cancellationToken));
    }

    private async Task<ApplicationUser?> FindScopedEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var employees = context.Users.Where(user => user.IsEmployee);
        if (!tenant.IsPlatformAdministrator)
        {
            employees = employees.Where(user => user.CompanyId == tenant.CompanyId);
        }

        return await employees.SingleOrDefaultAsync(
            employee => employee.Id == employeeId,
            cancellationToken);
    }

    private static EmployeeAdministrationResult Failed(EmployeeAdministrationStatus status) =>
        new(status);

    private static EmployeeAdministrationResult ValidationFailed(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(EmployeeAdministrationStatus.ValidationFailed, ValidationErrors: errors);
}
