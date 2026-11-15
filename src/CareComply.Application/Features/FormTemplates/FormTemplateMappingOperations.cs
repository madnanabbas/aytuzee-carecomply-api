using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientForms;
using CareComply.Domain.Forms;
using FluentValidation;
using Mediator;
using System.Globalization;

namespace CareComply.Application.Features.FormTemplates;

public enum FormTemplateMappingOperationStatus
{
    Succeeded = 1,
    NotFound,
    InvalidState,
    InvalidMappings,
    ReviewRequired,
    WarningAcknowledgementRequired,
    Conflict,
}

public sealed record FormTemplateMappingOperationResult(
    FormTemplateMappingOperationStatus Status,
    FormTemplateMappingSet? MappingSet,
    IReadOnlyList<string> ErrorCodes,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<FormTemplateMappingOperationResult>
{
    public static FormTemplateMappingOperationResult Denied(AuthorizationFailure failure) =>
        new(FormTemplateMappingOperationStatus.Conflict, null, [], failure);
}

public sealed record FormTemplateBindingCatalogResult(
    IReadOnlyList<ClientFormBindingDefinition>? Items,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<FormTemplateBindingCatalogResult>
{
    public static FormTemplateBindingCatalogResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed record FormTemplatePreviewValue(Guid MappingId, string Value);

public sealed record FormTemplatePreviewResult(
    IReadOnlyList<FormTemplatePreviewValue>? Values,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<FormTemplatePreviewResult>
{
    public static FormTemplatePreviewResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed record GetFormTemplateMappingsQuery(Guid TemplateId, Guid VersionId)
    : IQuery<FormTemplateMappingOperationResult>, IRequirePermission, IRequirePlatformAdministrator
{
    public string RequiredPermission => Permissions.FormTemplate.Manage;
}

public sealed record GetFormTemplateBindingCatalogQuery
    : IQuery<FormTemplateBindingCatalogResult>, IRequirePermission, IRequirePlatformAdministrator
{
    public string RequiredPermission => Permissions.FormTemplate.Manage;
}

public sealed record PreviewFormTemplateMappingsQuery(Guid TemplateId, Guid VersionId)
    : IQuery<FormTemplatePreviewResult>, IRequirePermission, IRequirePlatformAdministrator
{
    public string RequiredPermission => Permissions.FormTemplate.Manage;
}

public sealed record ReplaceFormTemplateMappingsCommand(
    Guid TemplateId,
    Guid VersionId,
    byte[] RowVersion,
    IReadOnlyList<FormTemplateMappingUpdate> Mappings)
    : ICommand<FormTemplateMappingOperationResult>, IRequirePermission, IRequirePlatformAdministrator,
        IAuditableRequest<FormTemplateMappingOperationResult>
{
    public string RequiredPermission => Permissions.FormTemplate.Manage;

    public AuditEvent CreateAuditEvent(FormTemplateMappingOperationResult response) => new(
        "FormTemplate.MappingsSaved", "FormTemplate", TemplateId.ToString("N"),
        response.Status == FormTemplateMappingOperationStatus.Succeeded
            ? AuditOutcome.Succeeded : AuditOutcome.Failed,
        AuditMetadata.Create(
            ("versionNumber", response.MappingSet?.VersionNumber.ToString(CultureInfo.InvariantCulture) ?? "unavailable"),
            ("templateKey", response.MappingSet?.TemplateKey ?? "unavailable"),
            ("mappingCount", response.MappingSet?.Mappings.Count.ToString(CultureInfo.InvariantCulture) ?? "0")));
}

public sealed record PublishFormTemplateVersionCommand(
    Guid TemplateId,
    Guid VersionId,
    byte[] RowVersion,
    bool AcknowledgeValidationWarnings)
    : ICommand<FormTemplateMappingOperationResult>, IRequirePermission, IRequirePlatformAdministrator,
        IAuditableRequest<FormTemplateMappingOperationResult>
{
    public string RequiredPermission => Permissions.FormTemplate.Manage;

    public AuditEvent CreateAuditEvent(FormTemplateMappingOperationResult response) => new(
        "FormTemplate.Published", "FormTemplate", TemplateId.ToString("N"),
        response.Status == FormTemplateMappingOperationStatus.Succeeded
            ? AuditOutcome.Succeeded : AuditOutcome.Failed,
        AuditMetadata.Create(
            ("mappingCount", response.MappingSet?.Mappings.Count.ToString(CultureInfo.InvariantCulture) ?? "0"),
            ("versionNumber", response.MappingSet?.VersionNumber.ToString(CultureInfo.InvariantCulture) ?? "unavailable"),
            ("templateKey", response.MappingSet?.TemplateKey ?? "unavailable")));
}

public sealed class FormTemplateMappingQueryValidator : AbstractValidator<GetFormTemplateMappingsQuery>
{
    public FormTemplateMappingQueryValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.VersionId).NotEmpty();
    }
}

public sealed class PreviewFormTemplateMappingsQueryValidator : AbstractValidator<PreviewFormTemplateMappingsQuery>
{
    public PreviewFormTemplateMappingsQueryValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.VersionId).NotEmpty();
    }
}

public sealed class ReplaceFormTemplateMappingsCommandValidator : AbstractValidator<ReplaceFormTemplateMappingsCommand>
{
    public ReplaceFormTemplateMappingsCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.VersionId).NotEmpty();
        RuleFor(x => x.RowVersion).NotEmpty();
        RuleFor(x => x.Mappings).NotNull();
        RuleForEach(x => x.Mappings).ChildRules(mapping =>
        {
            mapping.RuleFor(x => x.Id).NotEmpty();
            mapping.RuleFor(x => x.FieldPurpose).IsInEnum();
            mapping.RuleFor(x => x.BindingKey).MaximumLength(FormTemplateFieldMapping.MaximumBindingKeyLength);
        });
    }
}

public sealed class PublishFormTemplateVersionCommandValidator : AbstractValidator<PublishFormTemplateVersionCommand>
{
    public PublishFormTemplateVersionCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.VersionId).NotEmpty();
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}

public sealed class GetFormTemplateMappingsQueryHandler(IFormTemplateAdministration administration)
    : IQueryHandler<GetFormTemplateMappingsQuery, FormTemplateMappingOperationResult>
{
    public async ValueTask<FormTemplateMappingOperationResult> Handle(
        GetFormTemplateMappingsQuery query, CancellationToken cancellationToken)
    {
        var set = await administration.GetMappingSetAsync(query.TemplateId, query.VersionId, cancellationToken);
        return set is null
            ? new(FormTemplateMappingOperationStatus.NotFound, null, [], null)
            : new(FormTemplateMappingOperationStatus.Succeeded, set, [], null);
    }
}

public sealed class GetFormTemplateBindingCatalogQueryHandler
    : IQueryHandler<GetFormTemplateBindingCatalogQuery, FormTemplateBindingCatalogResult>
{
    public ValueTask<FormTemplateBindingCatalogResult> Handle(
        GetFormTemplateBindingCatalogQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new FormTemplateBindingCatalogResult(ClientFormBindingKeys.Catalog, null));
}

public sealed class PreviewFormTemplateMappingsQueryHandler(IFormTemplateAdministration administration)
    : IQueryHandler<PreviewFormTemplateMappingsQuery, FormTemplatePreviewResult>
{
    public async ValueTask<FormTemplatePreviewResult> Handle(
        PreviewFormTemplateMappingsQuery query, CancellationToken cancellationToken)
    {
        var set = await administration.GetMappingSetAsync(query.TemplateId, query.VersionId, cancellationToken);
        if (set is null) return new(null, null);
        var values = set.Mappings
            .Where(mapping => mapping.FieldPurpose == FormFieldPurpose.Data)
            .Select(mapping => new FormTemplatePreviewValue(
                mapping.Id, ClientFormBindingKeys.Find(mapping.BindingKey)?.SyntheticValue ?? string.Empty))
            .ToArray();
        return new(values, null);
    }
}

public sealed class ReplaceFormTemplateMappingsCommandHandler(
    IFormTemplateAdministration administration,
    ICurrentActor currentActor,
    TimeProvider timeProvider)
    : ICommandHandler<ReplaceFormTemplateMappingsCommand, FormTemplateMappingOperationResult>
{
    public async ValueTask<FormTemplateMappingOperationResult> Handle(
        ReplaceFormTemplateMappingsCommand command, CancellationToken cancellationToken)
    {
        var existing = await administration.GetMappingSetAsync(command.TemplateId, command.VersionId, cancellationToken);
        if (existing is null) return Result(FormTemplateMappingOperationStatus.NotFound);
        if (existing.Status != FormTemplateVersionStatus.Draft) return Result(FormTemplateMappingOperationStatus.InvalidState);
        var errors = FormTemplateMappingPolicy.Validate(existing.Mappings, command.Mappings);
        if (errors.Count > 0) return new(FormTemplateMappingOperationStatus.InvalidMappings, null, errors, null);
        var actor = currentActor.UserId ?? throw new InvalidOperationException("An authenticated actor is required.");
        var write = await administration.ReplaceMappingsAsync(command.TemplateId, command.VersionId,
            command.RowVersion, command.Mappings, actor, timeProvider.GetUtcNow(), cancellationToken);
        return Map(write);
    }

    private static FormTemplateMappingOperationResult Result(FormTemplateMappingOperationStatus status) => new(status, null, [], null);
    internal static FormTemplateMappingOperationResult Map(FormTemplateMappingWriteResult result) => new(
        result.Status switch
        {
            FormTemplateMappingWriteStatus.Succeeded => FormTemplateMappingOperationStatus.Succeeded,
            FormTemplateMappingWriteStatus.NotFound => FormTemplateMappingOperationStatus.NotFound,
            FormTemplateMappingWriteStatus.InvalidState => FormTemplateMappingOperationStatus.InvalidState,
            FormTemplateMappingWriteStatus.IncompleteSet => FormTemplateMappingOperationStatus.InvalidMappings,
            _ => FormTemplateMappingOperationStatus.Conflict,
        }, result.MappingSet, [], null);
}

public sealed class PublishFormTemplateVersionCommandHandler(
    IFormTemplateAdministration administration,
    ICurrentActor currentActor,
    TimeProvider timeProvider)
    : ICommandHandler<PublishFormTemplateVersionCommand, FormTemplateMappingOperationResult>
{
    public async ValueTask<FormTemplateMappingOperationResult> Handle(
        PublishFormTemplateVersionCommand command, CancellationToken cancellationToken)
    {
        var set = await administration.GetMappingSetAsync(command.TemplateId, command.VersionId, cancellationToken);
        if (set is null) return Result(FormTemplateMappingOperationStatus.NotFound);
        if (set.Status != FormTemplateVersionStatus.Draft) return Result(FormTemplateMappingOperationStatus.InvalidState);
        if (set.Mappings.Count == 0 || set.MappingsReviewedAtUtc is null)
            return Result(FormTemplateMappingOperationStatus.ReviewRequired);
        var unsafeMappings = FormTemplateMappingPolicy.FindUnsafePublishedMappings(set.Mappings);
        if (unsafeMappings.Count > 0)
            return new(FormTemplateMappingOperationStatus.InvalidMappings, null, unsafeMappings, null);
        if (set.RequiresWarningAcknowledgement && !command.AcknowledgeValidationWarnings)
            return Result(FormTemplateMappingOperationStatus.WarningAcknowledgementRequired);
        var actor = currentActor.UserId ?? throw new InvalidOperationException("An authenticated actor is required.");
        return ReplaceFormTemplateMappingsCommandHandler.Map(await administration.PublishAsync(
            command.TemplateId, command.VersionId, command.RowVersion, actor,
            timeProvider.GetUtcNow(), cancellationToken));
    }

    private static FormTemplateMappingOperationResult Result(FormTemplateMappingOperationStatus status) => new(status, null, [], null);
}

public static class FormTemplateMappingPolicy
{
    public static IReadOnlyList<string> Validate(
        IReadOnlyList<FormTemplateFieldDetails> fields,
        IReadOnlyList<FormTemplateMappingUpdate> updates)
    {
        if (fields.Count != updates.Count
            || !fields.Select(x => x.Id).ToHashSet().SetEquals(updates.Select(x => x.Id)))
            return ["incomplete-mapping-set"];
        var existing = fields.ToDictionary(x => x.Id);
        var errors = new List<string>();
        foreach (var update in updates)
        {
            var field = existing[update.Id];
            var definition = ClientFormBindingKeys.Find(update.BindingKey);
            if (update.FieldPurpose == FormFieldPurpose.Data)
            {
                if (definition is null) errors.Add("binding-key-not-allowed");
                else if (!definition.AllowedPdfFieldTypes.Contains(field.PdfFieldType))
                    errors.Add("field-type-not-bindable");
            }
            else if (update.BindingKey is not null) errors.Add("binding-not-allowed-for-purpose");
        }
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> FindUnsafePublishedMappings(IReadOnlyList<FormTemplateFieldDetails> fields) =>
        fields.Where(field => field.FieldPurpose == FormFieldPurpose.Data
            && SuggestPurpose(field.PdfFieldName) is FormFieldPurpose.Signature or FormFieldPurpose.Attestation)
            .Select(_ => "sensitive-purpose-confirmation-required")
            .Distinct(StringComparer.Ordinal).ToArray();

    public static FormFieldPurpose? SuggestPurpose(string fieldName)
    {
        var normalized = fieldName.Replace('_', ' ').Replace('-', ' ').ToLowerInvariant();
        if (normalized.Contains("signature", StringComparison.Ordinal)
            || normalized.Contains("signed by", StringComparison.Ordinal)
            || normalized.Contains("signer", StringComparison.Ordinal)) return FormFieldPurpose.Signature;
        if (normalized.Contains("acknowledg", StringComparison.Ordinal)
            || normalized.Contains("attest", StringComparison.Ordinal)
            || normalized.Contains("assurance", StringComparison.Ordinal)) return FormFieldPurpose.Attestation;
        return null;
    }
}
