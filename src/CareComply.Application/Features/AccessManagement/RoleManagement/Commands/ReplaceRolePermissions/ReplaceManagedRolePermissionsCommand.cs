using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.ReplaceRolePermissions;

public sealed record ReplaceManagedRolePermissionsCommand(
    Guid RoleId,
    IReadOnlyList<int> PermissionIds,
    string EditorVersion)
    : ICommand<ReplaceManagedRolePermissionsResult>, IRequirePermission, IAuditableRequest<ReplaceManagedRolePermissionsResult>
{
    private const int IdsPerMetadataEntry = 12;

    public string RequiredPermission => Permissions.Role.Manage;

    public AuditEvent CreateAuditEvent(ReplaceManagedRolePermissionsResult response)
    {
        var mutation = response.Mutation;
        var succeeded = mutation?.Status is RoleMutationStatus.Succeeded
            or RoleMutationStatus.NoChanges;
        return new(
            "Role.PermissionsChanged",
            "Role",
            RoleId.ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded && mutation is not null
                ? CreateMetadata(mutation)
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }

    private static AuditMetadata CreateMetadata(RolePermissionMutationResult mutation)
    {
        var entries = new List<(string Key, string Value)>();
        AddIds(entries, "addedIds", mutation.AddedPermissionIds);
        AddIds(entries, "removedIds", mutation.RemovedPermissionIds);
        return entries.Count == 0 ? AuditMetadata.Empty : AuditMetadata.Create([.. entries]);
    }

    private static void AddIds(List<(string Key, string Value)> entries, string prefix, IReadOnlyList<int> ids)
    {
        var ordered = ids.Order().ToArray();
        for (var offset = 0; offset < ordered.Length; offset += IdsPerMetadataEntry)
        {
            entries.Add(($"{prefix}{offset / IdsPerMetadataEntry + 1}", string.Join(',', ordered.Skip(offset).Take(IdsPerMetadataEntry))));
        }
    }
}
