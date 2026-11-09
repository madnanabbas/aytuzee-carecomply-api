using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientForms;
using CareComply.Application.Features.ClientForms.Queries.GetClientFormBindings;
using CareComply.Application.Features.ClientForms.Queries.GetClientFormContent;

namespace CareComply.Application.Tests;

public sealed class ClientFormQueryTests
{
    [Fact]
    public void BindingCatalogIsExplicitAndContainsNoSsnKey()
    {
        Assert.All(ClientFormBindingKeys.All, key =>
            Assert.DoesNotContain("ssn", key, StringComparison.OrdinalIgnoreCase));
        Assert.False(ClientFormBindingKeys.IsAllowed("client.ssn"));
        Assert.False(ClientFormBindingKeys.IsAllowed("client.protectedSsn"));
    }

    [Fact]
    public async Task QueriesRequireClientFormViewAndForwardCancellation()
    {
        var directory = new RecordingDirectory();
        using var source = new CancellationTokenSource();
        var clientId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var query = new GetClientFormBindingsQuery(clientId, versionId);

        Assert.Equal(Permissions.ClientForm.View, query.RequiredPermission);
        await new GetClientFormBindingsQueryHandler(directory).Handle(query, source.Token);

        Assert.Equal(source.Token, directory.CancellationToken);
        Assert.Equal(clientId, directory.ClientId);
        Assert.Equal(versionId, directory.VersionId);
    }

    [Fact]
    public void AuditMetadataContainsIdentifiersButNeverBoundValues()
    {
        var clientId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var descriptor = new ClientFormContentDescriptor(
            versionId, "DHS-6893F", 1, Guid.NewGuid().ToString("N"), "application/pdf");

        var audit = new GetClientFormContentQuery(clientId, versionId).CreateAuditEvent(
            GetClientFormContentResult.Succeeded(descriptor));
        var serialized = string.Join('|',
            audit.Metadata.Entries.Select(item => $"{item.Key}={item.Value}"));

        Assert.Contains(clientId.ToString("N"), serialized, StringComparison.Ordinal);
        Assert.Contains("DHS-6893F", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("firstName", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("value", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingDirectory : IClientFormDirectory
    {
        public Guid ClientId { get; private set; }
        public Guid VersionId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<IReadOnlyList<ClientFormTemplateSummary>?> ListAvailableAsync(
            Guid clientId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ClientFormTemplateSummary>?>([]);

        public Task<ClientFormContentDescriptor?> GetContentDescriptorAsync(
            Guid clientId, Guid templateVersionId, CancellationToken cancellationToken) =>
            Task.FromResult<ClientFormContentDescriptor?>(null);

        public Task<ClientFormBindings?> GetBindingsAsync(
            Guid clientId, Guid templateVersionId, CancellationToken cancellationToken)
        {
            ClientId = clientId;
            VersionId = templateVersionId;
            CancellationToken = cancellationToken;
            return Task.FromResult<ClientFormBindings?>(null);
        }
    }
}
