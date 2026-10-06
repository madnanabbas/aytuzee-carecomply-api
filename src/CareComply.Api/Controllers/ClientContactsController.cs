using CareComply.Api.Authorization;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientContacts;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/contacts")]
public sealed class ClientContactsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ClientContact.View)]
    public async Task<ActionResult<IReadOnlyList<ClientContactResponse>>> List(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListClientContactsQuery(clientId), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        return result.Items is null ? NotFound() : Ok(result.Items.Select(Map).ToArray());
    }

    [HttpPost]
    [HasPermission(Permissions.ClientContact.Create)]
    public async Task<ActionResult<ClientContactResponse>> Create(
        Guid clientId,
        SaveClientContactRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateClientContactCommand(clientId, request.Type, request.Name,
                request.Relationship, request.Phone, request.Email, request.Address,
                request.IsActive),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        return result.Item is null ? NotFound() : StatusCode(201, Map(result.Item));
    }

    [HttpPut("{contactId:guid}")]
    [HasPermission(Permissions.ClientContact.Edit)]
    public async Task<ActionResult<ClientContactResponse>> Update(
        Guid clientId,
        Guid contactId,
        SaveClientContactRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateClientContactCommand(clientId, contactId, request.Type, request.Name,
                request.Relationship, request.Phone, request.Email, request.Address,
                request.IsActive),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        return result.Item is null ? NotFound() : Ok(Map(result.Item));
    }

    private ActionResult AuthorizationFailureResult(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();

    private static ClientContactResponse Map(ClientContactItem contact) =>
        new(contact.Id, contact.Type.ToString(), contact.Name, contact.Relationship, contact.Phone,
            contact.Email, contact.Address, contact.IsActive);
}

public sealed record SaveClientContactRequest(string? Type, string? Name, string? Relationship,
    string? Phone, string? Email, string? Address, bool? IsActive);

public sealed record ClientContactResponse(Guid Id, string Type, string Name, string Relationship,
    string Phone, string Email, string Address, bool IsActive);
