using CareComply.Api.Authorization;
using CareComply.Api.Contracts.ClientForms;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientForms.Queries.GetClientFormBindings;
using CareComply.Application.Features.ClientForms.Queries.GetClientFormContent;
using CareComply.Application.Features.ClientForms.Queries.ListClientFormTemplates;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/form-templates")]
public sealed class ClientFormsController(ISender sender, IFormContentStore contentStore)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ClientForm.View)]
    public async Task<ActionResult<ClientFormTemplateListResponse>> List(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ListClientFormTemplatesQuery(clientId),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        if (result.Templates is null) return NotFoundProblem();

        return Ok(new ClientFormTemplateListResponse(result.Templates.Select(template =>
            new ClientFormTemplateListItemResponse(
                template.TemplateVersionId,
                template.TemplateKey,
                template.Name,
                template.Description,
                template.Category,
                template.VersionNumber,
                template.PageCount,
                template.EffectiveFrom,
                template.EffectiveTo)).ToArray()));
    }

    [HttpGet("{versionId:guid}/content")]
    [HasPermission(Permissions.ClientForm.View)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Content(
        Guid clientId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetClientFormContentQuery(clientId, versionId),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        if (result.Content is null) return NotFoundProblem();

        Stream content;
        try
        {
            content = await contentStore.OpenReadAsync(
                result.Content.StorageObjectKey,
                cancellationToken);
        }
        catch (IOException exception) when (
            exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return NotFoundProblem();
        }

        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers.ContentDisposition =
            $"inline; filename=\"{SafeFileName(result.Content.TemplateKey)}-v{result.Content.VersionNumber}.pdf\"";
        return File(content, result.Content.ContentType, enableRangeProcessing: false);
    }

    [HttpGet("{versionId:guid}/bindings")]
    [HasPermission(Permissions.ClientForm.View)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ClientFormBindingsResponse>> Bindings(
        Guid clientId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetClientFormBindingsQuery(clientId, versionId),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        if (result.Bindings is null) return NotFoundProblem();

        Response.Headers.CacheControl = "no-store";
        return Ok(new ClientFormBindingsResponse(
            result.Bindings.TemplateVersionId,
            result.Bindings.Fields.Select(field => new ClientFormBindingFieldResponse(
                field.PdfFieldName,
                field.Value,
                field.UserEditable)).ToArray()));
    }

    private ObjectResult NotFoundProblem() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Client form not found.",
        type: ProblemTypes.ClientFormNotFound);

    private ActionResult AuthorizationFailureResult(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();

    private static string SafeFileName(string templateKey)
    {
        var safe = new string(templateKey.Where(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_').ToArray());
        return string.IsNullOrEmpty(safe) ? "client-form" : safe;
    }
}
