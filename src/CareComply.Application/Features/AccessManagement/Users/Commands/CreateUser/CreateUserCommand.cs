using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? PhoneNumber,
    bool IsActive,
    bool RequiresMfa,
    UserMfaMethod MfaMethod,
    IReadOnlyList<Guid> RoleIds,
    Guid? CompanyId = null,
    IReadOnlyList<Guid>? LocationIds = null)
    : ICommand<CreateUserResult>, IRequirePermission, IAuditableRequest<CreateUserResult>
{
    public string RequiredPermission => Permissions.User.Manage;

    public AuditEvent CreateAuditEvent(CreateUserResult response)
    {
        var succeeded = response.Provisioning?.Status == UserProvisioningStatus.Succeeded;
        return new(
            "User.Created",
            "User",
            response.Provisioning?.UserId?.ToString("N") ?? "not-created",
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(
                    ("isActive", IsActive ? "true" : "false"),
                    ("requiresMfa", RequiresMfa ? "true" : "false"),
                    ("mfaMethod", MfaMethod.ToString()),
                    ("roleCount", RoleIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }
}
