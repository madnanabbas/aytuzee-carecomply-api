using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.UpdateUserGeneral;

public sealed record UpdateUserGeneralCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Password,
    string EditorVersion,
    Guid? CompanyId = null,
    IReadOnlyList<Guid>? LocationIds = null)
    : ICommand<UpdateUserGeneralResult>, IRequirePermission,
        IAuditableRequest<UpdateUserGeneralResult>
{
    public string RequiredPermission => Permissions.User.Manage;

    public AuditEvent CreateAuditEvent(UpdateUserGeneralResult response)
    {
        var succeeded = response.Update?.Status is UserAdministrationStatus.Succeeded or UserAdministrationStatus.NoChanges;
        var metadata = succeeded
            ? AuditMetadata.Create(("changedFields", string.Join(',', response.Update!.ChangedFields)))
            : AuditMetadata.Create(("reasonCode", response.ReasonCode));
        return new(
            "User.GeneralUpdated",
            "User",
            UserId.ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            metadata);
    }
}
