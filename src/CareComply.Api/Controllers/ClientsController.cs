using CareComply.Api.Authorization;
using CareComply.Api.Contracts.Clients;
using CareComply.Api.Errors;
using CareComply.Api.Mapping;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Application.Features.Clients.Commands.ChangeClientStatus;
using CareComply.Application.Features.Clients.Commands.CompleteClientCreation;
using CareComply.Application.Features.Clients.Commands.CreateClient;
using CareComply.Application.Features.Clients.Commands.CreateClientBasicDetails;
using CareComply.Application.Features.Clients.Commands.UpdateClientBasicDetails;
using CareComply.Application.Features.Clients.Commands.UpdateClient;
using CareComply.Application.Features.Clients.Commands.UpdateClientAdditionalInformation;
using CareComply.Application.Features.Clients.Commands.UpdateClientContactInformation;
using CareComply.Application.Features.Clients.CreationProgress;
using CareComply.Application.Features.Clients.Queries.GetClient;
using CareComply.Application.Features.Clients.Queries.ListClients;
using CareComply.Application.Features.Clients.Queries.RevealClientSsn;
using CareComply.Application.Features.Clients.GuestInvitations;
using CareComply.Domain.Clients;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients")]
public sealed class ClientsController(
    ISender sender,
    IClientSupervisorCandidateReader supervisorCandidateReader) : ControllerBase
{
    [HttpPost("guest-invitations")]
    [HasPermission(Permissions.Client.GuestInvite)]
    public async Task<ActionResult<CreateClientGuestInvitationResponse>> CreateGuestInvitation(
        CreateClientGuestInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateClientGuestInvitationCommand(
            request.ClientName,
            request.Email,
            request.MobileNumber,
            request.CompanyId), cancellationToken);
        if (result.AuthorizationFailure is { } failure) return MapAuthorizationFailure(failure);

        return result.Creation?.Status switch
        {
            ClientGuestInvitationCreationStatus.Succeeded => Ok(new CreateClientGuestInvitationResponse(
                result.Creation.InvitationId!.Value,
                result.Creation.GuestLink!,
                result.Creation.ExpiresAtUtc!.Value)),
            ClientGuestInvitationCreationStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The selected company was not found or is unavailable.",
                type: ProblemTypes.ClientNotFound),
            ClientGuestInvitationCreationStatus.InvalidConfiguration => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Guest invitation generation is not configured.",
                type: ProblemTypes.ClientInvalidState),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "The invitation could not be created."),
        };
    }

    [HttpPost("guest-invitations/{invitationId:guid}/send")]
    [HasPermission(Permissions.Client.GuestInvite)]
    public async Task<ActionResult<SendClientGuestInvitationResponse>> SendGuestInvitation(
        Guid invitationId,
        SendClientGuestInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SendClientGuestInvitationCommand(invitationId, request.Token),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure) return MapAuthorizationFailure(failure);

        return result.Send?.Status switch
        {
            ClientGuestInvitationSendStatus.Succeeded => Ok(new SendClientGuestInvitationResponse(
                result.Send.InvitationId,
                result.Send.SentAtUtc!.Value,
                result.Send.ExpiresAtUtc!.Value)),
            ClientGuestInvitationSendStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The invitation was not found.",
                type: ProblemTypes.ClientNotFound),
            ClientGuestInvitationSendStatus.InvalidToken => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The invitation token is invalid.",
                type: ProblemTypes.ClientInvalidState),
            ClientGuestInvitationSendStatus.Unavailable => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The invitation is no longer available.",
                type: ProblemTypes.ClientConflict),
            ClientGuestInvitationSendStatus.DeliveryFailed => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The invitation email could not be delivered.",
                type: ProblemTypes.ClientConflict),
            ClientGuestInvitationSendStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The invitation changed while it was being sent.",
                type: ProblemTypes.ClientConflict),
            ClientGuestInvitationSendStatus.InvalidConfiguration => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Guest invitation delivery is not configured.",
                type: ProblemTypes.ClientInvalidState),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "The invitation could not be sent."),
        };
    }

    [HttpGet("supervisor-candidates")]
    [HasPermission(Permissions.Client.View)]
    public async Task<ActionResult<IReadOnlyList<ClientSupervisorCandidateResponse>>> SupervisorCandidates(
        [FromQuery] Guid locationId, CancellationToken cancellationToken)
    {
        var result = await supervisorCandidateReader.ListAsync(locationId, cancellationToken);
        return result.Status switch
        {
            ClientSupervisorCandidateReadStatus.Succeeded =>
                Ok(result.Candidates.Select(ClientApiMapper.ToResponse).ToArray()),
            ClientSupervisorCandidateReadStatus.Unauthenticated => Unauthorized(),
            _ => NotFound(),
        };
    }
    [HttpGet]
    [HasPermission(Permissions.Client.View)]
    public async Task<ActionResult<ClientListResponse>> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? location,
        [FromQuery] string? programType,
        [FromQuery] string? serviceCode,
        [FromQuery] bool? isOnboarding,
        [FromQuery] string? sortBy = "name",
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        ClientStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseEnum(status, out ClientStatus value))
            {
                return InvalidEnum(nameof(status), status, typeof(ClientStatus));
            }

            parsedStatus = value;
        }

        ClientProgramType? parsedProgramType = null;
        if (!string.IsNullOrWhiteSpace(programType))
        {
            if (!TryParseEnum(programType, out ClientProgramType value))
            {
                return InvalidEnum(nameof(programType), programType, typeof(ClientProgramType));
            }

            parsedProgramType = value;
        }

        if (!TryParseSort(sortBy, out var sort))
        {
            return InvalidValue(nameof(sortBy), sortBy, "name, socDate, status, location, updated");
        }

        if (!TryParseSortDirection(sortDirection, out var sortDescending))
        {
            return InvalidValue(nameof(sortDirection), sortDirection, "asc, desc");
        }

        var result = await sender.Send(new ListClientsQuery(
            search,
            parsedStatus,
            location,
            parsedProgramType,
            serviceCode,
            isOnboarding,
            sort,
            sortDescending,
            pageNumber,
            pageSize), cancellationToken);

        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        var page = result.Page
            ?? throw new InvalidOperationException("The client directory result does not contain a page.");
        var totalPages = page.TotalCount == 0
            ? 0
            : (int)Math.Ceiling(page.TotalCount / (double)page.PageSize);

        return Ok(new ClientListResponse(
            page.Items.Select(ClientApiMapper.ToResponse).ToArray(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            totalPages));
    }

    [HttpGet("{clientId:guid}")]
    [HasPermission(Permissions.Client.View)]
    public async Task<ActionResult<ClientDetailsResponse>> Get(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetClientQuery(clientId), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return result.Client is { } client
            ? Ok(ClientApiMapper.ToResponse(client))
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The requested client was not found.",
                type: ProblemTypes.ClientNotFound);
    }

    [HttpGet("{clientId:guid}/ssn")]
    [HasPermission(Permissions.Client.RevealSsn)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ClientSsnResponse>> RevealSsn(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RevealClientSsnQuery(clientId), cancellationToken);
        if (result.AuthorizationFailure is { } failure) return MapAuthorizationFailure(failure);
        return result.Ssn is { } ssn
            ? Ok(new ClientSsnResponse(ssn))
            : NotFound();
    }

    [HttpPost]
    [HasPermission(Permissions.Client.Create)]
    public async Task<ActionResult<ClientDetailsResponse>> Create(
        CreateClientRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseEnum(request.Identity.Gender, out ClientGender gender))
        {
            return InvalidEnum("identity.gender", request.Identity.Gender, typeof(ClientGender));
        }

        if (!TryParseEnum(request.InitialStatus, out ClientStatus initialStatus))
        {
            return InvalidEnum("initialStatus", request.InitialStatus, typeof(ClientStatus));
        }

        if (!TryParseEnum(request.AdditionalProfile.ProgramType, out ClientProgramType programType))
        {
            return InvalidEnum(
                "additionalProfile.programType",
                request.AdditionalProfile.ProgramType,
                typeof(ClientProgramType));
        }

        var result = await sender.Send(new CreateClientCommand(
            ClientApiMapper.ToInput(request.Identity, gender),
            ClientApiMapper.ToInput(request.Contact),
            ClientApiMapper.ToInput(request.CareSetup),
            ClientApiMapper.ToInput(request.Safety),
            ClientApiMapper.ToInput(request.AdditionalProfile, programType),
            initialStatus,
            request.DischargeDate,
            request.DischargeReason,
            ClientApiMapper.ToInputs(request.Services),
            ClientApiMapper.ToInputs(request.Languages)), cancellationToken);

        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        var mutation = result.Administration;
        return mutation?.Status switch
        {
            ClientAdministrationStatus.Succeeded when mutation.Client is { } client =>
                CreatedAtAction(nameof(Get), new { clientId = client.Id }, ClientApiMapper.ToResponse(client)),
            ClientAdministrationStatus.DuplicatePmi => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "That PMI number is already assigned to another client.",
                type: ProblemTypes.ClientDuplicatePmi),
            ClientAdministrationStatus.InvalidState => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The client record contains an invalid business state.",
                type: ProblemTypes.ClientInvalidState),
            ClientAdministrationStatus.ServiceHistoryConflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The selected services conflict with existing client service history.",
                type: ProblemTypes.ClientServiceHistoryConflict),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The client could not be created.",
                type: ProblemTypes.ClientConflict),
        };
    }

    [HttpGet("{clientId:guid}/creation-state")]
    [HasPermission(Permissions.Client.Edit)]
    public async Task<ActionResult<ClientCreationStateResponse>> GetCreationState(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var operation = await sender.Send(
            new GetClientCreationStateQuery(clientId),
            cancellationToken);
        if (operation.AuthorizationFailure is { } failure)
            return MapAuthorizationFailure(failure);

        return operation.Result?.Status switch
        {
            ClientAdministrationStatus.Succeeded when operation.Result.State is { } state =>
                Ok(ToCreationStateResponse(state)),
            ClientAdministrationStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The requested client was not found.",
                type: ProblemTypes.ClientNotFound),
            ClientAdministrationStatus.InvalidState => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Only a pending client can resume creation.",
                type: ProblemTypes.ClientConflict),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Client creation could not be resumed.",
                type: ProblemTypes.ClientConflict),
        };
    }

    [HttpPost("{clientId:guid}/creation-progress")]
    [HasPermission(Permissions.Client.Edit)]
    public async Task<ActionResult<ClientCreationProgressResponse>> AdvanceCreationProgress(
        Guid clientId,
        AdvanceClientCreationProgressRequest request,
        CancellationToken cancellationToken)
    {
        var operation = await sender.Send(
            new AdvanceClientCreationProgressCommand(clientId, request.Step, request.EditorVersion),
            cancellationToken);
        if (operation.AuthorizationFailure is { } failure)
            return MapAuthorizationFailure(failure);

        return operation.Result?.Status switch
        {
            ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges
                when operation.Result.EditorVersion is { } editorVersion =>
                Ok(new ClientCreationProgressResponse(
                    operation.Result.ClientId,
                    operation.Result.CurrentStep,
                    editorVersion)),
            ClientAdministrationStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The requested client was not found.",
                type: ProblemTypes.ClientNotFound),
            ClientAdministrationStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The client has changed since it was loaded. Refresh the record and try again.",
                type: ProblemTypes.ClientConflict),
            ClientAdministrationStatus.InvalidState => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The requested creation progress is not valid.",
                type: ProblemTypes.ClientConflict),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Client creation progress could not be saved.",
                type: ProblemTypes.ClientConflict),
        };
    }

    [HttpPost("basic-details")]
    [HasPermission(Permissions.Client.Create)]
    public async Task<ActionResult<ClientDetailsResponse>> CreateBasicDetails(
        CreateClientBasicDetailsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseEnum(request.Identity.Gender, out ClientGender gender))
            return InvalidEnum("identity.gender", request.Identity.Gender, typeof(ClientGender));

        var result = await sender.Send(new CreateClientBasicDetailsCommand(
            ClientApiMapper.ToInput(request.Identity, gender),
            ClientApiMapper.ToInput(request.CareSetup),
            request.InsuranceProvider,
            ClientApiMapper.ToInputs(request.Services)), cancellationToken);

        if (result.AuthorizationFailure is { } failure) return MapAuthorizationFailure(failure);

        var mutation = result.Administration;
        return mutation?.Status switch
        {
            ClientAdministrationStatus.Succeeded when mutation.Client is { } client =>
                CreatedAtAction(nameof(Get), new { clientId = client.Id }, ClientApiMapper.ToResponse(client)),
            ClientAdministrationStatus.DuplicatePmi => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "That PMI number is already assigned to another client.",
                type: ProblemTypes.ClientDuplicatePmi),
            ClientAdministrationStatus.InvalidState => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The client record contains an invalid business state.",
                type: ProblemTypes.ClientInvalidState),
            ClientAdministrationStatus.ServiceHistoryConflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The selected services conflict with existing client service history.",
                type: ProblemTypes.ClientServiceHistoryConflict),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The client could not be created.",
                type: ProblemTypes.ClientConflict),
        };
    }

    [HttpPut("{clientId:guid}/basic-details")]
    [HasPermission(Permissions.Client.Edit)]
    public async Task<ActionResult<ClientMutationResponse>> UpdateBasicDetails(
        Guid clientId,
        UpdateClientBasicDetailsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseEnum(request.Identity.Gender, out ClientGender gender))
            return InvalidEnum("identity.gender", request.Identity.Gender, typeof(ClientGender));

        var result = await sender.Send(new UpdateClientBasicDetailsCommand(
            clientId,
            ClientApiMapper.ToInput(request.Identity, gender),
            ClientApiMapper.ToInput(request.CareSetup),
            request.InsuranceProvider,
            ClientApiMapper.ToInputs(request.Services),
            request.EditorVersion), cancellationToken);

        if (result.AuthorizationFailure is { } failure) return MapAuthorizationFailure(failure);

        var mutation = result.Administration;
        return mutation?.Status switch
        {
            ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges
                when mutation.Client is { } client =>
                Ok(new ClientMutationResponse(client.Id, client.EditorVersion)),
            ClientAdministrationStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The requested client was not found.",
                type: ProblemTypes.ClientNotFound),
            ClientAdministrationStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The client has changed since it was loaded. Refresh the record and try again.",
                type: ProblemTypes.ClientConflict),
            ClientAdministrationStatus.DuplicatePmi => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "That PMI number is already assigned to another client.",
                type: ProblemTypes.ClientDuplicatePmi),
            ClientAdministrationStatus.InvalidState => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Only a pending client can be updated through the creation workflow.",
                type: ProblemTypes.ClientInvalidState),
            ClientAdministrationStatus.InvalidServiceChange => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The selected service changes are not valid.",
                type: ProblemTypes.ClientServiceHistoryConflict),
            ClientAdministrationStatus.ServiceHistoryConflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The selected services conflict with existing client service history.",
                type: ProblemTypes.ClientServiceHistoryConflict),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The client Basic details could not be updated.",
                type: ProblemTypes.ClientConflict),
        };
    }

    [HttpPut("{clientId:guid}")]
    [HasPermission(Permissions.Client.Edit)]
    public async Task<ActionResult<ClientDetailsResponse>> Update(
        Guid clientId,
        UpdateClientRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseEnum(request.Identity.Gender, out ClientGender gender))
        {
            return InvalidEnum("identity.gender", request.Identity.Gender, typeof(ClientGender));
        }

        if (!TryParseEnum(request.AdditionalProfile.ProgramType, out ClientProgramType programType))
        {
            return InvalidEnum(
                "additionalProfile.programType",
                request.AdditionalProfile.ProgramType,
                typeof(ClientProgramType));
        }

        var result = await sender.Send(new UpdateClientCommand(
            clientId,
            ClientApiMapper.ToInput(request.Identity, gender),
            ClientApiMapper.ToInput(request.Contact),
            ClientApiMapper.ToInput(request.CareSetup),
            ClientApiMapper.ToInput(request.Safety),
            ClientApiMapper.ToInput(request.AdditionalProfile, programType),
            ClientApiMapper.ToInputs(request.Services),
            ClientApiMapper.ToInputs(request.Languages),
            request.ServiceChangeEffectiveDate,
            request.EditorVersion), cancellationToken);

        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return MapMutation(result.Administration);
    }

    [HttpPut("{clientId:guid}/additional-information")]
    [HasPermission(Permissions.Client.Edit)]
    public async Task<ActionResult<ClientMutationResponse>> UpdateAdditionalInformation(
        Guid clientId,
        UpdateClientAdditionalInformationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseEnum(request.AdditionalProfile.ProgramType, out ClientProgramType programType))
        {
            return InvalidEnum(
                "additionalProfile.programType",
                request.AdditionalProfile.ProgramType,
                typeof(ClientProgramType));
        }

        var result = await sender.Send(new UpdateClientAdditionalInformationCommand(
            clientId,
            ClientApiMapper.ToInput(request.AdditionalProfile, programType),
            request.EditorVersion), cancellationToken);
        if (result.AuthorizationFailure is { } failure) return MapAuthorizationFailure(failure);

        return result.Administration?.Status switch
        {
            ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges
                when result.Administration.Client is { } client =>
                    Ok(new ClientMutationResponse(client.Id, client.EditorVersion)),
            ClientAdministrationStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The requested client was not found.",
                type: ProblemTypes.ClientNotFound),
            ClientAdministrationStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The client has changed since it was loaded. Refresh the record and try again.",
                type: ProblemTypes.ClientConflict),
            ClientAdministrationStatus.InvalidState => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The requested change would place the client in an invalid business state.",
                type: ProblemTypes.ClientInvalidState),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The client could not be changed.",
                type: ProblemTypes.ClientConflict),
        };
    }

    [HttpPut("{clientId:guid}/contact-information")]
    [HasPermission(Permissions.Client.Edit)]
    public async Task<ActionResult<ClientMutationResponse>> UpdateContactInformation(
        Guid clientId,
        UpdateClientContactInformationRequest request,
        CancellationToken cancellationToken)
    {
        var contact = new ClientContactInput(
            null,
            request.Email,
            request.PrimaryPhone,
            request.SecondaryPhone,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.State,
            request.PostalCode,
            request.County,
            request.Country);
        var result = await sender.Send(new UpdateClientContactInformationCommand(
            clientId,
            contact,
            ClientApiMapper.ToInputs(request.PreferredLanguages),
            request.EditorVersion), cancellationToken);
        if (result.AuthorizationFailure is { } failure) return MapAuthorizationFailure(failure);

        return result.Administration?.Status switch
        {
            ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges
                when result.Administration.Client is { } client =>
                    Ok(new ClientMutationResponse(client.Id, client.EditorVersion)),
            ClientAdministrationStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The requested client was not found.",
                type: ProblemTypes.ClientNotFound),
            ClientAdministrationStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The client has changed since it was loaded. Refresh the record and try again.",
                type: ProblemTypes.ClientConflict),
            ClientAdministrationStatus.InvalidState => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The requested change would place the client in an invalid business state.",
                type: ProblemTypes.ClientInvalidState),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The client could not be changed.",
                type: ProblemTypes.ClientConflict),
        };
    }

    [HttpPut("{clientId:guid}/status")]
    [HasPermission(Permissions.Client.Edit)]
    public async Task<ActionResult<ClientDetailsResponse>> ChangeStatus(
        Guid clientId,
        ChangeClientStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseEnum(request.Status, out ClientStatus status))
        {
            return InvalidEnum(nameof(request.Status), request.Status, typeof(ClientStatus));
        }

        var result = await sender.Send(new ChangeClientStatusCommand(
            clientId,
            status,
            request.DischargeDate,
            request.DischargeReason,
            request.EditorVersion), cancellationToken);

        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return MapMutation(result.Administration);
    }

    [HttpPost("{clientId:guid}/complete-creation")]
    [HasPermission(Permissions.Client.Edit)]
    public async Task<ActionResult<CompleteClientCreationResponse>> CompleteCreation(
        Guid clientId,
        CompleteClientCreationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CompleteClientCreationCommand(clientId, request.EditorVersion),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return result.Administration?.Status switch
        {
            ClientAdministrationStatus.Succeeded when result.Administration.Client is { } client =>
                Ok(new CompleteClientCreationResponse(
                    client.Id,
                    client.Status.ToString(),
                    client.EditorVersion)),
            ClientAdministrationStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The requested client was not found.",
                type: ProblemTypes.ClientNotFound),
            ClientAdministrationStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The client has changed since it was loaded. Refresh the record and try again.",
                type: ProblemTypes.ClientConflict),
            ClientAdministrationStatus.InvalidState => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Only a pending client can complete creation.",
                type: ProblemTypes.ClientConflict),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Client creation could not be completed.",
                type: ProblemTypes.ClientConflict),
        };
    }

    private ActionResult<ClientDetailsResponse> MapMutation(ClientAdministrationResult? mutation) =>
        mutation?.Status switch
        {
            ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges
                when mutation.Client is { } client => Ok(ClientApiMapper.ToResponse(client)),
            ClientAdministrationStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The requested client was not found.",
                type: ProblemTypes.ClientNotFound),
            ClientAdministrationStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The client has changed since it was loaded. Refresh the record and try again.",
                type: ProblemTypes.ClientConflict),
            ClientAdministrationStatus.DuplicatePmi => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "That PMI number is already assigned to another client.",
                type: ProblemTypes.ClientDuplicatePmi),
            ClientAdministrationStatus.InvalidServiceChange => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The requested service change is invalid.",
                type: ProblemTypes.ClientInvalidServiceChange),
            ClientAdministrationStatus.ServiceHistoryConflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The selected services conflict with existing client service history.",
                type: ProblemTypes.ClientServiceHistoryConflict),
            ClientAdministrationStatus.InvalidState => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The requested change would place the client in an invalid business state.",
                type: ProblemTypes.ClientInvalidState),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The client could not be changed.",
                type: ProblemTypes.ClientConflict),
        };

    private static ClientCreationStateResponse ToCreationStateResponse(ClientCreationState state)
    {
        var client = ClientApiMapper.ToResponse(state.Client);
        return new(
            client.Id,
            client.CareSetup.Status,
            state.CurrentStep,
            client.EditorVersion,
            client.Identity,
            client.Contact,
            client.CareSetup,
            client.AdditionalProfile,
            client.Services,
            client.Languages);
    }

    private ActionResult MapAuthorizationFailure(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated
            ? Unauthorized()
            : Forbid();

    private BadRequestObjectResult InvalidEnum(string field, string? value, Type enumType) =>
        InvalidValue(
            field,
            value,
            string.Join(", ", Enum.GetNames(enumType)));

    private BadRequestObjectResult InvalidValue(string field, string? value, string allowedValues)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [field] = [$"'{value ?? string.Empty}' is invalid. Allowed values: {allowedValues}."],
        };
        return BadRequest(new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Type = ProblemTypes.Validation,
        });
    }

    private static bool TryParseEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

    private static bool TryParseSort(string? value, out ClientDirectorySort sort)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case null:
            case "":
            case "NAME":
                sort = ClientDirectorySort.Name;
                return true;
            case "SOCDATE":
            case "SOC":
                sort = ClientDirectorySort.SocDate;
                return true;
            case "STATUS":
                sort = ClientDirectorySort.Status;
                return true;
            case "LOCATION":
                sort = ClientDirectorySort.Location;
                return true;
            case "UPDATED":
            case "UPDATEDAT":
                sort = ClientDirectorySort.Updated;
                return true;
            default:
                sort = default;
                return false;
        }
    }

    private static bool TryParseSortDirection(string? value, out bool descending)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case null:
            case "":
            case "ASC":
                descending = false;
                return true;
            case "DESC":
                descending = true;
                return true;
            default:
                descending = false;
                return false;
        }
    }

}
