namespace CareComply.Application.Abstractions.Forms;

public sealed record ClientFormTemplateSummary(
    Guid TemplateVersionId,
    string TemplateKey,
    string Name,
    string Description,
    string Category,
    int VersionNumber,
    int PageCount,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record ClientFormContentDescriptor(
    Guid TemplateVersionId,
    string TemplateKey,
    int VersionNumber,
    string StorageObjectKey,
    string ContentType);

public sealed record ClientFormBindingField(
    string PdfFieldName,
    string Value,
    bool UserEditable);

public sealed record ClientFormBindings(
    Guid TemplateVersionId,
    string TemplateKey,
    int VersionNumber,
    IReadOnlyList<ClientFormBindingField> Fields);

public interface IClientFormDirectory
{
    Task<IReadOnlyList<ClientFormTemplateSummary>?> ListAvailableAsync(
        Guid clientId,
        CancellationToken cancellationToken);

    Task<ClientFormContentDescriptor?> GetContentDescriptorAsync(
        Guid clientId,
        Guid templateVersionId,
        CancellationToken cancellationToken);

    Task<ClientFormBindings?> GetBindingsAsync(
        Guid clientId,
        Guid templateVersionId,
        CancellationToken cancellationToken);
}
