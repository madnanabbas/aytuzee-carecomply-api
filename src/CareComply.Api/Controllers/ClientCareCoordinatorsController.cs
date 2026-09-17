using System.ComponentModel.DataAnnotations;
using CareComply.Api.Authorization;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/care-coordinators")]
public sealed class ClientCareCoordinatorsController(IClientCareCoordinatorStore store) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ClientCareCoordinator.View)]
    public async Task<ActionResult<IReadOnlyList<CareCoordinatorItem>>> List(Guid clientId, CancellationToken cancellationToken)
    {
        var coordinators = await store.ListAsync(clientId, cancellationToken);
        return coordinators is null ? NotFound() : Ok(coordinators);
    }

    [HttpPost]
    [HasPermission(Permissions.ClientCareCoordinator.Create)]
    public async Task<ActionResult<CareCoordinatorItem>> Create(Guid clientId, SaveCareCoordinatorRequest request, CancellationToken cancellationToken)
    {
        if (!Validate(request)) return ValidationProblem(ModelState);
        var coordinator = await store.CreateAsync(clientId, request.ToInput(), cancellationToken);
        return coordinator is null ? NotFound() : StatusCode(201, coordinator);
    }

    [HttpPut("{coordinatorId:guid}")]
    [HasPermission(Permissions.ClientCareCoordinator.Edit)]
    public async Task<ActionResult<CareCoordinatorItem>> Update(Guid clientId, Guid coordinatorId, SaveCareCoordinatorRequest request, CancellationToken cancellationToken)
    {
        if (!Validate(request)) return ValidationProblem(ModelState);
        var coordinator = await store.UpdateAsync(clientId, coordinatorId, request.ToInput(), cancellationToken);
        return coordinator is null ? NotFound() : Ok(coordinator);
    }

    private bool Validate(SaveCareCoordinatorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) ModelState.AddModelError(nameof(request.Name), "Name is required.");
        else if (request.Name.Length > ClientContact.MaximumNameLength) ModelState.AddModelError(nameof(request.Name), "Name is too long.");
        CheckLength(request.CompanyName, nameof(request.CompanyName), ClientContact.MaximumCompanyLength);
        CheckLength(request.Phone, nameof(request.Phone), ClientContact.MaximumPhoneLength);
        CheckLength(request.PhoneExtension, nameof(request.PhoneExtension), ClientContact.MaximumExtensionLength);
        CheckLength(request.Fax, nameof(request.Fax), ClientContact.MaximumPhoneLength);
        CheckLength(request.Email, nameof(request.Email), ClientContact.MaximumEmailLength);
        if (!string.IsNullOrWhiteSpace(request.Email) && !new EmailAddressAttribute().IsValid(request.Email))
            ModelState.AddModelError(nameof(request.Email), "Enter a valid email address.");
        if (request.IsActive is null) ModelState.AddModelError(nameof(request.IsActive), "Select a status.");
        return ModelState.IsValid;
    }

    private void CheckLength(string? value, string name, int maximum)
    {
        if (value?.Length > maximum) ModelState.AddModelError(name, $"{name} is too long.");
    }
}

public sealed record SaveCareCoordinatorRequest(string? Name, string? CompanyName, string? Phone,
    string? PhoneExtension, string? Fax, string? Email, bool? IsActive)
{
    public SaveCareCoordinator ToInput() => new(Name!, CompanyName, Phone, PhoneExtension, Fax, Email, IsActive!.Value);
}
