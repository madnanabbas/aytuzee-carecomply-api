namespace CareComply.Domain.Forms;

public sealed class FormTemplateVersion
{
    public const int MaximumOriginalFileNameLength = 255;
    public const int MaximumStorageObjectKeyLength = 32;
    public const int MaximumContentTypeLength = 100;
    public const int Sha256Length = 64;

    private FormTemplateVersion()
    {
    }

    public Guid Id { get; private set; }
    public Guid FormTemplateId { get; private set; }
    public int VersionNumber { get; private set; }
    public FormTemplateVersionStatus Status { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StorageObjectKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public int PageCount { get; private set; }
    public FormTechnology FormTechnology { get; private set; }
    public DateOnly? EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public bool RequiresWarningAcknowledgement { get; private set; }
    public Guid? MappingsReviewedByUserId { get; private set; }
    public DateTimeOffset? MappingsReviewedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static FormTemplateVersion CreateDraft(
        Guid id,
        Guid formTemplateId,
        int versionNumber,
        string originalFileName,
        string storageObjectKey,
        string contentType,
        long sizeBytes,
        string sha256,
        int pageCount,
        FormTechnology formTechnology,
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAtUtc,
        bool requiresWarningAcknowledgement = false)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A form template version identifier is required.", nameof(id));
        if (formTemplateId == Guid.Empty)
            throw new ArgumentException("A form template identifier is required.", nameof(formTemplateId));
        if (uploadedByUserId == Guid.Empty)
            throw new ArgumentException("An uploading user identifier is required.", nameof(uploadedByUserId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versionNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);
        if (!Enum.IsDefined(formTechnology))
            throw new ArgumentOutOfRangeException(nameof(formTechnology));
        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo < effectiveFrom)
            throw new ArgumentException("The effective end date cannot precede the start date.", nameof(effectiveTo));

        var normalizedContentType = NormalizeRequired(
            contentType,
            MaximumContentTypeLength,
            nameof(contentType));
        if (!string.Equals(normalizedContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Form templates must be PDF documents.", nameof(contentType));

        return new FormTemplateVersion
        {
            Id = id,
            FormTemplateId = formTemplateId,
            VersionNumber = versionNumber,
            Status = FormTemplateVersionStatus.Draft,
            OriginalFileName = NormalizeRequired(
                originalFileName,
                MaximumOriginalFileNameLength,
                nameof(originalFileName)),
            StorageObjectKey = NormalizeObjectKey(storageObjectKey),
            ContentType = "application/pdf",
            SizeBytes = sizeBytes,
            Sha256 = NormalizeSha256(sha256),
            PageCount = pageCount,
            FormTechnology = formTechnology,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            UploadedByUserId = uploadedByUserId,
            UploadedAtUtc = EnsureUtc(uploadedAtUtc, nameof(uploadedAtUtc)),
            RequiresWarningAcknowledgement = requiresWarningAcknowledgement,
        };
    }

    public void MarkMappingsReviewed(Guid reviewedByUserId, DateTimeOffset reviewedAtUtc)
    {
        if (reviewedByUserId == Guid.Empty)
            throw new ArgumentException("A reviewing user identifier is required.", nameof(reviewedByUserId));
        if (Status != FormTemplateVersionStatus.Draft)
            throw new InvalidOperationException("Only draft form template mappings can be reviewed.");

        MappingsReviewedByUserId = reviewedByUserId;
        MappingsReviewedAtUtc = EnsureUtc(reviewedAtUtc, nameof(reviewedAtUtc));
    }

    public void Publish(Guid publishedByUserId, DateTimeOffset publishedAtUtc)
    {
        if (publishedByUserId == Guid.Empty)
            throw new ArgumentException("A publishing user identifier is required.", nameof(publishedByUserId));
        if (Status != FormTemplateVersionStatus.Draft)
            throw new InvalidOperationException("Only a draft form template version can be published.");
        if (FormTechnology == FormTechnology.Unsupported)
            throw new InvalidOperationException("An unsupported form template cannot be published.");

        Status = FormTemplateVersionStatus.Published;
        PublishedByUserId = publishedByUserId;
        PublishedAtUtc = EnsureUtc(publishedAtUtc, nameof(publishedAtUtc));
    }

    public void Retire()
    {
        if (Status != FormTemplateVersionStatus.Published)
            throw new InvalidOperationException("Only a published form template version can be retired.");
        Status = FormTemplateVersionStatus.Retired;
    }

    private static string NormalizeObjectKey(string value)
    {
        var normalized = NormalizeRequired(value, MaximumStorageObjectKeyLength, nameof(value));
        if (!Guid.TryParseExact(normalized, "N", out var objectId)
            || !string.Equals(normalized, objectId.ToString("N"), StringComparison.Ordinal))
        {
            throw new ArgumentException("A canonical opaque object key is required.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeSha256(string value)
    {
        var normalized = NormalizeRequired(value, Sha256Length, nameof(value)).ToLowerInvariant();
        if (normalized.Length != Sha256Length || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("A SHA-256 value must contain exactly 64 hexadecimal characters.", nameof(value));
        return normalized;
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
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
