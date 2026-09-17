using System.ComponentModel.DataAnnotations;
using CareComply.Api.Authorization;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/responsible-parties")]
public sealed class ClientResponsiblePartiesController(IClientResponsiblePartyStore store) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ClientResponsibleParty.View)]
    public async Task<ActionResult<IReadOnlyList<ResponsiblePartyItem>>> List(Guid clientId, CancellationToken cancellationToken)
    {
        var parties = await store.ListAsync(clientId, cancellationToken);
        return parties is null ? NotFound() : Ok(parties);
    }

    [HttpPost]
    [HasPermission(Permissions.ClientResponsibleParty.Create)]
    public async Task<ActionResult<ResponsiblePartyItem>> Create(Guid clientId, ResponsiblePartyRequest request, CancellationToken cancellationToken)
    {
        if (!Validate(request)) return ValidationProblem(ModelState);
        var party = await store.CreateAsync(clientId, request.ToInput(), cancellationToken);
        return party is null ? NotFound() : StatusCode(201, party);
    }

    [HttpPut("{partyId:guid}")]
    [HasPermission(Permissions.ClientResponsibleParty.Edit)]
    public async Task<ActionResult<ResponsiblePartyItem>> Update(Guid clientId, Guid partyId, ResponsiblePartyRequest request, CancellationToken cancellationToken)
    {
        if (!Validate(request)) return ValidationProblem(ModelState);
        var party = await store.UpdateAsync(clientId, partyId, request.ToInput(), cancellationToken);
        return party is null ? NotFound() : Ok(party);
    }

    private bool Validate(ResponsiblePartyRequest request)
    {
        CheckRequired(request.Name, nameof(request.Name), ClientContact.MaximumNameLength);
        CheckRequired(request.Relationship, nameof(request.Relationship), ClientContact.MaximumRelationshipLength);
        CheckRequired(request.Phone, nameof(request.Phone), ClientContact.MaximumPhoneLength);
        if (!string.IsNullOrWhiteSpace(request.Email)
            && (request.Email.Length > ClientContact.MaximumEmailLength || !new EmailAddressAttribute().IsValid(request.Email)))
            ModelState.AddModelError(nameof(request.Email), "Enter a valid email address.");
        if (request.Address?.Length > ClientContact.MaximumAddressLength)
            ModelState.AddModelError(nameof(request.Address), "Address is too long.");
        if (request.LivesWithClient is null) ModelState.AddModelError(nameof(request.LivesWithClient), "Select whether this person lives with the client.");
        if (request.HasSpenddown is null) ModelState.AddModelError(nameof(request.HasSpenddown), "Select whether this person has spenddown responsibility.");
        if (request.IsActive is null) ModelState.AddModelError(nameof(request.IsActive), "Select a status.");
        return ModelState.IsValid;
    }

    private void CheckRequired(string? value, string field, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value)) ModelState.AddModelError(field, $"{field} is required.");
        else if (value.Length > maximum) ModelState.AddModelError(field, $"{field} is too long.");
    }
}

public sealed record ResponsiblePartyRequest(string? Name, string? Relationship, string? Phone, string? Email,
    string? Address, bool? LivesWithClient, bool? HasSpenddown, bool? IsActive)
{
    public SaveResponsibleParty ToInput() => new(Name!, Relationship!, Phone!, Email, Address,
        LivesWithClient!.Value, HasSpenddown!.Value, IsActive!.Value);
}
