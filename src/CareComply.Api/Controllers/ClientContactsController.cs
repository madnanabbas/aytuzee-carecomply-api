using System.ComponentModel.DataAnnotations;
using CareComply.Api.Authorization;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/contacts")]
public sealed class ClientContactsController(IClientContactStore store) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ClientContact.View)]
    public async Task<ActionResult<IReadOnlyList<ClientContactResponse>>> List(Guid clientId, CancellationToken cancellationToken)
    {
        var contacts = await store.ListAsync(clientId, cancellationToken);
        return contacts is null ? NotFound() : Ok(contacts.Select(Map).ToArray());
    }

    [HttpPost]
    [HasPermission(Permissions.ClientContact.Create)]
    public async Task<ActionResult<ClientContactResponse>> Create(Guid clientId, SaveClientContactRequest request, CancellationToken cancellationToken)
    {
        if (!Validate(request, out var type)) return ValidationProblem(ModelState);
        var contact = await store.CreateAsync(clientId, request.ToInput(type), cancellationToken);
        return contact is null ? NotFound() : StatusCode(201, Map(contact));
    }

    [HttpPut("{contactId:guid}")]
    [HasPermission(Permissions.ClientContact.Edit)]
    public async Task<ActionResult<ClientContactResponse>> Update(Guid clientId, Guid contactId, SaveClientContactRequest request, CancellationToken cancellationToken)
    {
        if (!Validate(request, out var type)) return ValidationProblem(ModelState);
        var contact = await store.UpdateAsync(clientId, contactId, request.ToInput(type), cancellationToken);
        return contact is null ? NotFound() : Ok(Map(contact));
    }

    private bool Validate(SaveClientContactRequest request, out ClientContactType type)
    {
        type = request.Type switch
        {
            "General" => ClientContactType.General,
            "Emergency" => ClientContactType.Emergency,
            _ => default,
        };
        if (type == default) ModelState.AddModelError(nameof(request.Type), "Select General or Emergency.");
        CheckRequired(request.Name, nameof(request.Name), ClientContact.MaximumNameLength);
        CheckRequired(request.Relationship, nameof(request.Relationship), ClientContact.MaximumRelationshipLength);
        CheckRequired(request.Phone, nameof(request.Phone), ClientContact.MaximumPhoneLength);
        if (!string.IsNullOrWhiteSpace(request.Email)
            && (request.Email.Length > ClientContact.MaximumEmailLength || !new EmailAddressAttribute().IsValid(request.Email)))
            ModelState.AddModelError(nameof(request.Email), "Enter a valid email address.");
        if (request.Address?.Length > ClientContact.MaximumAddressLength)
            ModelState.AddModelError(nameof(request.Address), "Address is too long.");
        if (request.IsActive is null) ModelState.AddModelError(nameof(request.IsActive), "Select a status.");
        return ModelState.IsValid;
    }

    private void CheckRequired(string? value, string field, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value)) ModelState.AddModelError(field, $"{field} is required.");
        else if (value.Length > maximum) ModelState.AddModelError(field, $"{field} is too long.");
    }

    private static ClientContactResponse Map(ClientContactItem contact) => new(contact.Id,
        contact.Type.ToString(), contact.Name, contact.Relationship, contact.Phone, contact.Email,
        contact.Address, contact.IsActive);
}

public sealed record SaveClientContactRequest(string? Type, string? Name, string? Relationship,
    string? Phone, string? Email, string? Address, bool? IsActive)
{
    public SaveClientContact ToInput(ClientContactType type) => new(type, Name!, Relationship!,
        Phone!, Email, Address, IsActive!.Value);
}

public sealed record ClientContactResponse(Guid Id, string Type, string Name, string Relationship,
    string Phone, string Email, string Address, bool IsActive);
