using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.FormTemplates;

public enum FormTemplateOperationStatus
{
    Succeeded = 1,
    NotFound,
    DuplicateKey,
    DuplicateContent,
    InvalidFile,
    InvalidState,
    Conflict,
}

public sealed record FormTemplateOperationResult(
    FormTemplateOperationStatus Status,
    FormTemplateDetails? Template,
    FormTemplateVersionDetails? Version,
    IReadOnlyList<FormTemplateValidationWarning> Warnings,
    FormTemplateFileValidationError ValidationError,
    FormTemplateDuplicate? Duplicate,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<FormTemplateOperationResult>
{
    public static FormTemplateOperationResult Denied(AuthorizationFailure failure) =>
        new(FormTemplateOperationStatus.Conflict, null, null, [], FormTemplateFileValidationError.None, null, failure);

    public static FormTemplateOperationResult Invalid(FormTemplateFileValidationError error) =>
        new(FormTemplateOperationStatus.InvalidFile, null, null, [], error, null, null);

    public static FormTemplateOperationResult DuplicateContent(FormTemplateDuplicate duplicate) =>
        new(FormTemplateOperationStatus.DuplicateContent, null, null, [], FormTemplateFileValidationError.None, duplicate, null);

    public static FormTemplateOperationResult FromWrite(
        FormTemplateWriteResult result,
        IReadOnlyList<FormTemplateValidationWarning> warnings) =>
        new(
            result.Status switch
            {
                FormTemplateWriteStatus.Succeeded => FormTemplateOperationStatus.Succeeded,
                FormTemplateWriteStatus.TemplateNotFound => FormTemplateOperationStatus.NotFound,
                FormTemplateWriteStatus.DuplicateKey => FormTemplateOperationStatus.DuplicateKey,
                _ => FormTemplateOperationStatus.Conflict,
            },
            result.Template,
            result.Version,
            warnings,
            FormTemplateFileValidationError.None,
            null,
            null);
}

public sealed record FormTemplateListResult(
    IReadOnlyList<FormTemplateSummary>? Templates,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<FormTemplateListResult>
{
    public static FormTemplateListResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed record FormTemplateDetailsResult(
    FormTemplateDetails? Template,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<FormTemplateDetailsResult>
{
    public static FormTemplateDetailsResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed record FormTemplateVersionResult(
    FormTemplateVersionDetails? Version,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<FormTemplateVersionResult>
{
    public static FormTemplateVersionResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed record FormTemplateFieldsResult(
    IReadOnlyList<FormTemplateFieldDetails>? Fields,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<FormTemplateFieldsResult>
{
    public static FormTemplateFieldsResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed record FormTemplateContentResult(
    Stream? Content,
    string? FileName,
    string? ContentType,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<FormTemplateContentResult>
{
    public static FormTemplateContentResult Denied(AuthorizationFailure failure) => new(null, null, null, failure);
}
