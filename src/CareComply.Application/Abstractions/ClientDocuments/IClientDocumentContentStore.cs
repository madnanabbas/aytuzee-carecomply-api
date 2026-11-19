namespace CareComply.Application.Abstractions.ClientDocuments;

public interface IClientDocumentContentStore
{
    Task WriteAsync(string objectKey, Stream content, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteUncommittedAsync(string objectKey, CancellationToken cancellationToken);
}
