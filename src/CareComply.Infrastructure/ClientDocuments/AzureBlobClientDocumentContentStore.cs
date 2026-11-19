using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CareComply.Application.Abstractions.ClientDocuments;
using CareComply.Infrastructure.Forms;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.ClientDocuments;

public sealed record ClientDocumentBlobContainer(BlobContainerClient Client);

public sealed class AzureBlobClientDocumentContentStore(
    ClientDocumentBlobContainer container,
    IOptions<FormContentStorageOptions> options) : IClientDocumentContentStore
{
    public async Task WriteAsync(string objectKey, Stream content, CancellationToken cancellationToken)
    {
        FormContentObjectKey.Validate(objectKey);
        using var bounded = new MaximumLengthReadStream(content, options.Value.ClientDocumentMaximumUploadSizeBytes);
        try
        {
            await container.Client.GetBlobClient(objectKey).UploadAsync(bounded,
                new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } }, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        { throw new IOException("Client document content already exists."); }
        catch (RequestFailedException) { throw new IOException("Client document storage operation failed."); }
    }

    public async Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        FormContentObjectKey.Validate(objectKey);
        try { return (await container.Client.GetBlobClient(objectKey).DownloadStreamingAsync(cancellationToken: cancellationToken)).Value.Content; }
        catch (RequestFailedException ex) when (ex.Status == 404)
        { throw new FileNotFoundException("Client document content was not found."); }
        catch (RequestFailedException) { throw new IOException("Client document storage operation failed."); }
    }

    public async Task DeleteUncommittedAsync(string objectKey, CancellationToken cancellationToken)
    {
        FormContentObjectKey.Validate(objectKey);
        try { await container.Client.GetBlobClient(objectKey).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken); }
        catch (RequestFailedException) { throw new IOException("Client document storage operation failed."); }
    }
}
