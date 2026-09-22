namespace CareComply.Application.Abstractions.Employees;

public interface IEmployeeDetailsReader
{
    Task<EmployeeDetailsResponse?> GetAsync(Guid employeeId, CancellationToken cancellationToken);
}
