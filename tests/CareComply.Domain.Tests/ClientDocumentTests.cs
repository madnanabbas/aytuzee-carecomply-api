using CareComply.Domain.Clients;

namespace CareComply.Domain.Tests;

public sealed class ClientDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateNormalizesMetadataAndStartsActive()
    {
        var document = ClientDocument.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            " Records ", " Care plan ", null, null, null, Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Equal("Records", document.Category);
        Assert.Equal("Care plan", document.Title);
        Assert.Equal(ClientDocumentStatus.Active, document.Status);
        Assert.Empty(document.Description);
    }

    [Fact]
    public void ArchivedDocumentCannotBeChanged()
    {
        var actor = Guid.NewGuid();
        var document = ClientDocument.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Records", "Care plan", null, null, null, Guid.NewGuid(), actor, Now);
        document.Archive(actor, Now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() => document.UpdateMetadata(
            "Records", "Changed", null, null, null, actor, Now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => document.ReplaceCurrentVersion(
            Guid.NewGuid(), actor, Now.AddMinutes(2)));
    }

    [Fact]
    public void VersionRequiresCanonicalOpaqueKeyAndLowercaseHash()
    {
        Assert.Throws<ArgumentException>(() => ClientDocumentVersion.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "../content", "file.pdf",
            "application/pdf", 10, new string('a', 64), Guid.NewGuid(), Now));
        Assert.Throws<ArgumentException>(() => ClientDocumentVersion.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid().ToString("N"), "file.pdf",
            "application/pdf", 10, new string('A', 64), Guid.NewGuid(), Now));
    }
}
