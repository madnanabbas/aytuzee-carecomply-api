using CareComply.Domain.Forms;

namespace CareComply.Application.Abstractions.Forms;

public interface IFormTemplateAdministration
{
    Task<FormTemplateDuplicate?> FindDuplicateAsync(string sha256, CancellationToken cancellationToken);

    Task<FormTemplateWriteResult> CreateTemplateAsync(
        FormTemplate formTemplate,
        FormTemplateVersion version,
        IReadOnlyList<FormTemplateFieldMapping> fields,
        CancellationToken cancellationToken);

    Task<FormTemplateWriteResult> CreateVersionAsync(
        Guid templateId,
        Func<int, FormTemplateVersion> versionFactory,
        Func<Guid, IReadOnlyList<FormTemplateFieldMapping>> fieldFactory,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FormTemplateSummary>> ListAsync(CancellationToken cancellationToken);

    Task<FormTemplateDetails?> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken);

    Task<FormTemplateVersionDetails?> GetVersionAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FormTemplateFieldDetails>?> GetFieldsAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken);

    Task<FormTemplateRetireResult> RetireAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken);

    Task<FormTemplateMappingSet?> GetMappingSetAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken);

    Task<FormTemplateMappingWriteResult> ReplaceMappingsAsync(
        Guid templateId,
        Guid versionId,
        byte[] expectedRowVersion,
        IReadOnlyList<FormTemplateMappingUpdate> mappings,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task<FormTemplateMappingWriteResult> PublishAsync(
        Guid templateId,
        Guid versionId,
        byte[] expectedRowVersion,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

public sealed record FormTemplateDuplicate(Guid TemplateId, string TemplateKey, int VersionNumber);

public enum FormTemplateWriteStatus
{
    Succeeded = 1,
    TemplateNotFound,
    DuplicateKey,
    Conflict,
}

public sealed record FormTemplateWriteResult(
    FormTemplateWriteStatus Status,
    FormTemplateDetails? Template,
    FormTemplateVersionDetails? Version);

public sealed record FormTemplateSummary(
    Guid Id,
    string Key,
    string Name,
    string Category,
    FormTemplateOwnerType OwnerType,
    Guid? CompanyId,
    bool IsActive,
    int VersionCount,
    int? LatestVersionNumber,
    FormTemplateVersionStatus? LatestVersionStatus,
    DateTimeOffset? LatestVersionUploadedAtUtc);

public sealed record FormTemplateDetails(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string Category,
    FormTemplateOwnerType OwnerType,
    Guid? CompanyId,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<FormTemplateVersionDetails> Versions);

public sealed record FormTemplateVersionDetails(
    Guid Id,
    Guid TemplateId,
    int VersionNumber,
    FormTemplateVersionStatus Status,
    string OriginalFileName,
    string StorageObjectKey,
    string ContentType,
    long SizeBytes,
    string Sha256,
    int PageCount,
    FormTechnology Technology,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    DateTimeOffset UploadedAtUtc,
    int FieldCount,
    bool RequiresWarningAcknowledgement,
    Guid? MappingsReviewedByUserId,
    DateTimeOffset? MappingsReviewedAtUtc,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAtUtc,
    byte[] RowVersion);

public sealed record FormTemplateFieldDetails(
    Guid Id,
    string PdfFieldName,
    FormFieldType PdfFieldType,
    FormFieldPurpose FieldPurpose,
    string? BindingKey,
    string? FormatKey,
    bool IsUserEditable);

public enum FormTemplateRetireStatus
{
    Succeeded = 1,
    NotFound,
    InvalidState,
    Conflict,
}

public sealed record FormTemplateRetireResult(
    FormTemplateRetireStatus Status,
    Guid TemplateId,
    Guid VersionId,
    int? VersionNumber);

public sealed record FormTemplateMappingSet(
    Guid TemplateId,
    string TemplateKey,
    Guid VersionId,
    int VersionNumber,
    FormTemplateVersionStatus Status,
    bool RequiresWarningAcknowledgement,
    Guid? MappingsReviewedByUserId,
    DateTimeOffset? MappingsReviewedAtUtc,
    byte[] RowVersion,
    IReadOnlyList<FormTemplateFieldDetails> Mappings);

public sealed record FormTemplateMappingUpdate(
    Guid Id,
    FormFieldPurpose FieldPurpose,
    string? BindingKey);

public enum FormTemplateMappingWriteStatus
{
    Succeeded = 1,
    NotFound,
    InvalidState,
    IncompleteSet,
    Conflict,
}

public sealed record FormTemplateMappingWriteResult(
    FormTemplateMappingWriteStatus Status,
    FormTemplateMappingSet? MappingSet);
