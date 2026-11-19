namespace CareComply.Application.Abstractions.ClientDocuments;

public enum ClientDocumentFileValidationError
{
    UnsupportedType,
    TooLarge,
    Malformed,
}

public sealed record ClientDocumentFileValidationResult(
    IValidatedClientDocumentFile? File,
    ClientDocumentFileValidationError? Error);

public interface IValidatedClientDocumentFile : IAsyncDisposable
{
    string ContentType { get; }
    string Extension { get; }
    long SizeBytes { get; }
    string Sha256 { get; }
    Stream OpenRead();
}

public interface IClientDocumentFileValidator
{
    Task<ClientDocumentFileValidationResult> ValidateAsync(Stream content, CancellationToken cancellationToken);
}
