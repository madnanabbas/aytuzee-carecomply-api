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
    string? LatestVersionStatus);

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

