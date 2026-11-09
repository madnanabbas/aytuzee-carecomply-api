using CareComply.Api.Authorization;
using CareComply.Api.Authentication;
using CareComply.Api.Contracts.FormTemplates;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Authorization;
using CareComply.Application.Features.FormTemplates;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.RateLimiting;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/form-templates")]
public sealed class FormTemplatesController(ISender sender) : ControllerBase
{
    private const long MaximumMultipartRequestBytes = 11 * 1024 * 1024;

    [HttpGet]
    [HasPermission(Permissions.FormTemplate.View)]
    public async Task<ActionResult<IReadOnlyList<FormTemplateSummaryResponse>>> List(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListFormTemplatesQuery(), cancellationToken);
        if (result.AuthorizationFailure is { } failure) return AuthorizationFailureResult(failure);
        return Ok(result.Templates!.Select(MapSummary).ToArray());
    }

    [HttpGet("{templateId:guid}")]
    [HasPermission(Permissions.FormTemplate.View)]
    public async Task<ActionResult<FormTemplateResponse>> Get(
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetFormTemplateQuery(templateId), cancellationToken);
        if (result.AuthorizationFailure is { } failure) return AuthorizationFailureResult(failure);
        return result.Template is null ? NotFoundProblem() : Ok(MapTemplate(result.Template));
    }

    [HttpGet("{templateId:guid}/versions/{versionId:guid}")]
    [HasPermission(Permissions.FormTemplate.View)]
    public async Task<ActionResult<FormTemplateVersionResponse>> GetVersion(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetFormTemplateVersionQuery(templateId, versionId), cancellationToken);
        if (result.AuthorizationFailure is { } failure) return AuthorizationFailureResult(failure);
        return result.Version is null ? NotFoundProblem() : Ok(MapVersion(result.Version));
    }

    [HttpGet("{templateId:guid}/versions/{versionId:guid}/fields")]
    [HasPermission(Permissions.FormTemplate.View)]
    public async Task<ActionResult<IReadOnlyList<FormTemplateFieldResponse>>> Fields(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetFormTemplateFieldsQuery(templateId, versionId), cancellationToken);
        if (result.AuthorizationFailure is { } failure) return AuthorizationFailureResult(failure);
        return result.Fields is null
            ? NotFoundProblem()
            : Ok(result.Fields.Select(MapField).ToArray());
    }

    [HttpGet("{templateId:guid}/versions/{versionId:guid}/content")]
    [HasPermission(Permissions.FormTemplate.View)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Content(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        FormTemplateContentResult result;
        try
        {
            result = await sender.Send(
                new GetFormTemplateContentQuery(templateId, versionId), cancellationToken);
        }
        catch (IOException exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return NotFoundProblem();
        }
        if (result.AuthorizationFailure is { } failure) return AuthorizationFailureResult(failure);
        if (result.Content is null) return NotFoundProblem();
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers.ContentDisposition =
            $"inline; filename=\"{SafeFileName(result.FileName!)}\"";
        return File(result.Content, result.ContentType!, enableRangeProcessing: false);
    }

    [HttpPost]
    [HasPermission(Permissions.FormTemplate.Manage)]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.FormTemplateUploadRateLimitPolicy)]
    [RequestSizeLimit(MaximumMultipartRequestBytes)]
    public async Task<ActionResult<FormTemplateMutationResponse>> Upload(
        [FromForm] UploadFormTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null) return InvalidUploadProblem();
        await using var content = request.File.OpenReadStream();
        var result = await sender.Send(new UploadFormTemplateCommand(
            Guid.CreateVersion7(), Guid.CreateVersion7(), request.Key, request.Name,
            request.Description, request.Category, request.File.FileName,
            request.EffectiveFrom, request.EffectiveTo, content), cancellationToken);
        return MutationResult(result, created: true);
    }

    [HttpPost("{templateId:guid}/versions")]
    [HasPermission(Permissions.FormTemplate.Manage)]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.FormTemplateUploadRateLimitPolicy)]
    [RequestSizeLimit(MaximumMultipartRequestBytes)]
    public async Task<ActionResult<FormTemplateMutationResponse>> UploadVersion(
        Guid templateId,
        [FromForm] UploadFormTemplateVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null) return InvalidUploadProblem();
        await using var content = request.File.OpenReadStream();
        var result = await sender.Send(new UploadFormTemplateVersionCommand(
            templateId, Guid.CreateVersion7(), request.File.FileName,
            request.EffectiveFrom, request.EffectiveTo, content), cancellationToken);
        return MutationResult(result, created: true);
    }

    [HttpPost("{templateId:guid}/versions/{versionId:guid}/retire")]
    [HasPermission(Permissions.FormTemplate.Manage)]
    public async Task<ActionResult<FormTemplateMutationResponse>> Retire(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken) =>
        MutationResult(await sender.Send(
            new RetireFormTemplateVersionCommand(templateId, versionId), cancellationToken), created: false);

    private ActionResult<FormTemplateMutationResponse> MutationResult(
        FormTemplateOperationResult result,
        bool created)
    {
        if (result.AuthorizationFailure is { } failure) return AuthorizationFailureResult(failure);
        if (result.Status == FormTemplateOperationStatus.Succeeded)
        {
            var response = new FormTemplateMutationResponse(
                result.Template is null ? null : MapTemplate(result.Template),
                result.Version is null ? null : MapVersion(result.Version),
                result.Warnings.Select(warning => new FormTemplateWarningResponse(
                    warning.Code, warning.Message)).ToArray());
            return StatusCode(created ? StatusCodes.Status201Created : StatusCodes.Status200OK, response);
        }
        if (result.Status == FormTemplateOperationStatus.NotFound) return NotFoundProblem();
        if (result.Status == FormTemplateOperationStatus.InvalidFile)
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "The PDF form template was rejected.",
                type: ProblemTypes.FormTemplateInvalidPdf,
                extensions: new Dictionary<string, object?> { ["code"] = result.ValidationError.ToString() });
        if (result.Status == FormTemplateOperationStatus.DuplicateContent)
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "This PDF content is already registered.",
                type: ProblemTypes.FormTemplateDuplicateContent,
                extensions: new Dictionary<string, object?>
                {
                    ["templateId"] = result.Duplicate!.TemplateId,
                    ["templateKey"] = result.Duplicate.TemplateKey,
                    ["versionNumber"] = result.Duplicate.VersionNumber,
                });
        return Problem(statusCode: StatusCodes.Status409Conflict,
            title: result.Status == FormTemplateOperationStatus.InvalidState
                ? "The form template version cannot be retired from its current state."
                : "The form template could not be changed because of a conflict.",
            type: ProblemTypes.FormTemplateConflict);
    }

    private ActionResult<FormTemplateMutationResponse> InvalidUploadProblem() => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "A PDF file is required.",
        type: ProblemTypes.FormTemplateInvalidPdf);

    private ObjectResult NotFoundProblem() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Form template not found.",
        type: ProblemTypes.FormTemplateNotFound);

    private ActionResult AuthorizationFailureResult(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();

    private static FormTemplateSummaryResponse MapSummary(FormTemplateSummary item) => new(
        item.Id, item.Key, item.Name, item.Category, item.OwnerType.ToString(), item.CompanyId,
        item.IsActive, item.VersionCount, item.LatestVersionNumber, item.LatestVersionStatus?.ToString());

    private static FormTemplateResponse MapTemplate(FormTemplateDetails item) => new(
        item.Id, item.Key, item.Name, item.Description, item.Category, item.OwnerType.ToString(),
        item.CompanyId, item.IsActive, item.CreatedAtUtc, item.Versions.Select(MapVersion).ToArray());

    private static FormTemplateVersionResponse MapVersion(FormTemplateVersionDetails item) => new(
        item.Id, item.TemplateId, item.VersionNumber, item.Status.ToString(), item.OriginalFileName,
        item.ContentType, item.SizeBytes, item.Sha256, item.PageCount, item.Technology.ToString(),
        item.EffectiveFrom, item.EffectiveTo, item.UploadedAtUtc, item.FieldCount);

    private static FormTemplateFieldResponse MapField(FormTemplateFieldDetails item) => new(
        item.Id, item.PdfFieldName, item.PdfFieldType.ToString(), item.FieldPurpose.ToString(),
        item.BindingKey, item.FormatKey, item.IsUserEditable);

    private static string SafeFileName(string originalFileName)
    {
        var name = Path.GetFileName(originalFileName.Replace('\\', '/'));
        var safe = new string(name.Where(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "form-template.pdf" : safe;
    }
}

