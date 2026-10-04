using CareComply.Api.Authorization;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientClinicalContacts;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}")]
public sealed class ClientClinicalContactsController(ISender sender) : ControllerBase
{
    [HttpGet("consultation-providers")]
    [HasPermission(Permissions.ClientConsultationProvider.View)]
    public async Task<ActionResult<IReadOnlyList<ClinicalContactItem>>> ListConsultationProviders(
        Guid clientId,
        CancellationToken cancellationToken) =>
        ListResult(await sender.Send(new ListConsultationProvidersQuery(clientId), cancellationToken));

    [HttpPost("consultation-providers")]
    [HasPermission(Permissions.ClientConsultationProvider.Create)]
    public async Task<ActionResult<ClinicalContactItem>> CreateConsultationProvider(
        Guid clientId,
        ConsultationProviderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateConsultationProviderCommand(clientId, request.Name, request.Phone,
                request.Email, request.SupportProviderName, request.SupportProviderPhone,
                request.SupportProviderAddress, request.IsActive),
            cancellationToken);
        return Mutation(result, true);
    }

    [HttpPut("consultation-providers/{id:guid}")]
    [HasPermission(Permissions.ClientConsultationProvider.Edit)]
    public async Task<ActionResult<ClinicalContactItem>> UpdateConsultationProvider(
        Guid clientId,
        Guid id,
        ConsultationProviderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateConsultationProviderCommand(clientId, id, request.Name, request.Phone,
                request.Email, request.SupportProviderName, request.SupportProviderPhone,
                request.SupportProviderAddress, request.IsActive),
            cancellationToken);
        return Mutation(result);
    }

    [HttpGet("physicians")]
    [HasPermission(Permissions.ClientPhysician.View)]
    public async Task<ActionResult<IReadOnlyList<ClinicalContactItem>>> ListPhysicians(
        Guid clientId,
        CancellationToken cancellationToken) =>
        ListResult(await sender.Send(new ListPhysiciansQuery(clientId), cancellationToken));

    [HttpPost("physicians")]
    [HasPermission(Permissions.ClientPhysician.Create)]
    public async Task<ActionResult<ClinicalContactItem>> CreatePhysician(
        Guid clientId,
        PhysicianRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreatePhysicianCommand(clientId, request.Name, request.Npi, request.Phone,
                request.Fax, request.Address, request.PostalCode, request.City, request.State,
                request.IsActive),
            cancellationToken);
        return Mutation(result, true);
    }

    [HttpPut("physicians/{id:guid}")]
    [HasPermission(Permissions.ClientPhysician.Edit)]
    public async Task<ActionResult<ClinicalContactItem>> UpdatePhysician(
        Guid clientId,
        Guid id,
        PhysicianRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdatePhysicianCommand(clientId, id, request.Name, request.Npi, request.Phone,
                request.Fax, request.Address, request.PostalCode, request.City, request.State,
                request.IsActive),
            cancellationToken);
        return Mutation(result);
    }

    private ActionResult<IReadOnlyList<ClinicalContactItem>> ListResult(
        ClientClinicalContactResult result)
    {
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        return result.Items is null ? NotFound() : Ok(result.Items);
    }

    private ActionResult<ClinicalContactItem> Mutation(
        ClientClinicalContactResult result,
        bool created = false)
    {
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        if (result.Item is null)
            return NotFound();
        return created ? StatusCode(201, result.Item) : Ok(result.Item);
    }

    private ActionResult AuthorizationFailureResult(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();
}

public sealed record ConsultationProviderRequest(string? Name, string? Phone, string? Email,
    string? SupportProviderName, string? SupportProviderPhone, string? SupportProviderAddress,
    bool? IsActive);

public sealed record PhysicianRequest(string? Name, string? Npi, string? Phone, string? Fax,
    string? Address, string? PostalCode, string? City, string? State, bool? IsActive);
