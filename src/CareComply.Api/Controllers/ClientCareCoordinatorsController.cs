using CareComply.Api.Authorization;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientCareCoordinators;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/care-coordinators")]
public sealed class ClientCareCoordinatorsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ClientCareCoordinator.View)]
    public async Task<ActionResult<IReadOnlyList<CareCoordinatorItem>>> List(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListClientCareCoordinatorsQuery(clientId), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        return result.Items is null ? NotFound() : Ok(result.Items);
    }

    [HttpPost]
    [HasPermission(Permissions.ClientCareCoordinator.Create)]
    public async Task<ActionResult<CareCoordinatorItem>> Create(
        Guid clientId,
        SaveCareCoordinatorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateClientCareCoordinatorCommand(clientId, request.Name, request.CompanyName,
                request.Phone, request.PhoneExtension, request.Fax, request.Email, request.IsActive),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        return result.Item is null ? NotFound() : StatusCode(201, result.Item);
    }

    [HttpPut("{coordinatorId:guid}")]
    [HasPermission(Permissions.ClientCareCoordinator.Edit)]
    public async Task<ActionResult<CareCoordinatorItem>> Update(
        Guid clientId,
        Guid coordinatorId,
        SaveCareCoordinatorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateClientCareCoordinatorCommand(clientId, coordinatorId, request.Name,
                request.CompanyName, request.Phone, request.PhoneExtension, request.Fax,
                request.Email, request.IsActive),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
            return AuthorizationFailureResult(failure);
        return result.Item is null ? NotFound() : Ok(result.Item);
    }

    private ActionResult AuthorizationFailureResult(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();
}

public sealed record SaveCareCoordinatorRequest(string? Name, string? CompanyName, string? Phone,
    string? PhoneExtension, string? Fax, string? Email, bool? IsActive);
