namespace CareComply.Application.Auditing;

public enum AuditOutcome
{
    Succeeded,
    Failed,
}

public sealed class AuditEvent
{
    private const int MaximumActionLength = 128;
    private const int MaximumResourceTypeLength = 64;
    private const int MaximumResourceIdLength = 128;

    public AuditEvent(
        string action,
        string resourceType,
        string resourceId,
        AuditOutcome outcome,
        AuditMetadata? metadata = null)
    {
        Action = ValidateQualifiedName(action, MaximumActionLength, nameof(action));
        ResourceType = ValidateResourceType(resourceType);
        ResourceId = ValidateResourceId(resourceId);
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "The audit outcome is invalid.");
        }

        Outcome = outcome;
        Metadata = metadata ?? AuditMetadata.Empty;
    }

    public string Action { get; }

    public string ResourceType { get; }

    public string ResourceId { get; }

    public AuditOutcome Outcome { get; }

    public AuditMetadata Metadata { get; }

    public override string ToString() =>
        "AuditEvent { Metadata = [OMITTED] }";

    private static string ValidateQualifiedName(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        if (value.Length > maximumLength
            || value[0] == '.'
            || value[^1] == '.'
            || value.Contains("..", StringComparison.Ordinal)
            || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '.')))
        {
            throw new ArgumentException(
                "An audit action must be a bounded dot-separated server-owned name.",
                parameterName);
        }

        return value;
    }

    private static string ValidateResourceType(string resourceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);

        if (resourceType.Length > MaximumResourceTypeLength
            || !char.IsAsciiLetter(resourceType[0])
            || resourceType.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException(
                "An audit resource type must be a bounded server-owned name.",
                nameof(resourceType));
        }

        return resourceType;
    }

    private static string ValidateResourceId(string resourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

        if (resourceId.Length > MaximumResourceIdLength
            || resourceId.Any(char.IsControl))
        {
            throw new ArgumentException(
                "An audit resource identifier must be bounded and contain no control characters.",
                nameof(resourceId));
        }

        return resourceId;
    }
}
