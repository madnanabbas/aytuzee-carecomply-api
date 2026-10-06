using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Abstractions.Employees;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.Employees.UpdateEmployee;

public sealed record UpdateEmployeeCommand(Guid EmployeeId, SaveEmployeeRequest Request)
    : ICommand<UpdateEmployeeResult>, IRequirePermission, IAuditableRequest<UpdateEmployeeResult>
{
    public string RequiredPermission => Permissions.Employee.Edit;

    public AuditEvent CreateAuditEvent(UpdateEmployeeResult response)
    {
        var administration = response.Administration;
        var succeeded = administration?.Status == EmployeeAdministrationStatus.Succeeded;
        return new(
            "Employee.Updated",
            "Employee",
            EmployeeId.ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(
                    ("companyId", administration!.Employee!.CompanyId.ToString("N")),
                    ("locationCount", administration.Employee.LocationIds.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)))
                : AuditMetadata.Create(("reasonCode", EmployeeAuditReasonCode(response))));
    }

    private static string EmployeeAuditReasonCode(UpdateEmployeeResult response) =>
        response.AuthorizationFailure is not null
            ? "authorization-failed"
            : response.Administration?.Status switch
            {
                EmployeeAdministrationStatus.Unauthorized => "unauthorized",
                EmployeeAdministrationStatus.NotFound => "not-found",
                EmployeeAdministrationStatus.ValidationFailed => "validation-failed",
                EmployeeAdministrationStatus.Conflict => "conflict",
                _ => "operation-failed",
            };
}

public sealed record UpdateEmployeeResult(
    EmployeeAdministrationResult? Administration,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<UpdateEmployeeResult>
{
    public static UpdateEmployeeResult Completed(EmployeeAdministrationResult administration) =>
        new(administration, null);

    public static UpdateEmployeeResult Denied(AuthorizationFailure failure) =>
        new(null, failure);
}

public sealed class UpdateEmployeeCommandHandler(IEmployeeAdministration administration)
    : ICommandHandler<UpdateEmployeeCommand, UpdateEmployeeResult>
{
    public async ValueTask<UpdateEmployeeResult> Handle(
        UpdateEmployeeCommand command,
        CancellationToken cancellationToken) =>
        UpdateEmployeeResult.Completed(await administration.UpdateAsync(
            command.EmployeeId,
            command.Request,
            cancellationToken));
}
