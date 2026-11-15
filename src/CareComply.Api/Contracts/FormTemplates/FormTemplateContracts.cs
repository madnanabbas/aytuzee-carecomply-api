namespace CareComply.Api.Contracts.FormTemplates;

public sealed class UploadFormTemplateRequest
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = string.Empty;
    public DateOnly? EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public IFormFile? File { get; init; }
}

public sealed class UploadFormTemplateVersionRequest
{
    public DateOnly? EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public IFormFile? File { get; init; }
}

public sealed record FormTemplateResponse(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string Category,
    string OwnerType,
    Guid? CompanyId,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<FormTemplateVersionResponse> Versions);

public sealed record FormTemplateSummaryResponse(
    Guid Id,
    string Key,
    string Name,
    string Category,
    string OwnerType,
    Guid? CompanyId,
    bool IsActive,
    int VersionCount,
    int? LatestVersionNumber,
    string? LatestVersionStatus,
    DateTimeOffset? LatestVersionUploadedAtUtc);

public sealed record FormTemplateVersionResponse(
    Guid Id,
    Guid TemplateId,
    int VersionNumber,
    string Status,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    int PageCount,
    string Technology,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    DateTimeOffset UploadedAtUtc,
    int FieldCount);

public sealed record FormTemplateFieldResponse(
    Guid Id,
    string PdfFieldName,
    string PdfFieldType,
    string FieldPurpose,
    string? BindingKey,
    string? FormatKey,
    bool IsUserEditable);

public sealed record FormTemplateMutationResponse(
    FormTemplateResponse? Template,
    FormTemplateVersionResponse? Version,
    IReadOnlyList<FormTemplateWarningResponse> Warnings);

public sealed record FormTemplateWarningResponse(string Code, string Message);

public sealed record FormTemplateBindingCatalogResponse(IReadOnlyList<FormTemplateBindingGroupResponse> Groups);
public sealed record FormTemplateBindingGroupResponse(string Label, IReadOnlyList<FormTemplateBindingItemResponse> Items);
public sealed record FormTemplateBindingItemResponse(string Key, string Label, IReadOnlyList<string> AllowedPdfFieldTypes);

public sealed record FormTemplateMappingSetResponse(
    Guid TemplateId, string TemplateKey, Guid VersionId, int VersionNumber, string Status,
    bool RequiresWarningAcknowledgement, bool IsReviewed, string RowVersion,
    IReadOnlyList<FormTemplateMappingResponse> Mappings);

public sealed record FormTemplateMappingResponse(
    Guid Id, string PdfFieldName, string PdfFieldType, string FieldPurpose,
    string? BindingKey, string? SuggestedPurpose);

public sealed class ReplaceFormTemplateMappingsRequest
{
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<ReplaceFormTemplateMappingItemRequest> Mappings { get; init; } = [];
}

public sealed record ReplaceFormTemplateMappingItemRequest(Guid Id, string FieldPurpose, string? BindingKey);
public sealed record PublishFormTemplateVersionRequest(string RowVersion, bool AcknowledgeValidationWarnings);
public sealed record FormTemplatePreviewResponse(bool IsSynthetic, IReadOnlyList<FormTemplatePreviewValueResponse> Values);
public sealed record FormTemplatePreviewValueResponse(Guid MappingId, string Value);

