namespace CareComply.Domain.AccessManagement;

public sealed class RoleProfile
{
    private RoleProfile()
    {
    }

    public RoleProfile(Guid roleId, string? description)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("A role identifier is required.", nameof(roleId));
        }

        RoleId = roleId;
        ChangeDescription(description);
    }

    public Guid RoleId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public bool ChangeDescription(string? description)
    {
        var value = (description ?? string.Empty).Trim();
        if (value.Length > RoleDefinition.MaximumDescriptionLength)
        {
            throw new ArgumentOutOfRangeException(nameof(description), "The role description is too long.");
        }

        if (string.Equals(Description, value, StringComparison.Ordinal))
        {
            return false;
        }

        Description = value;
        return true;
    }
}
