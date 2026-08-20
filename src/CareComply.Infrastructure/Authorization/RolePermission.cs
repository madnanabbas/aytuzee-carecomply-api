using System.Diagnostics.CodeAnalysis;

namespace CareComply.Infrastructure.Authorization;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "RolePermission is the approved CareComply RBAC relationship term.")]
public sealed class RolePermission
{
    private RolePermission()
    {
    }

    public RolePermission(Guid roleId, int permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }

    public int PermissionId { get; private set; }
}
