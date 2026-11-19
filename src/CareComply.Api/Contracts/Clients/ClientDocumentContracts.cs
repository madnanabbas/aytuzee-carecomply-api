namespace CareComply.Api.Contracts.Clients;

public sealed record ClientDocumentResponse(
    Guid Id, Guid ClientId, string Category, string Title, string? Description,
    DateOnly? ExpiryDate, DateOnly? ReviewDate, string Status, Guid CurrentVersionId,
    int CurrentVersionNumber, string OriginalFileName, string ContentType, long SizeBytes,
    DateTimeOffset UploadedAtUtc, DateTimeOffset UpdatedAtUtc, string EditorVersion);

public sealed record ClientDocumentVersionResponse(
    Guid Id, int VersionNumber, string OriginalFileName, string ContentType,
    long SizeBytes, DateTimeOffset UploadedAtUtc, bool IsCurrent);

public sealed record ClientDocumentDetailsResponse(
    ClientDocumentResponse Document, IReadOnlyList<ClientDocumentVersionResponse> Versions);

public sealed class UploadClientDocumentRequest
{
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public DateOnly? ReviewDate { get; init; }
    public IFormFile? File { get; init; }
}

public sealed record UpdateClientDocumentRequest(
    string Category, string Title, string? Description, DateOnly? ExpiryDate,
    DateOnly? ReviewDate, string EditorVersion);

public sealed class ReplaceClientDocumentRequest
{
    public string EditorVersion { get; init; } = string.Empty;
    public IFormFile? File { get; init; }
}

public sealed record ArchiveClientDocumentRequest(string EditorVersion);
