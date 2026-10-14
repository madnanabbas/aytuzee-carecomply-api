using CareComply.Api.Authorization;
using CareComply.Application.Abstractions.Companies;
using CareComply.Application.Authorization;
using CareComply.Application.Features.Payers;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController, Route("api/payers")]
public sealed class PayersController(ISender sender) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Payer.View)]
    public async Task<ActionResult<IReadOnlyList<PayerResponse>>> List(
        [FromQuery] bool includeInactive, CancellationToken ct)
    {
        var result = await sender.Send(new ListPayersQuery(includeInactive), ct);
        if (result.AuthorizationFailure is { } failure) return Auth(failure);
        return result.Items is null ? Forbid() : Ok(result.Items.Select(Map).ToArray());
    }

    [HttpPost, HasPermission(Permissions.Payer.Create)]
    public async Task<ActionResult<PayerResponse>> Create(CreatePayerRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreatePayerCommand(request.Name), ct);
        if (result.AuthorizationFailure is { } failure) return Auth(failure);
        return MapMutation(result, true);
    }

    [HttpPut("{payerId:guid}"), HasPermission(Permissions.Payer.Edit)]
    public async Task<ActionResult<PayerResponse>> Update(Guid payerId, UpdatePayerRequest request,
        CancellationToken ct)
    {
        var result = await sender.Send(new UpdatePayerCommand(payerId, request.Name,
            request.IsActive, request.EditorVersion), ct);
        if (result.AuthorizationFailure is { } failure) return Auth(failure);
        return MapMutation(result, false);
    }

    private ActionResult<PayerResponse> MapMutation(PayerResult result, bool created) => result.Status switch
    {
        PayerMutationStatus.Succeeded when result.Item is { } item =>
            created ? StatusCode(StatusCodes.Status201Created, Map(item)) : Ok(Map(item)),
        PayerMutationStatus.NotFound => NotFound(),
        PayerMutationStatus.TenantUnavailable => Forbid(),
        PayerMutationStatus.DuplicateName => Conflict(new ProblemDetails { Title = "A payer with this name already exists." }),
        PayerMutationStatus.Conflict => Conflict(new ProblemDetails { Title = "The payer was changed. Reload and try again." }),
        _ => Conflict(),
    };

    private ActionResult Auth(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();
    private static PayerResponse Map(PayerItem item) => new(item.Id, item.Name, item.IsActive, item.EditorVersion);
}

public sealed record CreatePayerRequest(string? Name);
public sealed record UpdatePayerRequest(string? Name, bool? IsActive, string? EditorVersion);
public sealed record PayerResponse(Guid Id, string Name, bool IsActive, string EditorVersion);
