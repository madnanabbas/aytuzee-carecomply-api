using System.Net;
using System.Text;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CareComply.Application.Abstractions.Forms;
using CareComply.Infrastructure.Forms;
using Microsoft.Extensions.Options;

namespace CareComply.Api.IntegrationTests;

public abstract class FormContentStoreContractTests : IDisposable
{
    private const long MaximumUploadSizeBytes = 32;

    protected abstract IFormContentStore CreateStore();

    [Fact]
    public async Task WritesReadsAndDeletesContentWithoutOverwriting()
    {
        var store = CreateStore();
        var objectKey = Guid.NewGuid().ToString("N");
        var expected = Encoding.UTF8.GetBytes("synthetic PDF test content");
        await using var source = new MemoryStream(expected);

        await store.WriteAsync(objectKey, source, CancellationToken.None);

        await using (var stored = await store.OpenReadAsync(objectKey, CancellationToken.None))
        {
            using var copy = new MemoryStream();
            await stored.CopyToAsync(copy);
            Assert.Equal(expected, copy.ToArray());
        }

        await using var replacement = new MemoryStream(Encoding.UTF8.GetBytes("replacement"));
        await Assert.ThrowsAsync<IOException>(() =>
            store.WriteAsync(objectKey, replacement, CancellationToken.None));

        await store.DeleteUncommittedAsync(objectKey, CancellationToken.None);
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            store.OpenReadAsync(objectKey, CancellationToken.None));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("folder/object")]
    [InlineData("C:\\temp\\object")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("ABCDEFABCDEFABCDEFABCDEFABCDEFAB")]
    public async Task RejectsNonCanonicalOrPathLikeObjectKeys(string objectKey)
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.OpenReadAsync(objectKey, CancellationToken.None));
    }

    [Fact]
    public async Task HonorsCancellationBeforeStorageAccess()
    {
        var store = CreateStore();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.OpenReadAsync(Guid.NewGuid().ToString("N"), cancellation.Token));
    }

    [Fact]
    public async Task RejectsContentBeyondConfiguredMaximumWhileStreaming()
    {
        var store = CreateStore();
        await using var source = new MemoryStream(new byte[MaximumUploadSizeBytes + 1]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.WriteAsync(Guid.NewGuid().ToString("N"), source, CancellationToken.None));
    }

    public virtual void Dispose() => GC.SuppressFinalize(this);

    protected static FormContentStorageOptions CreateOptions(string localRootPath = "") => new()
    {
        LocalRootPath = localRootPath,
        MaximumUploadSizeBytes = MaximumUploadSizeBytes,
    };
}

public sealed class LocalFormContentStoreContractTests : FormContentStoreContractTests
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        "carecomply-form-content-tests",
        Guid.NewGuid().ToString("N"));

    protected override IFormContentStore CreateStore() =>
        new LocalFormContentStore(Options.Create(CreateOptions(rootPath)));

    [Fact]
    public void RejectsMissingRelativeAndWwwrootConfiguration()
    {
        Assert.Throws<InvalidOperationException>(() => CreateLocalStore(string.Empty));
        Assert.Throws<InvalidOperationException>(() => CreateLocalStore("relative/forms"));
        Assert.Throws<InvalidOperationException>(() => CreateLocalStore(Path.Combine(
            Path.GetTempPath(), "application", "wwwroot", "forms")));
    }

    public override void Dispose()
    {
        if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
        base.Dispose();
    }

    private static LocalFormContentStore CreateLocalStore(string configuredPath) =>
        new(Options.Create(CreateOptions(configuredPath)));
}

public sealed class AzureBlobFormContentStoreContractTests : FormContentStoreContractTests
{
    private readonly FakeBlobContainerClient container = new();

    protected override IFormContentStore CreateStore() =>
        new AzureBlobFormContentStore(container, Options.Create(CreateOptions()));

    [Fact]
    public async Task UsesConditionalUploadAndForwardsCancellation()
    {
        var store = CreateStore();
        var key = Guid.NewGuid().ToString("N");
        await using var content = new MemoryStream([1, 2, 3]);
        using var cancellation = new CancellationTokenSource();

        await store.WriteAsync(key, content, cancellation.Token);

        Assert.Equal(ETag.All, container.LastUploadConditions?.IfNoneMatch);
        Assert.Equal("application/pdf", container.LastContentType);
        Assert.Equal(key + ".pdf", container.LastBlobName);
        Assert.Equal(cancellation.Token, container.LastCancellationToken);
        Assert.False(container.CreateContainerCalled);
    }

    [Fact]
    public async Task StorageFailureDoesNotExposeSdkErrorDetails()
    {
        var store = CreateStore();
        container.FailNextUpload = true;
        await using var content = new MemoryStream([1, 2, 3]);

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            store.WriteAsync(Guid.NewGuid().ToString("N"), content, CancellationToken.None));

        Assert.Equal("Form content storage operation failed.", exception.Message);
        Assert.DoesNotContain("synthetic-secret", exception.ToString(), StringComparison.Ordinal);
    }

    private sealed class FakeBlobContainerClient : BlobContainerClient
    {
        private readonly Dictionary<string, byte[]> blobs = [];

        public BlobRequestConditions? LastUploadConditions { get; set; }
        public string? LastContentType { get; set; }
        public CancellationToken LastCancellationToken { get; set; }
        public string? LastBlobName { get; set; }
        public bool FailNextUpload { get; set; }
        public bool CreateContainerCalled { get; set; }

        public override BlobClient GetBlobClient(string blobName)
        {
            LastBlobName = blobName;
            return new FakeBlobClient(this, blobName);
        }

        public override Task<Response<BlobContainerInfo>> CreateIfNotExistsAsync(
            PublicAccessType publicAccessType = PublicAccessType.None,
            IDictionary<string, string>? metadata = null,
            BlobContainerEncryptionScopeOptions? encryptionScopeOptions = null,
            CancellationToken cancellationToken = default)
        {
            CreateContainerCalled = true;
            throw new InvalidOperationException("The application must not create the container.");
        }

        private sealed class FakeBlobClient(FakeBlobContainerClient owner, string blobName) : BlobClient
        {
            public override async Task<Response<BlobContentInfo>> UploadAsync(
                Stream content,
                BlobUploadOptions options,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                owner.LastUploadConditions = options.Conditions;
                owner.LastContentType = options.HttpHeaders?.ContentType;
                owner.LastCancellationToken = cancellationToken;
                if (owner.FailNextUpload)
                {
                    owner.FailNextUpload = false;
                    throw new RequestFailedException(500, "synthetic-secret");
                }
                if (owner.blobs.ContainsKey(blobName))
                    throw new RequestFailedException(412, "Precondition failed.");

                using var copy = new MemoryStream();
                await content.CopyToAsync(copy, cancellationToken);
                owner.blobs.Add(blobName, copy.ToArray());
                var result = BlobsModelFactory.BlobContentInfo(
                    ETag.All,
                    DateTimeOffset.UtcNow,
                    Array.Empty<byte>(),
                    string.Empty,
                    string.Empty,
                    0);
                return Response.FromValue(result, new FakeResponse(201));
            }

            public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(
                HttpRange range = default,
                BlobRequestConditions? conditions = null,
                bool rangeGetContentHash = false,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!owner.blobs.TryGetValue(blobName, out var content))
                    throw new RequestFailedException(404, "Not found.");

                var result = BlobsModelFactory.BlobDownloadStreamingResult(
                    new MemoryStream(content, writable: false),
                    details: null);
                return Task.FromResult(Response.FromValue(result, new FakeResponse(200)));
            }

            public override Task<Response<bool>> DeleteIfExistsAsync(
                DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.IncludeSnapshots,
                BlobRequestConditions? conditions = null,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var removed = owner.blobs.Remove(blobName);
                return Task.FromResult(Response.FromValue(removed, new FakeResponse(202)));
            }
        }
    }

    private sealed class FakeResponse(int status) : Response
    {
        public override int Status => status;
        public override string ReasonPhrase => string.Empty;
        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; } = string.Empty;

        public override void Dispose()
        {
        }

        protected override bool ContainsHeader(string name) => false;
        protected override IEnumerable<HttpHeader> EnumerateHeaders() => [];
        protected override bool TryGetHeader(string name, out string value)
        {
            value = string.Empty;
            return false;
        }

        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
        {
            values = [];
            return false;
        }
    }
}
