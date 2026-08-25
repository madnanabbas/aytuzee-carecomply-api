using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.SetUserMfa;

public sealed record SetUserMfaCommand(
    Guid UserId,
    bool IsEnabled,
    UserMfaMethod Method,
    string EditorVersion)
    : ICommand<SetUserMfaResult>, IRequirePermission, IAuditableRequest<SetUserMfaResult>
{
    public string RequiredPermission => Permissions.User.Manage;

    public AuditEvent CreateAuditEvent(SetUserMfaResult response)
    {
        var succeeded = response.Update?.Status is
            UserMfaUpdateStatus.Succeeded or UserMfaUpdateStatus.MethodChanged;
        var action = response.Update?.Status == UserMfaUpdateStatus.MethodChanged
            ? "User.MfaMethodChanged"
            : IsEnabled ? "User.MfaEnabled" : "User.MfaDisabled";
        return new(
            action,
            "User",
            UserId.ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(("mfaMethod", Method.ToString()))
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }
}
