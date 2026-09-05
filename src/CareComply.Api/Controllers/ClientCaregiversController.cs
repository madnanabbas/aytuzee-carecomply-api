using CareComply.Api.Authorization;
using CareComply.Api.Contracts.Clients;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientCaregivers;
using CareComply.Domain.Clients;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/caregivers")]
public sealed class ClientCaregiversController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ClientCaregiver.View)]
    public async Task<ActionResult<IReadOnlyList<ClientCaregiverResponse>>> List(Guid clientId, CancellationToken ct)
    { var result = await sender.Send(new ListClientCaregiversQuery(clientId), ct); if (result.AuthorizationFailure is { } failure) return Auth(failure); return result.Value is null ? ClientMissing() : Ok(result.Value.Items.Select(Map).ToArray()); }

    [HttpGet("candidates")]
    [HasPermission(Permissions.ClientCaregiver.View)]
    public async Task<ActionResult<IReadOnlyList<ClientCaregiverCandidateResponse>>> Candidates(CancellationToken ct)
    { var result = await sender.Send(new ListClientCaregiverCandidatesQuery(), ct); if (result.AuthorizationFailure is { } failure) return Auth(failure); return Ok((result.Value ?? []).Select(x => new ClientCaregiverCandidateResponse(x.Id, x.Name, x.Title, x.Phone, x.Email)).ToArray()); }

    [HttpPost]
    [HasPermission(Permissions.ClientCaregiver.Create)]
    public async Task<ActionResult<ClientCaregiverResponse>> Create(Guid clientId, SaveClientCaregiverRequest request, CancellationToken ct)
    { if (!ParseType(request.Type, out var type)) return BadType(request.Type); return Mutation(await sender.Send(new CreateClientCaregiverCommand(clientId, request.UserId, type, request.IsPreferred, request.Services, request.StartDate, request.EndDate, request.Notes), ct), true); }

    [HttpPut("{assignmentId:guid}")]
    [HasPermission(Permissions.ClientCaregiver.Edit)]
    public async Task<ActionResult<ClientCaregiverResponse>> Update(Guid clientId, Guid assignmentId, SaveClientCaregiverRequest request, CancellationToken ct)
    { if (!ParseType(request.Type, out var type)) return BadType(request.Type); return Mutation(await sender.Send(new UpdateClientCaregiverCommand(clientId, assignmentId, type, request.IsPreferred, request.Services, request.StartDate, request.EndDate, request.Notes, request.EditorVersion ?? string.Empty), ct)); }

    [HttpPost("{assignmentId:guid}/end")]
    [HasPermission(Permissions.ClientCaregiver.Edit)]
    public async Task<ActionResult<ClientCaregiverResponse>> End(Guid clientId, Guid assignmentId, EndClientCaregiverRequest request, CancellationToken ct) => Mutation(await sender.Send(new EndClientCaregiverCommand(clientId, assignmentId, request.EndDate, request.EditorVersion), ct));

    private ActionResult<ClientCaregiverResponse> Mutation(ClientCaregiverOperationResult result, bool created = false)
    { if (result.AuthorizationFailure is { } failure) return Auth(failure); return result.Mutation?.Status switch {
        ClientCaregiverMutationStatus.Succeeded when result.Mutation.Assignment is { } value => created ? StatusCode(201, Map(value)) : Ok(Map(value)),
        ClientCaregiverMutationStatus.ClientNotFound => ClientMissing(),
        ClientCaregiverMutationStatus.AssignmentNotFound => Problem(statusCode: 404, title: "The caregiver assignment was not found.", type: ProblemTypes.ClientCaregiverNotFound),
        ClientCaregiverMutationStatus.InvalidCaregiver => Problem(statusCode: 400, title: "Select an active caregiver.", type: ProblemTypes.ClientCaregiverInvalid),
        ClientCaregiverMutationStatus.DuplicateActiveAssignment => Problem(statusCode: 409, title: "This caregiver already has an active assignment for the client.", type: ProblemTypes.ClientCaregiverDuplicate),
        ClientCaregiverMutationStatus.PrimaryConflict => Problem(statusCode: 409, title: "The client already has an active primary caregiver. Change or end that assignment first.", type: ProblemTypes.ClientCaregiverPrimaryConflict),
        ClientCaregiverMutationStatus.InvalidState => Problem(statusCode: 409, title: "The caregiver assignment is no longer editable.", type: ProblemTypes.ClientCaregiverConflict),
        _ => Problem(statusCode: 409, title: "The caregiver assignment changed. Refresh and try again.", type: ProblemTypes.ClientCaregiverConflict) }; }
    private ObjectResult ClientMissing() => Problem(statusCode: 404, title: "The requested client was not found.", type: ProblemTypes.ClientNotFound);
    private ActionResult Auth(AuthorizationFailure value) => value == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();
    private BadRequestObjectResult BadType(string? value) => BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["type"] = [$"'{value ?? string.Empty}' is invalid. Allowed values: Primary, Backup."] }) { Status = 400, Title = "One or more validation errors occurred.", Type = ProblemTypes.Validation });
    private static bool ParseType(string? value, out ClientCaregiverType type) => Enum.TryParse(value, true, out type) && Enum.IsDefined(type);
    private static ClientCaregiverResponse Map(ClientCaregiverItem x) => new(x.Id, x.UserId, x.Name, x.Title, x.Phone, x.Email, x.Type.ToString(), x.IsPreferred, x.Services, x.StartDate, x.EndDate, x.Notes, x.Status.ToString(), x.EditorVersion);
}
