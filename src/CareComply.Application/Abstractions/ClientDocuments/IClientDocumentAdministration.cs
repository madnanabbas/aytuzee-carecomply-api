namespace CareComply.Application.Abstractions.ClientDocuments;

public enum ClientDocumentMutationStatus
{
    Succeeded,
    ClientNotFound,
    DocumentNotFound,
    Conflict,
    Archived,
    Duplicate,
}

public sealed record ClientDocumentItem(
    Guid Id,
    Guid ClientId,
    string Category,
    string Title,
    string? Description,
    DateOnly? ExpiryDate,
    DateOnly? ReviewDate,
    string Status,
    Guid CurrentVersionId,
    int CurrentVersionNumber,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string EditorVersion);

public sealed record ClientDocumentVersionItem(
    Guid Id,
    int VersionNumber,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAtUtc,
    bool IsCurrent);

public sealed record ClientDocumentDetails(
    ClientDocumentItem Document,
    IReadOnlyList<ClientDocumentVersionItem> Versions);

public sealed record ClientDocumentContent(
    string StorageObjectKey,
    string OriginalFileName,
    string ContentType,
    long SizeBytes);

public sealed record CreateClientDocumentData(
    Guid ClientId,
    string Category,
    string Title,
    string? Description,
    DateOnly? ExpiryDate,
    DateOnly? ReviewDate,
    string StorageObjectKey,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record UpdateClientDocumentData(
    Guid ClientId,
    Guid DocumentId,
    string Category,
    string Title,
    string? Description,
    DateOnly? ExpiryDate,
    DateOnly? ReviewDate,
    string EditorVersion,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record ReplaceClientDocumentData(
    Guid ClientId,
    Guid DocumentId,
    string EditorVersion,
    string StorageObjectKey,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record ArchiveClientDocumentData(
    Guid ClientId,
    Guid DocumentId,
    string EditorVersion,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record ClientDocumentMutationResult(
    ClientDocumentMutationStatus Status,
    ClientDocumentDetails? Details = null);

public interface IClientDocumentAdministration
{
    Task<bool> ClientIsAccessibleAsync(Guid clientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClientDocumentItem>?> ListAsync(Guid clientId, CancellationToken cancellationToken);
    Task<ClientDocumentDetails?> GetAsync(Guid clientId, Guid documentId, CancellationToken cancellationToken);
    Task<ClientDocumentContent?> GetContentAsync(Guid clientId, Guid documentId, Guid versionId, CancellationToken cancellationToken);
    Task<bool> HasDuplicateAsync(Guid clientId, string sha256, CancellationToken cancellationToken);
    Task<ClientDocumentMutationResult> CreateAsync(CreateClientDocumentData data, CancellationToken cancellationToken);
    Task<ClientDocumentMutationResult> UpdateAsync(UpdateClientDocumentData data, CancellationToken cancellationToken);
    Task<ClientDocumentMutationResult> ReplaceAsync(ReplaceClientDocumentData data, CancellationToken cancellationToken);
    Task<ClientDocumentMutationResult> ArchiveAsync(ArchiveClientDocumentData data, CancellationToken cancellationToken);
}
