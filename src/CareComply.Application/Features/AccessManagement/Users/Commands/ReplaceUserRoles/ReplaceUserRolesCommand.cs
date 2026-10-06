using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.ReplaceUserRoles;

public sealed record ReplaceUserRolesCommand(
    Guid UserId,
    IReadOnlyList<Guid> RoleIds,
    string EditorVersion)
    : ICommand<ReplaceUserRolesResult>, IRequirePermission,
        IAuditableRequest<ReplaceUserRolesResult>
{
    private const int RoleIdsPerMetadataEntry = 7;

    public string RequiredPermission => Permissions.User.Manage;

    public AuditEvent CreateAuditEvent(ReplaceUserRolesResult response)
    {
        var succeeded = response.Update?.Status == UserRoleUpdateStatus.Succeeded;
        return new(
            "User.RolesChanged",
            "User",
            UserId.ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? CreateRoleMetadata(response.Update!)
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }

    private static AuditMetadata CreateRoleMetadata(UserRoleUpdateResult update)
    {
        var entries = new List<(string Key, string Value)>(4);
        AddRoleEntries(entries, "addedRoleIds", update.AddedRoleIds);
        AddRoleEntries(entries, "removedRoleIds", update.RemovedRoleIds);
        return entries.Count == 0 ? AuditMetadata.Empty : AuditMetadata.Create([.. entries]);
    }

    private static void AddRoleEntries(
        List<(string Key, string Value)> entries,
        string keyPrefix,
        IReadOnlyList<Guid> roleIds)
    {
        var orderedIds = roleIds.Order().Select(roleId => roleId.ToString("N")).ToArray();
        for (var offset = 0; offset < orderedIds.Length; offset += RoleIdsPerMetadataEntry)
        {
            var sequence = offset / RoleIdsPerMetadataEntry + 1;
            entries.Add((
                $"{keyPrefix}{sequence}",
                string.Join(',', orderedIds.Skip(offset).Take(RoleIdsPerMetadataEntry))));
        }
    }
}
