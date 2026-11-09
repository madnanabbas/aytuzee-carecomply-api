using CareComply.Domain.Forms;

namespace CareComply.Application.Abstractions.Forms;

public interface IFormTemplatePdfValidator
{
    Task<FormTemplateFileValidationResult> ValidateAsync(
        Stream source,
        CancellationToken cancellationToken);
}

public interface IValidatedFormTemplateFile : IAsyncDisposable
{
    long SizeBytes { get; }
    string Sha256 { get; }
    int PageCount { get; }
    FormTechnology Technology { get; }
    IReadOnlyList<DiscoveredFormField> Fields { get; }
    IReadOnlyList<FormTemplateValidationWarning> Warnings { get; }
    Stream OpenRead();
}

public sealed record DiscoveredFormField(string Name, FormFieldType Type);

public sealed record FormTemplateValidationWarning(string Code, string Message);

public sealed record FormTemplateFileValidationResult(
    IValidatedFormTemplateFile? File,
    FormTemplateFileValidationError Error)
{
    public static FormTemplateFileValidationResult Valid(IValidatedFormTemplateFile file) =>
        new(file, FormTemplateFileValidationError.None);

    public static FormTemplateFileValidationResult Invalid(FormTemplateFileValidationError error) =>
        new(null, error);
}

public enum FormTemplateFileValidationError
{
    None = 0,
    TooLarge,
    NotPdf,
    MalformedPdf,
    Encrypted,
    TooManyPages,
    TooManyFields,
    NoAcroFormFields,
    PureXfa,
    DuplicateFieldNames,
    DangerousContent,
    TraversalLimitExceeded,
}
