using CareComply.Infrastructure.ClientDocuments;
using CareComply.Infrastructure.Forms;
using Microsoft.Extensions.Options;

namespace CareComply.Api.IntegrationTests;

public sealed class ClientDocumentStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CareComplyClientDocuments", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LocalStoreWritesReadsPreventsOverwriteAndRejectsTraversal()
    {
        var store = CreateStore();
        var key = Guid.NewGuid().ToString("N");
        await store.WriteAsync(key, new MemoryStream("content"u8.ToArray()), CancellationToken.None);
        await using var opened = await store.OpenReadAsync(key, CancellationToken.None);
        using var reader = new StreamReader(opened);
        Assert.Equal("content", await reader.ReadToEndAsync());

        await Assert.ThrowsAsync<IOException>(() => store.WriteAsync(
            key, new MemoryStream("changed"u8.ToArray()), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => store.OpenReadAsync("../secret", CancellationToken.None));
    }

    [Fact]
    public async Task LocalStoreHonorsCancellationAndStreamingSizeLimit()
    {
        var store = CreateStore(maximumSize: 3);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.WriteAsync(
            Guid.NewGuid().ToString("N"), new MemoryStream("four"u8.ToArray()), CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.OpenReadAsync(
            Guid.NewGuid().ToString("N"), cancellation.Token));
    }

    [Fact]
    public async Task ValidatorAcceptsPngAndRejectsSpoofedAndOversizeFiles()
    {
        var validator = new ClientDocumentFileValidator();
        var validPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var valid = await validator.ValidateAsync(new MemoryStream(validPng), CancellationToken.None);
        Assert.NotNull(valid.File);
        Assert.Equal("image/png", valid.File!.ContentType);
        await valid.File.DisposeAsync();

        var spoofed = await validator.ValidateAsync(new MemoryStream("<html>not pdf</html>"u8.ToArray()), CancellationToken.None);
        Assert.Equal(CareComply.Application.Abstractions.ClientDocuments.ClientDocumentFileValidationError.UnsupportedType, spoofed.Error);

        var oversize = new byte[(10 * 1024 * 1024) + 1];
        var rejected = await validator.ValidateAsync(new MemoryStream(oversize), CancellationToken.None);
        Assert.Equal(CareComply.Application.Abstractions.ClientDocuments.ClientDocumentFileValidationError.TooLarge, rejected.Error);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
        GC.SuppressFinalize(this);
    }

    private LocalClientDocumentContentStore CreateStore(long maximumSize = 1024) => new(
        Options.Create(new FormContentStorageOptions
        {
            ClientDocumentLocalRootPath = root,
            ClientDocumentMaximumUploadSizeBytes = maximumSize,
        }));
}
