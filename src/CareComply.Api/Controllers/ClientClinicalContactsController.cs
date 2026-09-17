using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using CareComply.Api.Authorization;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}")]
public sealed class ClientClinicalContactsController(IClientClinicalContactStore store) : ControllerBase
{
    [HttpGet("consultation-providers")]
    [HasPermission(Permissions.ClientConsultationProvider.View)]
    public async Task<ActionResult<IReadOnlyList<ClinicalContactItem>>> ListConsultationProviders(Guid clientId, CancellationToken cancellationToken)
        => await List(clientId, ClientContactType.ConsultationProvider, cancellationToken);

    [HttpPost("consultation-providers")]
    [HasPermission(Permissions.ClientConsultationProvider.Create)]
    public async Task<ActionResult<ClinicalContactItem>> CreateConsultationProvider(Guid clientId, ConsultationProviderRequest request, CancellationToken cancellationToken)
    {
        ValidateConsultationProvider(request);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var value = await store.CreateConsultationProviderAsync(clientId, request.ToInput(), cancellationToken);
        return value is null ? NotFound() : StatusCode(201, value);
    }

    [HttpPut("consultation-providers/{id:guid}")]
    [HasPermission(Permissions.ClientConsultationProvider.Edit)]
    public async Task<ActionResult<ClinicalContactItem>> UpdateConsultationProvider(Guid clientId, Guid id, ConsultationProviderRequest request, CancellationToken cancellationToken)
    {
        ValidateConsultationProvider(request);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var value = await store.UpdateConsultationProviderAsync(clientId, id, request.ToInput(), cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpGet("physicians")]
    [HasPermission(Permissions.ClientPhysician.View)]
    public async Task<ActionResult<IReadOnlyList<ClinicalContactItem>>> ListPhysicians(Guid clientId, CancellationToken cancellationToken)
        => await List(clientId, ClientContactType.Physician, cancellationToken);

    [HttpPost("physicians")]
    [HasPermission(Permissions.ClientPhysician.Create)]
    public async Task<ActionResult<ClinicalContactItem>> CreatePhysician(Guid clientId, PhysicianRequest request, CancellationToken cancellationToken)
    {
        ValidatePhysician(request);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var value = await store.CreatePhysicianAsync(clientId, request.ToInput(), cancellationToken);
        return value is null ? NotFound() : StatusCode(201, value);
    }

    [HttpPut("physicians/{id:guid}")]
    [HasPermission(Permissions.ClientPhysician.Edit)]
    public async Task<ActionResult<ClinicalContactItem>> UpdatePhysician(Guid clientId, Guid id, PhysicianRequest request, CancellationToken cancellationToken)
    {
        ValidatePhysician(request);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var value = await store.UpdatePhysicianAsync(clientId, id, request.ToInput(), cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    private async Task<ActionResult<IReadOnlyList<ClinicalContactItem>>> List(Guid clientId, ClientContactType type, CancellationToken cancellationToken)
    {
        var values = await store.ListAsync(clientId, type, cancellationToken);
        return values is null ? NotFound() : Ok(values);
    }

    private void ValidateConsultationProvider(ConsultationProviderRequest request)
    {
        RequiredName(request.Name);
        CheckLength(request.Phone, nameof(request.Phone), ClientContact.MaximumPhoneLength);
        CheckEmail(request.Email);
        CheckLength(request.SupportProviderName, nameof(request.SupportProviderName), ClientContact.MaximumNameLength);
        CheckLength(request.SupportProviderPhone, nameof(request.SupportProviderPhone), ClientContact.MaximumPhoneLength);
        CheckLength(request.SupportProviderAddress, nameof(request.SupportProviderAddress), ClientContact.MaximumAddressLength);
        RequireStatus(request.IsActive);
    }

    private void ValidatePhysician(PhysicianRequest request)
    {
        RequiredName(request.Name);
        if (!string.IsNullOrWhiteSpace(request.Npi) && !Regex.IsMatch(request.Npi, "^[0-9]{10}$"))
            ModelState.AddModelError(nameof(request.Npi), "NPI must contain exactly 10 digits.");
        CheckLength(request.Phone, nameof(request.Phone), ClientContact.MaximumPhoneLength);
        CheckLength(request.Fax, nameof(request.Fax), ClientContact.MaximumPhoneLength);
        CheckLength(request.Address, nameof(request.Address), ClientContact.MaximumAddressLength);
        CheckLength(request.PostalCode, nameof(request.PostalCode), ClientContact.MaximumPostalCodeLength);
        CheckLength(request.City, nameof(request.City), ClientContact.MaximumCityLength);
        CheckLength(request.State, nameof(request.State), ClientContact.MaximumStateLength);
        RequireStatus(request.IsActive);
    }

    private void RequiredName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) ModelState.AddModelError("Name", "Name is required.");
        else CheckLength(name, "Name", ClientContact.MaximumNameLength);
    }

    private void CheckEmail(string? email)
    {
        CheckLength(email, "Email", ClientContact.MaximumEmailLength);
        if (!string.IsNullOrWhiteSpace(email) && !new EmailAddressAttribute().IsValid(email))
            ModelState.AddModelError("Email", "Enter a valid email address.");
    }

    private void CheckLength(string? value, string field, int maximum)
    {
        if (value?.Length > maximum) ModelState.AddModelError(field, $"{field} is too long.");
    }

    private void RequireStatus(bool? status)
    {
        if (status is null) ModelState.AddModelError("IsActive", "Select a status.");
    }
}

public sealed record ConsultationProviderRequest(string? Name, string? Phone, string? Email,
    string? SupportProviderName, string? SupportProviderPhone, string? SupportProviderAddress, bool? IsActive)
{
    public SaveConsultationProvider ToInput() => new(Name!, Phone, Email, SupportProviderName,
        SupportProviderPhone, SupportProviderAddress, IsActive!.Value);
}

public sealed record PhysicianRequest(string? Name, string? Npi, string? Phone, string? Fax,
    string? Address, string? PostalCode, string? City, string? State, bool? IsActive)
{
    public SavePhysician ToInput() => new(Name!, Npi, Phone, Fax, Address, PostalCode, City, State, IsActive!.Value);
}
