using CareComply.Api.Authorization;
using CareComply.Api.Contracts.Clients;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientHospitalizations;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/hospitalizations")]
public sealed class ClientHospitalizationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ClientHospitalization.View)]
    public async Task<ActionResult<IReadOnlyList<ClientHospitalizationResponse>>> List(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ListClientHospitalizationsQuery(clientId),
            cancellationToken);

        if (result.AuthorizationFailure is not null)
        {
            return Forbid();
        }

        return result.List is null
            ? NotFound()
            : Ok(result.List.Items.Select(Map));
    }

    [HttpPost]
    [HasPermission(Permissions.ClientHospitalization.Create)]
    public async Task<ActionResult<ClientHospitalizationResponse>> Create(
        Guid clientId,
        SaveClientHospitalizationRequest request,
        CancellationToken cancellationToken) =>
        MapMutation(
            await sender.Send(
                new SaveClientHospitalizationCommand(
                    clientId,
                    null,
                    request.HospitalName,
                    request.AdmittedAtUtc,
                    request.DischargedAtUtc,
                    request.Reason,
                    request.Diagnosis,
                    request.DischargeInstructions,
                    null),
                cancellationToken),
            created: true);

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.ClientHospitalization.Edit)]
    public async Task<ActionResult<ClientHospitalizationResponse>> Update(
        Guid clientId,
        Guid id,
        SaveClientHospitalizationRequest request,
        CancellationToken cancellationToken) =>
        MapMutation(
            await sender.Send(
                new SaveClientHospitalizationCommand(
                    clientId,
                    id,
                    request.HospitalName,
                    request.AdmittedAtUtc,
                    request.DischargedAtUtc,
                    request.Reason,
                    request.Diagnosis,
                    request.DischargeInstructions,
                    request.EditorVersion),
                cancellationToken),
            created: false);

    private ActionResult<ClientHospitalizationResponse> MapMutation(
        HospitalizationResult result,
        bool created) =>
        result.Mutation?.Status switch
        {
            HospitalizationMutationStatus.Succeeded when result.Mutation.Value is { } hospitalization =>
                created
                    ? StatusCode(StatusCodes.Status201Created, Map(hospitalization))
                    : Ok(Map(hospitalization)),
            HospitalizationMutationStatus.ClientNotFound => NotFound(),
            HospitalizationMutationStatus.NotFound => NotFound(),
            _ => Conflict(),
        };

    private static ClientHospitalizationResponse Map(HospitalizationItem hospitalization) => new(
        hospitalization.Id,
        hospitalization.HospitalName,
        hospitalization.AdmittedAtUtc,
        hospitalization.DischargedAtUtc,
        hospitalization.Reason,
        hospitalization.Diagnosis,
        hospitalization.DischargeInstructions,
        hospitalization.DischargedAtUtc.HasValue ? "Discharged" : "Admitted",
        hospitalization.EditorVersion);
}
