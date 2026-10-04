using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Abstractions.Employees;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.Employees.CreateEmployee;

public sealed record CreateEmployeeCommand(SaveEmployeeRequest Request)
    : ICommand<CreateEmployeeResult>, IRequirePermission, IAuditableRequest<CreateEmployeeResult>
{
    public string RequiredPermission => Permissions.Employee.Create;

    public AuditEvent CreateAuditEvent(CreateEmployeeResult response)
    {
        var administration = response.Administration;
        var succeeded = administration?.Status == EmployeeAdministrationStatus.Succeeded
            && administration.Employee is not null;
        return new(
            "Employee.Created",
            "Employee",
            administration?.Employee?.Id.ToString("N") ?? "not-created",
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(
                    ("companyId", administration!.Employee!.CompanyId.ToString("N")),
                    ("locationCount", administration.Employee.LocationIds.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)))
                : AuditMetadata.Create(("reasonCode", EmployeeAuditReasonCode(response))));
    }

    private static string EmployeeAuditReasonCode(CreateEmployeeResult response) =>
        response.AuthorizationFailure is not null
            ? "authorization-failed"
            : response.Administration?.Status switch
            {
                EmployeeAdministrationStatus.Unauthorized => "unauthorized",
                EmployeeAdministrationStatus.ValidationFailed => "validation-failed",
                _ => "operation-failed",
            };
}

public sealed record CreateEmployeeResult(
    EmployeeAdministrationResult? Administration,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<CreateEmployeeResult>
{
    public static CreateEmployeeResult Completed(EmployeeAdministrationResult administration) =>
        new(administration, null);

    public static CreateEmployeeResult Denied(AuthorizationFailure failure) =>
        new(null, failure);
}

public sealed class CreateEmployeeCommandHandler(IEmployeeAdministration administration)
    : ICommandHandler<CreateEmployeeCommand, CreateEmployeeResult>
{
    public async ValueTask<CreateEmployeeResult> Handle(
        CreateEmployeeCommand command,
        CancellationToken cancellationToken) =>
        CreateEmployeeResult.Completed(await administration.CreateAsync(
            command.Request,
            cancellationToken));
}
