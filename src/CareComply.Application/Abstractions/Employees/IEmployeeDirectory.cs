namespace CareComply.Application.Abstractions.Employees;

public interface IEmployeeDirectory
{
    Task<EmployeeDirectoryPage?> ListAsync(
        EmployeeDirectoryRequest request,
        CancellationToken cancellationToken);

    Task<EmployeeDetailsResponse?> GetAsync(
        Guid employeeId,
        CancellationToken cancellationToken);
}

public sealed record EmployeeDirectoryRequest(string? Search, int PageNumber, int PageSize);

public sealed record EmployeeDirectoryPage(
    IReadOnlyList<EmployeeDirectoryEntry> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

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
