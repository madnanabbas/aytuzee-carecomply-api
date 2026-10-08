using CareComply.Api.Authorization;
using CareComply.Api.Contracts.Clients;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientServiceAgreements;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/service-agreements")]
public sealed class ClientServiceAgreementsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ClientServiceAgreement.View)]
    public async Task<ActionResult<IReadOnlyList<ClientServiceAgreementResponse>>> List(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ListClientServiceAgreementsQuery(clientId),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return result.Items is null
            ? ClientNotFound()
            : Ok(result.Items.Select(Map).ToArray());
    }

    [HttpGet("{agreementId:guid}")]
    [HasPermission(Permissions.ClientServiceAgreement.View)]
    public async Task<ActionResult<ClientServiceAgreementResponse>> Get(
        Guid clientId,
        Guid agreementId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetClientServiceAgreementQuery(clientId, agreementId),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return result.Item is null
            ? AgreementNotFound()
            : Ok(Map(result.Item));
    }

    [HttpPost]
    [HasPermission(Permissions.ClientServiceAgreement.Create)]
    public async Task<ActionResult<ClientServiceAgreementResponse>> Create(
        Guid clientId,
        CreateClientServiceAgreementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateClientServiceAgreementCommand(
                clientId,
                request.AgreementNumber,
                request.PayerName,
                request.InsuredMemberId,
                request.PriorAuthorizationNumber,
                request.StartDate,
                request.EndDate,
                request.Status,
                request.Notes,
                request.Lines?.Select(Map).ToArray()),
            cancellationToken);
        return MapMutation(result, created: true);
    }

    [HttpPut("{agreementId:guid}")]
    [HasPermission(Permissions.ClientServiceAgreement.Edit)]
    public async Task<ActionResult<ClientServiceAgreementResponse>> Update(
        Guid clientId,
        Guid agreementId,
        UpdateClientServiceAgreementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateClientServiceAgreementCommand(
                clientId,
                agreementId,
                request.AgreementNumber,
                request.PayerName,
                request.InsuredMemberId,
                request.PriorAuthorizationNumber,
                request.StartDate,
                request.EndDate,
                request.Status,
                request.Notes,
                request.EditorVersion,
                request.Lines?.Select(Map).ToArray()),
            cancellationToken);
        return MapMutation(result, created: false);
    }

    private ActionResult<ClientServiceAgreementResponse> MapMutation(
        ClientServiceAgreementResult result,
        bool created)
    {
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return result.Status switch
        {
            ClientServiceAgreementMutationStatus.Succeeded when result.Item is { } item =>
                created
                    ? StatusCode(StatusCodes.Status201Created, Map(item))
                    : Ok(Map(item)),
            ClientServiceAgreementMutationStatus.ClientNotFound => ClientNotFound(),
            ClientServiceAgreementMutationStatus.NotFound => AgreementNotFound(),
            ClientServiceAgreementMutationStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The service agreement has changed since it was loaded. Refresh it and try again.",
                type: ProblemTypes.ClientServiceAgreementConflict),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The service agreement could not be changed.",
                type: ProblemTypes.ClientServiceAgreementConflict),
        };
    }

    private ObjectResult ClientNotFound() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "The requested client was not found.",
        type: ProblemTypes.ClientNotFound);

    private ObjectResult AgreementNotFound() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "The requested service agreement was not found.",
        type: ProblemTypes.ClientServiceAgreementNotFound);

    private ActionResult MapAuthorizationFailure(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated
            ? Unauthorized()
            : Forbid();

    private static ClientServiceAgreementLineInput Map(
        CreateClientServiceAgreementLineRequest line) => new(
        null,
        line.ServiceCode,
        line.ServiceName,
        line.UnitType,
        line.MinutesPerUnit,
        line.AuthorizedUnits,
        line.RatePerUnit,
        line.PlannedHoursPerVisit,
        line.PlannedVisitsPerWeek,
        line.PrimaryDiagnosisText,
        line.IcdCode);

    private static ClientServiceAgreementLineInput Map(
        UpdateClientServiceAgreementLineRequest line) => new(
        line.Id,
        line.ServiceCode,
        line.ServiceName,
        line.UnitType,
        line.MinutesPerUnit,
        line.AuthorizedUnits,
        line.RatePerUnit,
        line.PlannedHoursPerVisit,
        line.PlannedVisitsPerWeek,
        line.PrimaryDiagnosisText,
        line.IcdCode);

    private static ClientServiceAgreementResponse Map(ClientServiceAgreementItem agreement) => new(
        agreement.Id,
        agreement.AgreementNumber,
        agreement.PayerName,
        agreement.InsuredMemberId,
        agreement.PriorAuthorizationNumber,
        agreement.StartDate,
        agreement.EndDate,
        agreement.Status,
        agreement.Notes,
        agreement.EditorVersion,
        agreement.Lines.Select(Map).ToArray());

    private static ClientServiceAgreementLineResponse Map(ClientServiceAgreementLineItem line) => new(
        line.Id,
        line.ServiceCode,
        line.ServiceName,
        line.UnitType,
        line.MinutesPerUnit,
        line.AuthorizedUnits,
        line.RatePerUnit,
        line.PlannedHoursPerVisit,
        line.PlannedVisitsPerWeek,
        line.PrimaryDiagnosisText,
        line.IcdCode,
        line.EstimatedBudget,
        line.PlannedHoursPerWeek,
        line.PlannedUnitsPerWeek);
}
