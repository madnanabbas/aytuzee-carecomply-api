using CareComply.Api.Authorization;
using CareComply.Api.Contracts.Clients;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.ClientDocuments;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientDocuments;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/documents")]
public sealed class ClientDocumentsController(ISender sender) : ControllerBase
{
    private const long MaximumMultipartRequestBytes = 11 * 1024 * 1024;

    [HttpGet]
    [HasPermission(Permissions.ClientDocument.View)]
    public async Task<ActionResult<IReadOnlyList<ClientDocumentResponse>>> List(Guid clientId, CancellationToken ct)
    {
        var result = await sender.Send(new ListClientDocumentsQuery(clientId), ct);
        if (result.AuthorizationFailure is { } failure) return AuthorizationFailureResult(failure);
        return result.Items is null ? ClientNotFound() : Ok(result.Items.Select(Map).ToArray());
    }

    [HttpGet("{documentId:guid}")]
    [HasPermission(Permissions.ClientDocument.View)]
    public async Task<ActionResult<ClientDocumentDetailsResponse>> Get(Guid clientId, Guid documentId, CancellationToken ct)
    {
        var result = await sender.Send(new GetClientDocumentQuery(clientId, documentId), ct);
        if (result.AuthorizationFailure is { } failure) return AuthorizationFailureResult(failure);
        return result.Details is null ? DocumentNotFound() : Ok(Map(result.Details));
    }

    [HttpPost]
    [HasPermission(Permissions.ClientDocument.Upload)]
    [RequestSizeLimit(MaximumMultipartRequestBytes)]
    public async Task<ActionResult<ClientDocumentDetailsResponse>> Upload(
        Guid clientId, [FromForm] UploadClientDocumentRequest request, CancellationToken ct)
    {
        if (request.File is null) return InvalidFile("A PDF, JPEG or PNG file is required.");
        await using var stream = request.File.OpenReadStream();
        return Mutation(await sender.Send(new UploadClientDocumentCommand(clientId, request.Category,
            request.Title, request.Description, request.ExpiryDate, request.ReviewDate,
            request.File.FileName, stream), ct), created: true);
    }

    [HttpPut("{documentId:guid}")]
    [HasPermission(Permissions.ClientDocument.Upload)]
    public async Task<ActionResult<ClientDocumentDetailsResponse>> Update(
        Guid clientId, Guid documentId, UpdateClientDocumentRequest request, CancellationToken ct) =>
        Mutation(await sender.Send(new UpdateClientDocumentCommand(clientId, documentId,
            request.Category, request.Title, request.Description, request.ExpiryDate,
            request.ReviewDate, request.EditorVersion), ct));

    [HttpPost("{documentId:guid}/versions")]
    [HasPermission(Permissions.ClientDocument.Replace)]
    [RequestSizeLimit(MaximumMultipartRequestBytes)]
    public async Task<ActionResult<ClientDocumentDetailsResponse>> Replace(
        Guid clientId, Guid documentId, [FromForm] ReplaceClientDocumentRequest request, CancellationToken ct)
    {
        if (request.File is null) return InvalidFile("A PDF, JPEG or PNG file is required.");
        await using var stream = request.File.OpenReadStream();
        return Mutation(await sender.Send(new ReplaceClientDocumentCommand(clientId, documentId,
            request.EditorVersion, request.File.FileName, stream), ct));
    }

    [HttpPost("{documentId:guid}/archive")]
    [HasPermission(Permissions.ClientDocument.Archive)]
    public async Task<ActionResult<ClientDocumentDetailsResponse>> Archive(
        Guid clientId, Guid documentId, ArchiveClientDocumentRequest request, CancellationToken ct) =>
        Mutation(await sender.Send(new ArchiveClientDocumentCommand(clientId, documentId, request.EditorVersion), ct));

    [HttpGet("{documentId:guid}/versions/{versionId:guid}/content")]
    [HasPermission(Permissions.ClientDocument.Download)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Download(Guid clientId, Guid documentId, Guid versionId, CancellationToken ct)
    {
        ClientDocumentResult result;
        try { result = await sender.Send(new DownloadClientDocumentQuery(clientId, documentId, versionId), ct); }
        catch (IOException ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return DocumentNotFound(); }
        if (result.AuthorizationFailure is { } failure) return AuthorizationFailureResult(failure);
        if (result.Download is not { } file) return DocumentNotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        return File(file.Content, file.ContentType, SafeFileName(file.FileName), enableRangeProcessing: false);
    }

    private ActionResult<ClientDocumentDetailsResponse> Mutation(ClientDocumentResult result, bool created = false)
    {
        if (result.AuthorizationFailure is { } failure) return AuthorizationFailureResult(failure);
        if (result.ValidationError is { } validation) return validation switch
        {
            ClientDocumentFileValidationError.TooLarge => InvalidFile("The file exceeds the 10 MB limit."),
            ClientDocumentFileValidationError.UnsupportedType => InvalidFile("Only PDF, JPEG and PNG files are supported."),
            _ => InvalidFile("The file is malformed or incomplete."),
        };
        return result.Mutation?.Status switch
        {
            ClientDocumentMutationStatus.Succeeded when result.Mutation.Details is { } details => created
                ? StatusCode(StatusCodes.Status201Created, Map(details)) : Ok(Map(details)),
            ClientDocumentMutationStatus.ClientNotFound => ClientNotFound(),
            ClientDocumentMutationStatus.DocumentNotFound => DocumentNotFound(),
            ClientDocumentMutationStatus.Duplicate => Problem(statusCode: 409,
                title: "This file already exists for the client.", type: ProblemTypes.ClientDocumentDuplicate),
            ClientDocumentMutationStatus.Archived => Problem(statusCode: 409,
                title: "An archived document cannot be changed.", type: ProblemTypes.ClientDocumentConflict),
            _ => Problem(statusCode: 409,
                title: "The document changed since it was loaded. Refresh and try again.", type: ProblemTypes.ClientDocumentConflict),
        };
    }

    private ObjectResult InvalidFile(string title) => Problem(statusCode: 400, title: title, type: ProblemTypes.ClientDocumentInvalidFile);
    private ObjectResult ClientNotFound() => Problem(statusCode: 404, title: "The requested client was not found.", type: ProblemTypes.ClientNotFound);
    private ObjectResult DocumentNotFound() => Problem(statusCode: 404, title: "The requested document was not found.", type: ProblemTypes.ClientDocumentNotFound);
    private ActionResult AuthorizationFailureResult(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();

    private static ClientDocumentResponse Map(ClientDocumentItem x) => new(x.Id, x.ClientId,
        x.Category, x.Title, x.Description, x.ExpiryDate, x.ReviewDate, x.Status,
        x.CurrentVersionId, x.CurrentVersionNumber, x.OriginalFileName, x.ContentType,
        x.SizeBytes, x.UploadedAtUtc, x.UpdatedAtUtc, x.EditorVersion);
    private static ClientDocumentDetailsResponse Map(ClientDocumentDetails x) => new(Map(x.Document),
        x.Versions.Select(v => new ClientDocumentVersionResponse(v.Id, v.VersionNumber,
            v.OriginalFileName, v.ContentType, v.SizeBytes, v.UploadedAtUtc, v.IsCurrent)).ToArray());
    private static string SafeFileName(string name) => name.Replace("\"", string.Empty, StringComparison.Ordinal)
        .Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
}
