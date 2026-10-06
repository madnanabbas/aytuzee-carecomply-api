using CareComply.Application.Abstractions.Employees;
using CareComply.Application.Features.Employees;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Employees;

internal sealed class EmployeePersistenceValidator(
    CareComplyDbContext context,
    UserManager<ApplicationUser> userManager,
    EmployeeRequestValidation requestValidation)
{
    public async Task<IReadOnlyDictionary<string, string[]>> ValidateAsync(
        SaveEmployeeRequest request,
        Guid companyId,
        Guid? currentEmployeeId,
        bool creating,
        CancellationToken cancellationToken)
    {
        var errors = requestValidation.Validate(request, creating)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToList(),
                StringComparer.Ordinal);

        var validLocationCount = request.LocationIds is null
            ? 0
            : await context.CompanyLocations.CountAsync(
                location => request.LocationIds.Contains(location.Id)
                    && location.CompanyId == companyId
                    && location.IsActive,
                cancellationToken);
        if (request.LocationIds is not { Count: > 0 }
            || validLocationCount != request.LocationIds.Distinct().Count())
        {
            AddError(errors, nameof(request.LocationIds), "Select at least one valid company location.");
        }

        if (await context.Users.AnyAsync(
            user => user.Id != currentEmployeeId
                && user.NormalizedEmail == userManager.NormalizeEmail(request.Email),
            cancellationToken))
        {
            AddError(errors, nameof(request.Email), "Email is already in use.");
        }

        return errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Distinct(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    }

    private static void AddError(
        Dictionary<string, List<string>> errors,
        string key,
        string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors.Add(key, messages);
        }

        messages.Add(message);
    }
}
