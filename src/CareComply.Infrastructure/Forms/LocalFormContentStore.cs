using CareComply.Application.Abstractions.Forms;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Forms;

public sealed class LocalFormContentStore : IFormContentStore
{
    private const int BufferSize = 64 * 1024;
    private readonly string rootPath;

    private readonly long maximumUploadSizeBytes;

    public LocalFormContentStore(IOptions<FormContentStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configuredPath = options.Value.LocalRootPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                $"The required configuration '{FormContentStorageOptions.SectionName}:LocalRootPath' is missing.");
        }

        if (!Path.IsPathFullyQualified(configuredPath))
            throw new InvalidOperationException("The local form-content root path must be absolute.");

        rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredPath));
        if (ContainsWwwRootSegment(rootPath))
            throw new InvalidOperationException("The local form-content root path must be outside wwwroot.");

        maximumUploadSizeBytes = options.Value.MaximumUploadSizeBytes;
    }

    public async Task WriteAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        var destinationPath = ResolvePath(objectKey);
        Directory.CreateDirectory(rootPath);
        var temporaryPath = Path.Combine(rootPath, $".{Guid.CreateVersion7():N}.tmp");

        try
        {
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var boundedContent = new MaximumLengthReadStream(content, maximumUploadSizeBytes);
                await boundedContent.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destinationPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream content = new FileStream(
            ResolvePath(objectKey),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(content);
    }

    public Task DeleteUncommittedAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(objectKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string objectKey)
    {
        FormContentObjectKey.Validate(objectKey);

        var resolved = Path.GetFullPath(Path.Combine(rootPath, objectKey + ".pdf"));
        var expectedPrefix = rootPath + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!resolved.StartsWith(expectedPrefix, comparison))
            throw new ArgumentException("The form-content object key is invalid.", nameof(objectKey));
        return resolved;
    }

    private static bool ContainsWwwRootSegment(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => string.Equals(segment, "wwwroot", StringComparison.OrdinalIgnoreCase));
}
