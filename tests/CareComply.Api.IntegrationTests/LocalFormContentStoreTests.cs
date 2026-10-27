using System.Text;
using CareComply.Infrastructure.Forms;
using Microsoft.Extensions.Options;

namespace CareComply.Api.IntegrationTests;

public sealed class LocalFormContentStoreTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        "carecomply-form-content-tests",
        Guid.NewGuid().ToString("N"));

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
    public void RejectsMissingRelativeAndWwwrootConfiguration()
    {
        Assert.Throws<InvalidOperationException>(() => CreateStore(string.Empty));
        Assert.Throws<InvalidOperationException>(() => CreateStore("relative/forms"));
        Assert.Throws<InvalidOperationException>(() => CreateStore(Path.Combine(
            Path.GetTempPath(), "application", "wwwroot", "forms")));
    }

    [Fact]
    public async Task HonorsCancellationBeforeFilesystemAccess()
    {
        var store = CreateStore();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.OpenReadAsync(Guid.NewGuid().ToString("N"), cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
        GC.SuppressFinalize(this);
    }

    private LocalFormContentStore CreateStore(string? configuredPath = null) =>
        new(Options.Create(new LocalFormContentStoreOptions
        {
            LocalRootPath = configuredPath ?? rootPath,
        }));
}
