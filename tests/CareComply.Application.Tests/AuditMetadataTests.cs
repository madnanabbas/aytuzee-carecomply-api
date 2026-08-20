using CareComply.Application.Auditing;

namespace CareComply.Application.Tests;

public sealed class AuditMetadataTests
{
    [Fact]
    public void CreateAcceptsBoundedSafeMetadataAndOrdersKeys()
    {
        var metadata = AuditMetadata.Create(
            ("documentVersion", "3"),
            ("changedFields", "ExpiryDate,ReviewDate"));

        Assert.Equal(
            ["changedFields", "documentVersion"],
            metadata.Entries.Select(entry => entry.Key));
        Assert.DoesNotContain("ExpiryDate", metadata.ToString(), StringComparison.Ordinal);
        Assert.Contains("[OMITTED]", metadata.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRejectsDuplicateInvalidAndOversizedValues()
    {
        Assert.Throws<ArgumentException>(() => AuditMetadata.Create(
            ("version", "1"),
            ("version", "2")));
        Assert.Throws<ArgumentException>(() => AuditMetadata.Create(("invalid-key", "1")));
        Assert.Throws<ArgumentException>(() => AuditMetadata.Create(
            ("value", new string('x', AuditMetadata.MaximumValueLength + 1))));
        Assert.Throws<ArgumentException>(() => AuditMetadata.Create(
            Enumerable.Range(0, AuditMetadata.MaximumEntryCount + 1)
                .Select(index => ($"key{index}", "value"))
                .ToArray()));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("passwordHash")]
    [InlineData("credential")]
    [InlineData("cookie")]
    [InlineData("antiforgeryToken")]
    [InlineData("accessToken")]
    [InlineData("refreshToken")]
    [InlineData("authorizationHeader")]
    [InlineData("securityStamp")]
    [InlineData("secret")]
    [InlineData("connectionString")]
    [InlineData("requestBody")]
    [InlineData("documentContent")]
    [InlineData("fileContent")]
    [InlineData("exception")]
    [InlineData("stackTrace")]
    public void CreateRejectsSensitiveMetadataKeys(string key) =>
        Assert.Throws<ArgumentException>(() => AuditMetadata.Create((key, "value")));

    [Fact]
    public void AuditEventValidatesServerOwnedIdentifiersAndOmitsMetadataFormatting()
    {
        var secret = $"safe-metadata-{Guid.NewGuid():N}";
        var auditEvent = new AuditEvent(
            "Client.Updated",
            "Client",
            Guid.CreateVersion7().ToString("N"),
            AuditOutcome.Succeeded,
            AuditMetadata.Create(("changedFields", secret)));

        Assert.DoesNotContain(secret, auditEvent.ToString(), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => new AuditEvent(
            "Invalid Action",
            "Client",
            "1",
            AuditOutcome.Succeeded));
        Assert.Throws<ArgumentException>(() => new AuditEvent(
            "Client.Updated",
            "Invalid.Resource",
            "1",
            AuditOutcome.Succeeded));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuditEvent(
            "Client.Updated",
            "Client",
            "1",
            (AuditOutcome)999));
    }
}
