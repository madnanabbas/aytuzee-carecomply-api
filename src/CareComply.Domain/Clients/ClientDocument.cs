namespace CareComply.Domain.Clients;

public enum ClientDocumentStatus
{
    Active = 1,
    Archived = 2,
}

public sealed class ClientDocument
{
    public const int MaximumCategoryLength = 100;
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 2000;

    private ClientDocument() { }

    private ClientDocument(
        Guid id,
        Guid companyId,
        Guid clientId,
        string category,
        string title,
        string? description,
        DateOnly? expiryDate,
        DateOnly? reviewDate,
        Guid currentVersionId,
        Guid actorUserId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("A document identifier is required.", nameof(id));
        if (companyId == Guid.Empty) throw new ArgumentException("A company identifier is required.", nameof(companyId));
        if (clientId == Guid.Empty) throw new ArgumentException("A client identifier is required.", nameof(clientId));
        if (currentVersionId == Guid.Empty) throw new ArgumentException("A current version identifier is required.", nameof(currentVersionId));
        if (actorUserId == Guid.Empty) throw new ArgumentException("An actor identifier is required.", nameof(actorUserId));

        Id = id;
        CompanyId = companyId;
        ClientId = clientId;
        CurrentVersionId = currentVersionId;
        Status = ClientDocumentStatus.Active;
        CreatedByUserId = actorUserId;
        UpdatedByUserId = actorUserId;
        CreatedAtUtc = EnsureUtc(nowUtc, nameof(nowUtc));
        UpdatedAtUtc = CreatedAtUtc;
        SetMetadata(category, title, description, expiryDate, reviewDate);
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid ClientId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateOnly? ExpiryDate { get; private set; }
    public DateOnly? ReviewDate { get; private set; }
    public ClientDocumentStatus Status { get; private set; }
    public Guid CurrentVersionId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ClientDocument Create(
        Guid id,
        Guid companyId,
        Guid clientId,
        string category,
        string title,
        string? description,
        DateOnly? expiryDate,
        DateOnly? reviewDate,
        Guid currentVersionId,
        Guid actorUserId,
        DateTimeOffset nowUtc) =>
        new(id, companyId, clientId, category, title, description, expiryDate,
            reviewDate, currentVersionId, actorUserId, nowUtc);

    public void UpdateMetadata(
        string category,
        string title,
        string? description,
        DateOnly? expiryDate,
        DateOnly? reviewDate,
        Guid actorUserId,
        DateTimeOffset nowUtc)
    {
        EnsureActive();
        SetMetadata(category, title, description, expiryDate, reviewDate);
        Touch(actorUserId, nowUtc);
    }

    public void ReplaceCurrentVersion(Guid versionId, Guid actorUserId, DateTimeOffset nowUtc)
    {
        EnsureActive();
        if (versionId == Guid.Empty) throw new ArgumentException("A version identifier is required.", nameof(versionId));
        CurrentVersionId = versionId;
        Touch(actorUserId, nowUtc);
    }

    public void Archive(Guid actorUserId, DateTimeOffset nowUtc)
    {
        EnsureActive();
        Status = ClientDocumentStatus.Archived;
        Touch(actorUserId, nowUtc);
    }

    private void SetMetadata(
        string category,
        string title,
        string? description,
        DateOnly? expiryDate,
        DateOnly? reviewDate)
    {
        Category = NormalizeRequired(category, MaximumCategoryLength, nameof(category));
        Title = NormalizeRequired(title, MaximumTitleLength, nameof(title));
        Description = Normalize(description, MaximumDescriptionLength, nameof(description));
        ExpiryDate = expiryDate;
        ReviewDate = reviewDate;
    }

    private void EnsureActive()
    {
        if (Status != ClientDocumentStatus.Active)
            throw new InvalidOperationException("An archived document cannot be changed.");
    }

    private void Touch(Guid actorUserId, DateTimeOffset nowUtc)
    {
        if (actorUserId == Guid.Empty) throw new ArgumentException("An actor identifier is required.", nameof(actorUserId));
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = EnsureUtc(nowUtc, nameof(nowUtc));
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Normalize(value, maximumLength, parameterName);
    }

    private static string Normalize(string? value, int maximumLength, string parameterName)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maximumLength} characters.");
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName) =>
        value.Offset == TimeSpan.Zero
            ? value
            : throw new ArgumentException("The timestamp must be UTC.", parameterName);
}

public sealed class ClientDocumentVersion
{
    public const int MaximumOriginalFileNameLength = 255;
    public const int MaximumContentTypeLength = 100;
    public const int Sha256Length = 64;

    private ClientDocumentVersion() { }

    private ClientDocumentVersion(
        Guid id,
        Guid documentId,
        Guid clientId,
        int versionNumber,
        string storageObjectKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("A version identifier is required.", nameof(id));
        if (documentId == Guid.Empty) throw new ArgumentException("A document identifier is required.", nameof(documentId));
        if (clientId == Guid.Empty) throw new ArgumentException("A client identifier is required.", nameof(clientId));
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNumber, 1);
        if (!Guid.TryParseExact(storageObjectKey, "N", out var parsedKey)
            || !string.Equals(storageObjectKey, parsedKey.ToString("N"), StringComparison.Ordinal))
            throw new ArgumentException("An opaque storage key is required.", nameof(storageObjectKey));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);
        if (uploadedByUserId == Guid.Empty) throw new ArgumentException("An uploader is required.", nameof(uploadedByUserId));

        Id = id;
        DocumentId = documentId;
        ClientId = clientId;
        VersionNumber = versionNumber;
        StorageObjectKey = storageObjectKey;
        OriginalFileName = NormalizeRequired(originalFileName, MaximumOriginalFileNameLength, nameof(originalFileName));
        ContentType = NormalizeRequired(contentType, MaximumContentTypeLength, nameof(contentType));
        Sha256 = ValidateSha256(sha256);
        SizeBytes = sizeBytes;
        UploadedByUserId = uploadedByUserId;
        UploadedAtUtc = uploadedAtUtc.Offset == TimeSpan.Zero
            ? uploadedAtUtc
            : throw new ArgumentException("The timestamp must be UTC.", nameof(uploadedAtUtc));
        IsCurrent = true;
    }

    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid ClientId { get; private set; }
    public int VersionNumber { get; private set; }
    public string StorageObjectKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }
    public bool IsCurrent { get; private set; }

    public static ClientDocumentVersion Create(
        Guid id,
        Guid documentId,
        Guid clientId,
        int versionNumber,
        string storageObjectKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAtUtc) =>
        new(id, documentId, clientId, versionNumber, storageObjectKey, originalFileName,
            contentType, sizeBytes, sha256, uploadedByUserId, uploadedAtUtc);

    public void MarkReplaced()
    {
        if (!IsCurrent) throw new InvalidOperationException("The version is not current.");
        IsCurrent = false;
    }

    private static string ValidateSha256(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != Sha256Length || value.Any(character => !char.IsAsciiHexDigit(character) || char.IsUpper(character)))
            throw new ArgumentException("A lowercase SHA-256 value is required.", nameof(value));
        return value;
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maximumLength} characters.");
    }
}
