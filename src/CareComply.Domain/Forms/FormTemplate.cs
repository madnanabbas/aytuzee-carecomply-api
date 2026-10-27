namespace CareComply.Domain.Forms;

public sealed class FormTemplate
{
    public const int MaximumKeyLength = 100;
    public const int MaximumNameLength = 200;
    public const int MaximumDescriptionLength = 1000;
    public const int MaximumCategoryLength = 100;

    private FormTemplate()
    {
    }

    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public FormTemplateOwnerType OwnerType { get; private set; }
    public Guid? CompanyId { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static FormTemplate Create(
        Guid id,
        string key,
        string name,
        string? description,
        string category,
        FormTemplateOwnerType ownerType,
        Guid? companyId,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A form template identifier is required.", nameof(id));
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("A creating user identifier is required.", nameof(createdByUserId));
        if (!Enum.IsDefined(ownerType))
            throw new ArgumentOutOfRangeException(nameof(ownerType));

        ValidateOwnership(ownerType, companyId);
        var timestamp = EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        return new FormTemplate
        {
            Id = id,
            Key = NormalizeKey(key),
            Name = NormalizeRequired(name, MaximumNameLength, nameof(name)),
            Description = NormalizeOptional(description, MaximumDescriptionLength, nameof(description)),
            Category = NormalizeRequired(category, MaximumCategoryLength, nameof(category)),
            OwnerType = ownerType,
            CompanyId = companyId,
            IsActive = true,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp,
        };
    }

    private static void ValidateOwnership(FormTemplateOwnerType ownerType, Guid? companyId)
    {
        if (ownerType == FormTemplateOwnerType.Platform && companyId is not null)
        {
            throw new ArgumentException(
                "A platform-owned template cannot have a company identifier.",
                nameof(companyId));
        }

        if (ownerType == FormTemplateOwnerType.Company
            && (!companyId.HasValue || companyId.Value == Guid.Empty))
        {
            throw new ArgumentException(
                "A company-owned template requires a company identifier.",
                nameof(companyId));
        }
    }

    private static string NormalizeKey(string value)
    {
        var normalized = NormalizeRequired(value, MaximumKeyLength, nameof(value));
        if (normalized.Any(character => !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException(
                "A template key may contain only ASCII letters, digits, hyphens, underscores, and periods.",
                nameof(value));
        }

        return normalized;
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static string NormalizeOptional(string? value, int maximumLength, string parameterName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("The timestamp must be UTC.", parameterName);
        return value;
    }
}
