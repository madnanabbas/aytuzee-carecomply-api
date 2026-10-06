namespace CareComply.Application.Abstractions.Employees;

public interface IEmployeeAdministration
{
    Task<EmployeeAdministrationResult> CreateAsync(
        SaveEmployeeRequest request,
        CancellationToken cancellationToken);

    Task<EmployeeAdministrationResult> UpdateAsync(
        Guid employeeId,
        SaveEmployeeRequest request,
        CancellationToken cancellationToken);
}

public interface IEmployeeSensitiveData
{
    string ProtectSsn(string ssn);

    string ProtectWorkforceSecret(string secret);
}

public sealed record EmployeeAdministrationResult(
    EmployeeAdministrationStatus Status,
    EmployeeDetailsResponse? Employee = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public enum EmployeeAdministrationStatus
{
    Succeeded,
    Unauthorized,
    NotFound,
    ValidationFailed,
    Conflict,
}
