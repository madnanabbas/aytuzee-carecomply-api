namespace CareComply.Domain.AccessManagement;

public sealed class RoleDefinition
{
    public const int MaximumNameLength = 256;
    public const int MaximumDescriptionLength = 500;

    private readonly HashSet<int> _permissionIds;

    private RoleDefinition(
        Guid id,
        string name,
        string description,
        IEnumerable<int> permissionIds)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A role identifier is required.", nameof(id));
        }

        Id = id;
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        _permissionIds = NormalizePermissionIds(permissionIds);
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public IReadOnlySet<int> PermissionIds => _permissionIds;

    public static RoleDefinition Create(
        Guid id,
        string name,
        string? description,
        IEnumerable<int> permissionIds) =>
        new(id, name, description ?? string.Empty, permissionIds);

    public static RoleDefinition Restore(
        Guid id,
        string name,
        string? description,
        IEnumerable<int> permissionIds) =>
        new(id, name, description ?? string.Empty, permissionIds);

    public bool ChangeGeneral(string name, string? description)
    {
        var normalizedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description ?? string.Empty);
        if (string.Equals(Name, normalizedName, StringComparison.Ordinal)
            && string.Equals(Description, normalizedDescription, StringComparison.Ordinal))
        {
            return false;
        }

        Name = normalizedName;
        Description = normalizedDescription;
        return true;
    }

    public RolePermissionChange ReplacePermissions(IEnumerable<int> permissionIds)
    {
        var requested = NormalizePermissionIds(permissionIds);
        var added = requested.Except(_permissionIds).Order().ToArray();
        var removed = _permissionIds.Except(requested).Order().ToArray();
        if (added.Length == 0 && removed.Length == 0)
        {
            return RolePermissionChange.None;
        }

        _permissionIds.Clear();
        _permissionIds.UnionWith(requested);
        return new RolePermissionChange(added, removed);
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var value = name.Trim();
        if (value.Length > MaximumNameLength)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "The role name is too long.");
        }

        return value;
    }

    private static string NormalizeDescription(string description)
    {
        var value = description.Trim();
        if (value.Length > MaximumDescriptionLength)
        {
            throw new ArgumentOutOfRangeException(nameof(description), "The role description is too long.");
        }

        return value;
    }

    private static HashSet<int> NormalizePermissionIds(IEnumerable<int> permissionIds)
    {
        ArgumentNullException.ThrowIfNull(permissionIds);
        var values = permissionIds.ToHashSet();
        if (values.Any(permissionId => permissionId <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(permissionIds), "Permission identifiers must be positive.");
        }

        return values;
    }
}

public sealed record RolePermissionChange(
    IReadOnlyList<int> AddedPermissionIds,
    IReadOnlyList<int> RemovedPermissionIds)
{
    public static RolePermissionChange None { get; } = new([], []);

    public bool HasChanges => AddedPermissionIds.Count > 0 || RemovedPermissionIds.Count > 0;
}
