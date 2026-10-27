namespace CareComply.Application.Abstractions.Forms;

public interface IFormContentStore
{
    Task WriteAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken);

    Task DeleteUncommittedAsync(
        string objectKey,
        CancellationToken cancellationToken);
}
