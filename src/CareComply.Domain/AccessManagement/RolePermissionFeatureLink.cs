namespace CareComply.Domain.AccessManagement;

public sealed class RolePermissionFeatureLink
{
    private RolePermissionFeatureLink()
    {
    }

    public RolePermissionFeatureLink(
        int featureId,
        int permissionId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(featureId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permissionId);

        FeatureId = featureId;
        PermissionId = permissionId;
    }

    public int FeatureId { get; private set; }

    public int PermissionId { get; private set; }
}