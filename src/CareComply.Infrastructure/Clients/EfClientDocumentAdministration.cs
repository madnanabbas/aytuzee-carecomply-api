using System.Security.Cryptography;
using System.Text;
using CareComply.Application.Abstractions.ClientDocuments;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientDocumentAdministration(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantScopeAccessor) : IClientDocumentAdministration
{
    public async Task<bool> ClientIsAccessibleAsync(Guid clientId, CancellationToken ct) =>
        await (await AccessibleClients(ct)).AnyAsync(x => x.Id == clientId, ct);

    public async Task<IReadOnlyList<ClientDocumentItem>?> ListAsync(Guid clientId, CancellationToken ct)
    {
        if (!await ClientIsAccessibleAsync(clientId, ct)) return null;
        var rows = await (from document in context.ClientDocuments.AsNoTracking()
                          join version in context.ClientDocumentVersions.AsNoTracking()
                              on document.CurrentVersionId equals version.Id
                          where document.ClientId == clientId
                          orderby document.Title
                          select new { Document = document, Version = version }).ToArrayAsync(ct);
        return rows.Select(x => Map(x.Document, x.Version)).ToArray();
    }

    public async Task<ClientDocumentDetails?> GetAsync(Guid clientId, Guid documentId, CancellationToken ct)
    {
        if (!await ClientIsAccessibleAsync(clientId, ct)) return null;
        var row = await (from document in context.ClientDocuments.AsNoTracking()
                         join version in context.ClientDocumentVersions.AsNoTracking()
                             on document.CurrentVersionId equals version.Id
                         where document.ClientId == clientId && document.Id == documentId
                         select new { Document = document, Version = version }).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var versions = await context.ClientDocumentVersions.AsNoTracking()
            .Where(x => x.DocumentId == documentId).OrderByDescending(x => x.VersionNumber)
            .Select(x => new ClientDocumentVersionItem(x.Id, x.VersionNumber, x.OriginalFileName,
                x.ContentType, x.SizeBytes, x.UploadedAtUtc, x.IsCurrent)).ToArrayAsync(ct);
        return new(Map(row.Document, row.Version), versions);
    }

    public async Task<ClientDocumentContent?> GetContentAsync(
        Guid clientId, Guid documentId, Guid versionId, CancellationToken ct)
    {
        if (!await ClientIsAccessibleAsync(clientId, ct)) return null;
        return await (from document in context.ClientDocuments.AsNoTracking()
                      join version in context.ClientDocumentVersions.AsNoTracking()
                          on document.Id equals version.DocumentId
                      where document.ClientId == clientId && document.Id == documentId
                          && version.Id == versionId && document.Status == ClientDocumentStatus.Active
                      select new ClientDocumentContent(version.StorageObjectKey,
                          version.OriginalFileName, version.ContentType, version.SizeBytes))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<bool> HasDuplicateAsync(Guid clientId, string sha256, CancellationToken ct)
    {
        if (!await ClientIsAccessibleAsync(clientId, ct)) return false;
        return await (from version in context.ClientDocumentVersions.AsNoTracking()
                      join document in context.ClientDocuments.AsNoTracking()
                          on version.DocumentId equals document.Id
                      where document.ClientId == clientId && version.Sha256 == sha256
                      select version.Id).AnyAsync(ct);
    }

    public async Task<ClientDocumentMutationResult> CreateAsync(CreateClientDocumentData data, CancellationToken ct)
    {
        var companyId = await (await AccessibleClients(ct)).Where(x => x.Id == data.ClientId)
            .Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(ct);
        if (companyId is null) return new(ClientDocumentMutationStatus.ClientNotFound);
        if (await HasDuplicateAsync(data.ClientId, data.Sha256, ct))
            return new(ClientDocumentMutationStatus.Duplicate);

        var documentId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();
        var document = ClientDocument.Create(documentId, companyId.Value, data.ClientId,
            data.Category, data.Title, data.Description, data.ExpiryDate, data.ReviewDate,
            versionId, data.ActorUserId, data.NowUtc);
        var version = ClientDocumentVersion.Create(versionId, documentId, data.ClientId, 1,
            data.StorageObjectKey, data.OriginalFileName, data.ContentType, data.SizeBytes,
            data.Sha256, data.ActorUserId, data.NowUtc);
        context.ClientDocuments.Add(document);
        context.ClientDocumentVersions.Add(version);
        await context.SaveChangesAsync(ct);
        return new(ClientDocumentMutationStatus.Succeeded, await GetAsync(data.ClientId, documentId, ct));
    }

    public async Task<ClientDocumentMutationResult> UpdateAsync(UpdateClientDocumentData data, CancellationToken ct)
    {
        var document = await FindTracked(data.ClientId, data.DocumentId, ct);
        if (document is null) return new(ClientDocumentMutationStatus.DocumentNotFound);
        if (!VersionMatches(document.RowVersion, data.EditorVersion)) return new(ClientDocumentMutationStatus.Conflict);
        try
        {
            document.UpdateMetadata(data.Category, data.Title, data.Description,
                data.ExpiryDate, data.ReviewDate, data.ActorUserId, data.NowUtc);
            await context.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException) { return new(ClientDocumentMutationStatus.Archived); }
        catch (DbUpdateConcurrencyException) { return new(ClientDocumentMutationStatus.Conflict); }
        return new(ClientDocumentMutationStatus.Succeeded, await GetAsync(data.ClientId, data.DocumentId, ct));
    }

    public async Task<ClientDocumentMutationResult> ReplaceAsync(ReplaceClientDocumentData data, CancellationToken ct)
    {
        var document = await FindTracked(data.ClientId, data.DocumentId, ct);
        if (document is null) return new(ClientDocumentMutationStatus.DocumentNotFound);
        if (!VersionMatches(document.RowVersion, data.EditorVersion)) return new(ClientDocumentMutationStatus.Conflict);
        if (await HasDuplicateAsync(data.ClientId, data.Sha256, ct))
            return new(ClientDocumentMutationStatus.Duplicate);
        var current = await context.ClientDocumentVersions.SingleAsync(
            x => x.DocumentId == data.DocumentId && x.IsCurrent, ct);
        var nextVersion = await context.ClientDocumentVersions.Where(x => x.DocumentId == data.DocumentId)
            .MaxAsync(x => x.VersionNumber, ct) + 1;
        var versionId = Guid.CreateVersion7();
        IDbContextTransaction? localTransaction = null;
        try
        {
            if (context.Database.CurrentTransaction is null)
                localTransaction = await context.Database.BeginTransactionAsync(ct);
            current.MarkReplaced();
            await context.SaveChangesAsync(ct);
            var version = ClientDocumentVersion.Create(versionId, data.DocumentId, data.ClientId, nextVersion,
                data.StorageObjectKey, data.OriginalFileName, data.ContentType, data.SizeBytes,
                data.Sha256, data.ActorUserId, data.NowUtc);
            document.ReplaceCurrentVersion(versionId, data.ActorUserId, data.NowUtc);
            context.ClientDocumentVersions.Add(version);
            await context.SaveChangesAsync(ct);
            if (localTransaction is not null) await localTransaction.CommitAsync(ct);
        }
        catch (InvalidOperationException)
        {
            if (localTransaction is not null) await localTransaction.RollbackAsync(CancellationToken.None);
            return new(ClientDocumentMutationStatus.Archived);
        }
        catch (DbUpdateException)
        {
            if (localTransaction is not null) await localTransaction.RollbackAsync(CancellationToken.None);
            return new(ClientDocumentMutationStatus.Conflict);
        }
        finally { if (localTransaction is not null) await localTransaction.DisposeAsync(); }
        return new(ClientDocumentMutationStatus.Succeeded, await GetAsync(data.ClientId, data.DocumentId, ct));
    }

    public async Task<ClientDocumentMutationResult> ArchiveAsync(ArchiveClientDocumentData data, CancellationToken ct)
    {
        var document = await FindTracked(data.ClientId, data.DocumentId, ct);
        if (document is null) return new(ClientDocumentMutationStatus.DocumentNotFound);
        if (!VersionMatches(document.RowVersion, data.EditorVersion)) return new(ClientDocumentMutationStatus.Conflict);
        try
        {
            document.Archive(data.ActorUserId, data.NowUtc);
            await context.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException) { return new(ClientDocumentMutationStatus.Archived); }
        catch (DbUpdateConcurrencyException) { return new(ClientDocumentMutationStatus.Conflict); }
        return new(ClientDocumentMutationStatus.Succeeded, await GetAsync(data.ClientId, data.DocumentId, ct));
    }

    private async Task<ClientDocument?> FindTracked(Guid clientId, Guid documentId, CancellationToken ct)
    {
        if (!await ClientIsAccessibleAsync(clientId, ct)) return null;
        return await context.ClientDocuments.SingleOrDefaultAsync(
            x => x.Id == documentId && x.ClientId == clientId, ct);
    }

    private async Task<IQueryable<Client>> AccessibleClients(CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return context.Clients.Where(_ => false);
        return context.Clients.Where(x => tenant.IsPlatformAdministrator
            || (x.CompanyId == tenant.CompanyId && context.UserLocations.Any(
                access => access.UserId == tenant.UserId && access.LocationId == x.LocationId)));
    }

    private static ClientDocumentItem Map(ClientDocument document, ClientDocumentVersion version) => new(
        document.Id, document.ClientId, document.Category, document.Title,
        string.IsNullOrEmpty(document.Description) ? null : document.Description,
        document.ExpiryDate, document.ReviewDate, document.Status.ToString(),
        document.CurrentVersionId, version.VersionNumber, version.OriginalFileName,
        version.ContentType, version.SizeBytes, version.UploadedAtUtc, document.UpdatedAtUtc,
        CreateVersion(document.RowVersion));

    private static string CreateVersion(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    private static bool VersionMatches(byte[] value, string supplied)
    {
        var expected = Encoding.ASCII.GetBytes(CreateVersion(value));
        var actual = Encoding.ASCII.GetBytes(supplied.ToUpperInvariant());
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

}
