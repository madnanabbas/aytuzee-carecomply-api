using CareComply.Api.Authorization;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientCoverages;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController, Route("api/clients/{clientId:guid}/coverages")]
public sealed class ClientCoveragesController(ISender sender) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.ClientCoverage.View)]
    public async Task<ActionResult<IReadOnlyList<ClientCoverageResponse>>> List(Guid clientId, CancellationToken ct)
    {
        var result = await sender.Send(new ListClientCoveragesQuery(clientId), ct);
        if (result.AuthorizationFailure is { } failure) return Auth(failure);
        return result.Items is null ? NotFound() : Ok(result.Items.Select(Map).ToArray());
    }

    [HttpPost, HasPermission(Permissions.ClientCoverage.Create)]
    public async Task<ActionResult<ClientCoverageResponse>> Create(Guid clientId,
        SaveClientCoverageRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateClientCoverageCommand(clientId, request.PayerId,
            request.ManualPayerName, request.MemberId, request.MedicaidId, request.EffectiveFrom,
            request.EffectiveTo, request.IsVerified, request.IsPrimary), ct);
        if (result.AuthorizationFailure is { } failure) return Auth(failure);
        return MapMutation(result, true);
    }

    [HttpPut("{coverageId:guid}"), HasPermission(Permissions.ClientCoverage.Edit)]
    public async Task<ActionResult<ClientCoverageResponse>> Update(Guid clientId, Guid coverageId,
        UpdateClientCoverageRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateClientCoverageCommand(clientId, coverageId,
            request.PayerId, request.ManualPayerName, request.MemberId, request.MedicaidId,
            request.EffectiveFrom, request.EffectiveTo, request.IsVerified, request.IsPrimary,
            request.EditorVersion), ct);
        if (result.AuthorizationFailure is { } failure) return Auth(failure);
        return MapMutation(result, false);
    }

    private ActionResult<ClientCoverageResponse> MapMutation(ClientCoverageResult result, bool created) =>
        result.Status switch
        {
            ClientCoverageMutationStatus.Succeeded when result.Item is { } item =>
                created ? StatusCode(StatusCodes.Status201Created, Map(item)) : Ok(Map(item)),
            ClientCoverageMutationStatus.ClientNotFound or ClientCoverageMutationStatus.NotFound => NotFound(),
            ClientCoverageMutationStatus.InvalidPayer => BadRequest(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["payerId"] = ["Select an active payer owned by the client's company."] })),
            ClientCoverageMutationStatus.DuplicatePrimary => Conflict(
                new ProblemDetails { Title = "This client already has a primary coverage." }),
            ClientCoverageMutationStatus.Conflict => Conflict(
                new ProblemDetails { Title = "The coverage was changed. Reload and try again." }),
            _ => Conflict(),
        };

    private ActionResult Auth(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();
    private static ClientCoverageResponse Map(ClientCoverageItem item) => new(item.Id, item.PayerId,
        item.PayerName, item.ManualPayerName, item.MemberId, item.MedicaidId, item.EffectiveFrom,
        item.EffectiveTo, item.IsVerified, item.IsPrimary, item.EditorVersion);
}

public record SaveClientCoverageRequest(Guid? PayerId, string? ManualPayerName, string? MemberId,
    string? MedicaidId, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, bool? IsVerified, bool? IsPrimary);
public sealed record UpdateClientCoverageRequest(Guid? PayerId, string? ManualPayerName,
    string? MemberId, string? MedicaidId, DateOnly? EffectiveFrom, DateOnly? EffectiveTo,
    bool? IsVerified, bool? IsPrimary, string? EditorVersion)
    : SaveClientCoverageRequest(PayerId, ManualPayerName, MemberId, MedicaidId, EffectiveFrom,
        EffectiveTo, IsVerified, IsPrimary);
public sealed record ClientCoverageResponse(Guid Id, Guid? PayerId, string PayerName,
    string? ManualPayerName, string? MemberId, string? MedicaidId, DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo, bool IsVerified, bool IsPrimary, string EditorVersion);
