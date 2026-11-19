using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.ClientDocuments;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using FluentValidation;
using Mediator;
using static CareComply.Application.Features.ClientDocuments.ClientDocumentOperationHelpers;

namespace CareComply.Application.Features.ClientDocuments;

public sealed record ListClientDocumentsQuery(Guid ClientId)
    : IQuery<ClientDocumentResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.ClientDocument.View;
}

public sealed record GetClientDocumentQuery(Guid ClientId, Guid DocumentId)
    : IQuery<ClientDocumentResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.ClientDocument.View;
}

public sealed record DownloadClientDocumentQuery(Guid ClientId, Guid DocumentId, Guid VersionId)
    : IQuery<ClientDocumentResult>, IRequirePermission, IAuditableRequest<ClientDocumentResult>
{
    public string RequiredPermission => Permissions.ClientDocument.Download;
    public AuditEvent CreateAuditEvent(ClientDocumentResult response) => Audit(
        "ClientDocument.Downloaded", ClientId, DocumentId, response);
}

public sealed class UploadClientDocumentCommand(
    Guid clientId, string category, string title, string? description,
    DateOnly? expiryDate, DateOnly? reviewDate, string originalFileName, Stream content)
    : ICommand<ClientDocumentResult>, IRequirePermission, IAuditableRequest<ClientDocumentResult>
{
    public Guid ClientId { get; } = clientId;
    public string Category { get; } = category;
    public string Title { get; } = title;
    public string? Description { get; } = description;
    public DateOnly? ExpiryDate { get; } = expiryDate;
    public DateOnly? ReviewDate { get; } = reviewDate;
    public string OriginalFileName { get; } = originalFileName;
    public Stream Content { get; } = content;
    public string RequiredPermission => Permissions.ClientDocument.Upload;
    public AuditEvent CreateAuditEvent(ClientDocumentResult response) => Audit(
        "ClientDocument.Uploaded", ClientId, response.Mutation?.Details?.Document.Id ?? ClientId, response);
    public override string ToString() => "UploadClientDocumentCommand { Content = [OMITTED] }";
}

public sealed record UpdateClientDocumentCommand(
    Guid ClientId, Guid DocumentId, string Category, string Title, string? Description,
    DateOnly? ExpiryDate, DateOnly? ReviewDate, string EditorVersion)
    : ICommand<ClientDocumentResult>, IRequirePermission, IAuditableRequest<ClientDocumentResult>
{
    public string RequiredPermission => Permissions.ClientDocument.Upload;
    public AuditEvent CreateAuditEvent(ClientDocumentResult response) => Audit(
        "ClientDocument.MetadataUpdated", ClientId, DocumentId, response);
}

public sealed class ReplaceClientDocumentCommand(
    Guid clientId, Guid documentId, string editorVersion, string originalFileName, Stream content)
    : ICommand<ClientDocumentResult>, IRequirePermission, IAuditableRequest<ClientDocumentResult>
{
    public Guid ClientId { get; } = clientId;
    public Guid DocumentId { get; } = documentId;
    public string EditorVersion { get; } = editorVersion;
    public string OriginalFileName { get; } = originalFileName;
    public Stream Content { get; } = content;
    public string RequiredPermission => Permissions.ClientDocument.Replace;
    public AuditEvent CreateAuditEvent(ClientDocumentResult response) => Audit(
        "ClientDocument.Replaced", ClientId, DocumentId, response);
    public override string ToString() => "ReplaceClientDocumentCommand { Content = [OMITTED] }";
}

public sealed record ArchiveClientDocumentCommand(Guid ClientId, Guid DocumentId, string EditorVersion)
    : ICommand<ClientDocumentResult>, IRequirePermission, IAuditableRequest<ClientDocumentResult>
{
    public string RequiredPermission => Permissions.ClientDocument.Archive;
    public AuditEvent CreateAuditEvent(ClientDocumentResult response) => Audit(
        "ClientDocument.Archived", ClientId, DocumentId, response);
}

public sealed record ClientDocumentResult(
    IReadOnlyList<ClientDocumentItem>? Items,
    ClientDocumentDetails? Details,
    ClientDocumentMutationResult? Mutation,
    ClientDocumentDownload? Download,
    ClientDocumentFileValidationError? ValidationError,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ClientDocumentResult>
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Mutation?.Status.ToString() ?? ValidationError?.ToString() ?? "Unavailable";
    public static ClientDocumentResult Listed(IReadOnlyList<ClientDocumentItem>? items) => new(items, null, null, null, null, null);
    public static ClientDocumentResult Found(ClientDocumentDetails? value) => new(null, value, null, null, null, null);
    public static ClientDocumentResult Changed(ClientDocumentMutationResult value) => new(null, null, value, null, null, null);
    public static ClientDocumentResult File(ClientDocumentDownload? value) => new(null, null, null, value, null, null);
    public static ClientDocumentResult Invalid(ClientDocumentFileValidationError error) => new(null, null, null, null, error, null);
    public static ClientDocumentResult Denied(AuthorizationFailure failure) => new(null, null, null, null, null, failure);
}

public sealed record ClientDocumentDownload(Stream Content, string FileName, string ContentType, long SizeBytes);

public sealed class ListClientDocumentsHandler(IClientDocumentAdministration administration)
    : IQueryHandler<ListClientDocumentsQuery, ClientDocumentResult>
{
    public async ValueTask<ClientDocumentResult> Handle(ListClientDocumentsQuery query, CancellationToken cancellationToken) =>
        ClientDocumentResult.Listed(await administration.ListAsync(query.ClientId, cancellationToken));
}

public sealed class GetClientDocumentHandler(IClientDocumentAdministration administration)
    : IQueryHandler<GetClientDocumentQuery, ClientDocumentResult>
{
    public async ValueTask<ClientDocumentResult> Handle(GetClientDocumentQuery query, CancellationToken cancellationToken) =>
        ClientDocumentResult.Found(await administration.GetAsync(query.ClientId, query.DocumentId, cancellationToken));
}

public sealed class DownloadClientDocumentHandler(
    IClientDocumentAdministration administration, IClientDocumentContentStore contentStore)
    : IQueryHandler<DownloadClientDocumentQuery, ClientDocumentResult>
{
    public async ValueTask<ClientDocumentResult> Handle(DownloadClientDocumentQuery query, CancellationToken cancellationToken)
    {
        var descriptor = await administration.GetContentAsync(query.ClientId, query.DocumentId, query.VersionId, cancellationToken);
        if (descriptor is null) return ClientDocumentResult.File(null);
        var content = await contentStore.OpenReadAsync(descriptor.StorageObjectKey, cancellationToken);
        return ClientDocumentResult.File(new(content, descriptor.OriginalFileName, descriptor.ContentType, descriptor.SizeBytes));
    }
}

public sealed class UploadClientDocumentHandler(
    IClientDocumentAdministration administration, IClientDocumentFileValidator validator,
    IClientDocumentContentStore contentStore, ICurrentActor actor, TimeProvider timeProvider)
    : ICommandHandler<UploadClientDocumentCommand, ClientDocumentResult>
{
    public async ValueTask<ClientDocumentResult> Handle(UploadClientDocumentCommand command, CancellationToken cancellationToken)
    {
        if (!await administration.ClientIsAccessibleAsync(command.ClientId, cancellationToken))
            return ClientDocumentResult.Changed(new(ClientDocumentMutationStatus.ClientNotFound));
        var validation = await validator.ValidateAsync(command.Content, cancellationToken);
        if (validation.File is not { } file) return ClientDocumentResult.Invalid(validation.Error!.Value);
        await using (file)
        {
            if (await administration.HasDuplicateAsync(command.ClientId, file.Sha256, cancellationToken))
                return ClientDocumentResult.Changed(new(ClientDocumentMutationStatus.Duplicate));
            var key = Guid.CreateVersion7().ToString("N");
            await using (var source = file.OpenRead()) await contentStore.WriteAsync(key, source, cancellationToken);
            try
            {
                var result = await administration.CreateAsync(new(
                    command.ClientId, command.Category, command.Title, command.Description,
                    command.ExpiryDate, command.ReviewDate, key,
                    SafeFileName(command.OriginalFileName, file.Extension), file.ContentType,
                    file.SizeBytes, file.Sha256, RequiredActor(actor), timeProvider.GetUtcNow()), cancellationToken);
                if (result.Status != ClientDocumentMutationStatus.Succeeded)
                    await contentStore.DeleteUncommittedAsync(key, CancellationToken.None);
                return ClientDocumentResult.Changed(result);
            }
            catch
            {
                await contentStore.DeleteUncommittedAsync(key, CancellationToken.None);
                throw;
            }
        }
    }
}

public sealed class UpdateClientDocumentHandler(IClientDocumentAdministration administration, ICurrentActor actor, TimeProvider timeProvider)
    : ICommandHandler<UpdateClientDocumentCommand, ClientDocumentResult>
{
    public async ValueTask<ClientDocumentResult> Handle(UpdateClientDocumentCommand command, CancellationToken cancellationToken) =>
        ClientDocumentResult.Changed(await administration.UpdateAsync(new(
            command.ClientId, command.DocumentId, command.Category, command.Title, command.Description,
            command.ExpiryDate, command.ReviewDate, command.EditorVersion,
            RequiredActor(actor), timeProvider.GetUtcNow()), cancellationToken));
}

public sealed class ReplaceClientDocumentHandler(
    IClientDocumentAdministration administration, IClientDocumentFileValidator validator,
    IClientDocumentContentStore contentStore, ICurrentActor actor, TimeProvider timeProvider)
    : ICommandHandler<ReplaceClientDocumentCommand, ClientDocumentResult>
{
    public async ValueTask<ClientDocumentResult> Handle(ReplaceClientDocumentCommand command, CancellationToken cancellationToken)
    {
        var current = await administration.GetAsync(command.ClientId, command.DocumentId, cancellationToken);
        if (current is null) return ClientDocumentResult.Changed(new(ClientDocumentMutationStatus.DocumentNotFound));
        var validation = await validator.ValidateAsync(command.Content, cancellationToken);
        if (validation.File is not { } file) return ClientDocumentResult.Invalid(validation.Error!.Value);
        await using (file)
        {
            if (await administration.HasDuplicateAsync(command.ClientId, file.Sha256, cancellationToken))
                return ClientDocumentResult.Changed(new(ClientDocumentMutationStatus.Duplicate));
            var key = Guid.CreateVersion7().ToString("N");
            await using (var source = file.OpenRead()) await contentStore.WriteAsync(key, source, cancellationToken);
            try
            {
                var result = await administration.ReplaceAsync(new(
                    command.ClientId, command.DocumentId, command.EditorVersion, key,
                    SafeFileName(command.OriginalFileName, file.Extension), file.ContentType,
                    file.SizeBytes, file.Sha256, RequiredActor(actor), timeProvider.GetUtcNow()), cancellationToken);
                if (result.Status != ClientDocumentMutationStatus.Succeeded)
                    await contentStore.DeleteUncommittedAsync(key, CancellationToken.None);
                return ClientDocumentResult.Changed(result);
            }
            catch
            {
                await contentStore.DeleteUncommittedAsync(key, CancellationToken.None);
                throw;
            }
        }
    }
}

public sealed class ArchiveClientDocumentHandler(IClientDocumentAdministration administration, ICurrentActor actor, TimeProvider timeProvider)
    : ICommandHandler<ArchiveClientDocumentCommand, ClientDocumentResult>
{
    public async ValueTask<ClientDocumentResult> Handle(ArchiveClientDocumentCommand command, CancellationToken cancellationToken) =>
        ClientDocumentResult.Changed(await administration.ArchiveAsync(new(
            command.ClientId, command.DocumentId, command.EditorVersion,
            RequiredActor(actor), timeProvider.GetUtcNow()), cancellationToken));
}

public sealed class UploadClientDocumentValidator : AbstractValidator<UploadClientDocumentCommand>
{
    public UploadClientDocumentValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Category).NotEmpty().MaximumLength(ClientDocument.MaximumCategoryLength);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(ClientDocument.MaximumTitleLength);
        RuleFor(x => x.Description).MaximumLength(ClientDocument.MaximumDescriptionLength);
        RuleFor(x => x.OriginalFileName).NotEmpty().MaximumLength(ClientDocumentVersion.MaximumOriginalFileNameLength);
        RuleFor(x => x.Content).NotNull().Must(x => x.CanRead);
    }
}

public sealed class UpdateClientDocumentValidator : AbstractValidator<UpdateClientDocumentCommand>
{
    public UpdateClientDocumentValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty(); RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Category).NotEmpty().MaximumLength(ClientDocument.MaximumCategoryLength);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(ClientDocument.MaximumTitleLength);
        RuleFor(x => x.Description).MaximumLength(ClientDocument.MaximumDescriptionLength);
        RuleFor(x => x.EditorVersion).NotEmpty().Length(64);
    }
}

public sealed class ReplaceClientDocumentValidator : AbstractValidator<ReplaceClientDocumentCommand>
{
    public ReplaceClientDocumentValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty(); RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.EditorVersion).NotEmpty().Length(64);
        RuleFor(x => x.OriginalFileName).NotEmpty().MaximumLength(ClientDocumentVersion.MaximumOriginalFileNameLength);
        RuleFor(x => x.Content).NotNull().Must(x => x.CanRead);
    }
}

public sealed class ArchiveClientDocumentValidator : AbstractValidator<ArchiveClientDocumentCommand>
{
    public ArchiveClientDocumentValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty(); RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.EditorVersion).NotEmpty().Length(64);
    }
}

file static class ClientDocumentOperationHelpers
{
    public static Guid RequiredActor(ICurrentActor actor) => actor.UserId
        ?? throw new InvalidOperationException("An authenticated actor is required.");

    public static string SafeFileName(string supplied, string extension)
    {
        var value = supplied.Replace('\\', '/');
        value = value[(value.LastIndexOf('/') + 1)..].Trim();
        value = new string(value.Where(c => !char.IsControl(c)).ToArray());
        var stem = Path.GetFileNameWithoutExtension(value);
        if (string.IsNullOrWhiteSpace(stem)) stem = "document";
        var maximumStem = ClientDocumentVersion.MaximumOriginalFileNameLength - extension.Length;
        if (stem.Length > maximumStem) stem = stem[..maximumStem];
        return stem + extension;
    }

    public static AuditEvent Audit(string action, Guid clientId, Guid documentId, ClientDocumentResult response)
    {
        var succeeded = response.Mutation?.Status == ClientDocumentMutationStatus.Succeeded
            || response.Download is not null;
        return new(action, "ClientDocument", documentId.ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(("clientId", clientId.ToString("N")))
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }
}

