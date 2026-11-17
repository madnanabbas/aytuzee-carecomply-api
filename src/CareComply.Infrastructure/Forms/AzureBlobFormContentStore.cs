using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CareComply.Application.Abstractions.Forms;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Forms;

public sealed class AzureBlobFormContentStore(
    BlobContainerClient containerClient,
    IOptions<FormContentStorageOptions> options) : IFormContentStore
{
    public async Task WriteAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        FormContentObjectKey.Validate(objectKey);

        using var boundedContent = new MaximumLengthReadStream(
            content,
            options.Value.MaximumUploadSizeBytes);
        var uploadOptions = new BlobUploadOptions
        {
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" },
        };

        try
        {
            await containerClient.GetBlobClient(GetBlobName(objectKey))
                .UploadAsync(boundedContent, uploadOptions, cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            throw new IOException("Form content already exists.");
        }
        catch (RequestFailedException)
        {
            throw new IOException("Form content storage operation failed.");
        }
    }

    public async Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FormContentObjectKey.Validate(objectKey);

        try
        {
            var response = await containerClient.GetBlobClient(GetBlobName(objectKey))
                .DownloadStreamingAsync(
                    range: default,
                    conditions: null,
                    rangeGetContentHash: false,
                    cancellationToken);
            return response.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            throw new FileNotFoundException("Form content was not found.");
        }
        catch (RequestFailedException)
        {
            throw new IOException("Form content storage operation failed.");
        }
    }

    public async Task DeleteUncommittedAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FormContentObjectKey.Validate(objectKey);

        try
        {
            await containerClient.GetBlobClient(GetBlobName(objectKey))
                .DeleteIfExistsAsync(
                    DeleteSnapshotsOption.IncludeSnapshots,
                    conditions: null,
                    cancellationToken);
        }
        catch (RequestFailedException)
        {
            throw new IOException("Form content storage operation failed.");
        }
    }

    private static string GetBlobName(string objectKey) => objectKey + ".pdf";
}
