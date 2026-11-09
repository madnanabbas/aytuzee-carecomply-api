namespace CareComply.Api.Contracts.ClientForms;

public sealed record ClientFormTemplateListResponse(
    IReadOnlyList<ClientFormTemplateListItemResponse> Templates);

public sealed record ClientFormTemplateListItemResponse(
    Guid TemplateVersionId,
    string TemplateKey,
    string Name,
    string Description,
    string Category,
    int VersionNumber,
    int PageCount,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record ClientFormBindingsResponse(
    Guid TemplateVersionId,
    IReadOnlyList<ClientFormBindingFieldResponse> Fields);

public sealed record ClientFormBindingFieldResponse(
    string PdfFieldName,
    string Value,
    bool UserEditable);
