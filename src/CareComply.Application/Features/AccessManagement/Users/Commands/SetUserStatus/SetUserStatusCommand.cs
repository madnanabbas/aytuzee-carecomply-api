using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.SetUserStatus;

public sealed record SetUserStatusCommand(Guid UserId, bool IsActive, string EditorVersion)
    : ICommand<SetUserStatusResult>, IRequirePermission, IAuditableRequest<SetUserStatusResult>
{
    public string RequiredPermission => Permissions.User.Manage;

    public AuditEvent CreateAuditEvent(SetUserStatusResult response)
    {
        var succeeded = response.Update?.Status == UserAdministrationStatus.Succeeded;
        return new(
            IsActive ? "User.Activated" : "User.Deactivated",
            "User",
            UserId.ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Empty
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }
}
