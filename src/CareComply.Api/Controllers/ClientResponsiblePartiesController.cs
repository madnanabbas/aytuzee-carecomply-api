using CareComply.Api.Authorization;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientResponsibleParties;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/responsible-parties")]
public sealed class ClientResponsiblePartiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ClientResponsibleParty.View)]
    public async Task<ActionResult<IReadOnlyList<ResponsiblePartyItem>>> List(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListClientResponsiblePartiesQuery(clientId), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        return result.Items is null ? NotFound() : Ok(result.Items);
    }

    [HttpPost]
    [HasPermission(Permissions.ClientResponsibleParty.Create)]
    public async Task<ActionResult<ResponsiblePartyItem>> Create(
        Guid clientId,
        ResponsiblePartyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateClientResponsiblePartyCommand(clientId, request.Name, request.Relationship,
                request.Phone, request.Email, request.Address, request.LivesWithClient,
                request.HasSpenddown, request.IsActive),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        return result.Item is null ? NotFound() : StatusCode(201, result.Item);
    }

    [HttpPut("{partyId:guid}")]
    [HasPermission(Permissions.ClientResponsibleParty.Edit)]
    public async Task<ActionResult<ResponsiblePartyItem>> Update(
        Guid clientId,
        Guid partyId,
        ResponsiblePartyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateClientResponsiblePartyCommand(clientId, partyId, request.Name,
                request.Relationship, request.Phone, request.Email, request.Address,
                request.LivesWithClient, request.HasSpenddown, request.IsActive),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        return result.Item is null ? NotFound() : Ok(result.Item);
    }

    private ActionResult AuthorizationFailureResult(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();
}

public sealed record ResponsiblePartyRequest(string? Name, string? Relationship, string? Phone,
    string? Email, string? Address, bool? LivesWithClient, bool? HasSpenddown, bool? IsActive);
