namespace CareComply.Application.Abstractions.Employees;

public interface IEmployeeDirectory
{
    Task<IReadOnlyList<EmployeeDirectoryEntry>?> ListAsync(
        string? search,
        CancellationToken cancellationToken);
}

public sealed record EmployeeDirectoryEntry(
    Guid Id,
    Guid? CompanyId,
    string FirstName,
    string LastName,
    string PreferredName,
    string Email,
    string Phone,
    string JobTitle,
    bool IsActive,
    DateOnly? HireDate,
    IReadOnlyList<string> Locations);
