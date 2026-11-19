using CareComply.Application.Abstractions.ClientDocuments;
using CareComply.Infrastructure.Forms;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.ClientDocuments;

public sealed class LocalClientDocumentContentStore : IClientDocumentContentStore
{
    private const int BufferSize = 64 * 1024;
    private readonly string rootPath;
    private readonly long maximumSize;

    public LocalClientDocumentContentStore(IOptions<FormContentStorageOptions> options)
    {
        var path = options.Value.ClientDocumentLocalRootPath;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("The local client-document storage configuration is incomplete or invalid.");
        rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (rootPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(x => string.Equals(x, "wwwroot", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Client-document content must be stored outside public web content.");
        maximumSize = options.Value.ClientDocumentMaximumUploadSizeBytes;
    }

    public async Task WriteAsync(string objectKey, Stream content, CancellationToken cancellationToken)
    {
        FormContentObjectKey.Validate(objectKey);
        var destination = Resolve(objectKey);
        Directory.CreateDirectory(rootPath);
        var temporary = Path.Combine(rootPath, $".{Guid.CreateVersion7():N}.tmp");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var bounded = new MaximumLengthReadStream(content, maximumSize);
                await bounded.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            File.Move(temporary, destination, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(Resolve(objectKey), FileMode.Open, FileAccess.Read,
            FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteUncommittedAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(objectKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string objectKey)
    {
        FormContentObjectKey.Validate(objectKey);
        var path = Path.GetFullPath(Path.Combine(rootPath, objectKey));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(rootPath + Path.DirectorySeparatorChar, comparison))
            throw new ArgumentException("The client-document object key is invalid.", nameof(objectKey));
        return path;
    }
}
