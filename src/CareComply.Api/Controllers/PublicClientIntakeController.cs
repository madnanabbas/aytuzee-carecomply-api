using CareComply.Api.Authentication;
using CareComply.Api.Contracts.PublicClientIntake;
using CareComply.Api.Errors;
using CareComply.Api.Mapping;
using CareComply.Application.Features.Clients.GuestInvitations;
using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using Mediator;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CareComply.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/client-intake")]
public sealed class PublicClientIntakeController(
    ISender sender,
    IClientGuestSessionService guestSessions,
    IWebHostEnvironment environment) : ControllerBase
{
    public const string TokenHeaderName = "X-Client-Intake-Token";
    public const string SessionCookieName = "__Host-CareComply.GuestIntake";
    public const string CsrfCookieName = "CareComply.GuestIntake.XSRF";
    public const string CsrfHeaderName = "X-Client-Intake-XSRF";

    [HttpPost("redeem")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientGuestAccessResponse>> Redeem(
        [FromHeader(Name = TokenHeaderName)] string? token,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new PublicClientGuestAccessResponse("Invalid", null, null, false, 1));
        var result = await guestSessions.RedeemAsync(token, cancellationToken);
        if (result.State != ClientGuestAccessState.Ready
            || result.SessionToken is null || result.CsrfToken is null)
            return Ok(new PublicClientGuestAccessResponse(
                result.State.ToString(), null, null, false, 1));
        AppendGuestCookies(result.SessionToken, result.CsrfToken);
        return Ok(new PublicClientGuestAccessResponse(
            "Ready", result.ClientDisplayName, result.InvitationExpiresAtUtc,
            result.HasCreatedClient, result.CreationProgressStep));
    }

    [HttpGet("session")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientGuestAccessResponse>> Session(
        CancellationToken cancellationToken)
    {
        SetNoStore();
        if (!Request.Cookies.TryGetValue(SessionCookieName, out var token))
            return Ok(new PublicClientGuestAccessResponse("SessionExpired", null, null, false, 1));
        var result = await guestSessions.GetStateAsync(token, cancellationToken);
        if (result.State != ClientGuestAccessState.Ready) ClearGuestCookies();
        return Ok(new PublicClientGuestAccessResponse(
            result.State.ToString(), result.ClientDisplayName, result.InvitationExpiresAtUtc,
            result.HasCreatedClient, result.CreationProgressStep));
    }

    [HttpGet("invitation")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientInvitationResponse>> ResolveInvitation(
        [FromHeader(Name = TokenHeaderName)] string? token,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new PublicClientInvitationResponse("Invalid", null, null, false));
        }

        var result = await sender.Send(
            new ResolvePublicClientGuestInvitationQuery(token),
            cancellationToken);
        return Ok(new PublicClientInvitationResponse(
            result.State.ToString(),
            result.ClientDisplayName,
            result.ExpiresAtUtc,
            result.HasCreatedClient));
    }

    [HttpGet("locations")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeLocationsResponse>> Locations(
        [FromHeader(Name = TokenHeaderName)] string? token,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest();
        }
        var result = await sender.Send(new ListPublicClientIntakeLocationsQuery(token), cancellationToken);
        return Ok(new PublicClientIntakeLocationsResponse(
            result.State.ToString(),
            result.Locations.Select(value =>
                new PublicClientIntakeLocationResponse(value.Id, value.DisplayName)).ToArray()));
    }

    [HttpGet("supervisors")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeSupervisorsResponse>> Supervisors(
        [FromHeader(Name = TokenHeaderName)] string? token,
        [FromQuery] Guid locationId,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest();
        }
        var result = await sender.Send(
            new ListPublicClientIntakeSupervisorsQuery(token, locationId), cancellationToken);
        return Ok(new PublicClientIntakeSupervisorsResponse(
            result.State.ToString(),
            result.Supervisors.Select(value => new PublicClientIntakeSupervisorResponse(
                value.Id, value.DisplayName, value.JobTitle)).ToArray()));
    }

    [HttpPost("basic-details")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeContinuationResponse>> CreateBasicDetails(
        [FromHeader(Name = TokenHeaderName)] string? token,
        CreatePublicClientBasicDetailsRequest request,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest();
        }
        var normalizedGender = request.Identity.Gender.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!Enum.TryParse<ClientGender>(normalizedGender, true, out var gender) || gender == ClientGender.Unknown)
        {
            return BadRequest();
        }
        var result = await sender.Send(new CreatePublicClientBasicDetailsCommand(
            token,
            new ClientIdentityInput(
                request.Identity.FirstName, request.Identity.MiddleName, request.Identity.LastName,
                request.Identity.PreferredName, request.Identity.DateOfBirth, gender,
                request.Identity.PmiNumber, request.Identity.Ssn),
            new ClientCareSetupInput(
                request.CareSetup.IntakeDate, request.CareSetup.SocDate,
                request.CareSetup.ReferralSource, null, true,
                request.CareSetup.EvvAuthorizedHoursPerWeek, request.CareSetup.LocationId,
                request.CareSetup.SupervisorUserId),
            request.InsuranceProvider,
            request.Services?.Select(value => new ClientServiceInput(value.Code, value.Name)).ToArray() ?? []),
            cancellationToken);
        if ((result.Status is PublicClientIntakeCreationStatus.Created
                or PublicClientIntakeCreationStatus.AlreadyStarted)
            && result.NextStep is { } nextStep
            && result.EditorVersion is { } editorVersion)
        {
            return Ok(new PublicClientIntakeContinuationResponse(
                result.Status == PublicClientIntakeCreationStatus.Created ? "InProgress" : "AlreadyStarted",
                nextStep, editorVersion, true));
        }
        return result.Status switch
        {
            PublicClientIntakeCreationStatus.Expired => StatusCode(
                StatusCodes.Status410Gone,
                new PublicClientIntakeStateResponse("Expired")),
            PublicClientIntakeCreationStatus.Unavailable => Conflict(
                new PublicClientIntakeStateResponse("Unavailable")),
            PublicClientIntakeCreationStatus.Completed => Conflict(
                new PublicClientIntakeStateResponse("Completed")),
            PublicClientIntakeCreationStatus.DuplicatePmi => Conflict(
                new PublicClientIntakeStateResponse("Conflict")),
            PublicClientIntakeCreationStatus.Invalid or PublicClientIntakeCreationStatus.InvalidAssignment => BadRequest(),
            _ => Conflict(),
        };
    }

    [HttpGet("additional-information")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeAdditionalInformationResponse>> GetAdditionalInformation(
        [FromHeader(Name = TokenHeaderName)] string? token,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest();
        }

        var result = await sender.Send(
            new GetPublicClientAdditionalInformationQuery(token),
            cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created
            || result.AdditionalInformation is not { } profile
            || result.EditorVersion is not { } editorVersion
            || result.CreationProgressStep is not { } progress)
        {
            return MapPublicState(result.Status);
        }

        return Ok(new PublicClientIntakeAdditionalInformationResponse(
            ToResponse(profile),
            editorVersion,
            progress));
    }

    [HttpPut("additional-information")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeStepContinuationResponse>> UpdateAdditionalInformation(
        [FromHeader(Name = TokenHeaderName)] string? token,
        UpdatePublicClientIntakeAdditionalInformationRequest request,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)
            || !Enum.TryParse<ClientProgramType>(
                request.AdditionalInformation.ProgramType.Replace("245D", "Two45D", StringComparison.Ordinal),
                true,
                out var programType))
        {
            return BadRequest();
        }

        var result = await sender.Send(new UpdatePublicClientAdditionalInformationCommand(
            token,
            ClientApiMapper.ToInput(request.AdditionalInformation, programType),
            request.EditorVersion), cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created
            || result.EditorVersion is not { } editorVersion)
        {
            return MapPublicState(result.Status);
        }

        return Ok(new PublicClientIntakeStepContinuationResponse(editorVersion, 3));
    }

    private ObjectResult MapPublicState(PublicClientIntakeCreationStatus status) => status switch
    {
        PublicClientIntakeCreationStatus.Expired => StatusCode(
            StatusCodes.Status410Gone,
            new PublicClientIntakeStateResponse("Expired")),
        PublicClientIntakeCreationStatus.Unavailable => Conflict(
            new PublicClientIntakeStateResponse("Unavailable")),
        PublicClientIntakeCreationStatus.Completed => Conflict(
            new PublicClientIntakeStateResponse("Completed")),
        PublicClientIntakeCreationStatus.Conflict => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The client has changed since it was loaded. Refresh the intake and try again.",
            type: ProblemTypes.ClientConflict),
        PublicClientIntakeCreationStatus.InvalidAssignment => Conflict(
            new PublicClientIntakeStateResponse("Unavailable")),
        _ => BadRequest(new PublicClientIntakeStateResponse("Invalid")),
    };

    [HttpGet("contact-information")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeContactInformationResponse>> GetContactInformation(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new GetPublicClientContactInformationQuery(token), cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created || result.Contact is not { } contact
            || result.Languages is not { } languages || result.EditorVersion is not { } version
            || result.CreationProgressStep is not { } progress) return MapPublicState(result.Status);
        return Ok(new PublicClientIntakeContactInformationResponse(
            new(contact.Email, contact.PrimaryPhone, contact.SecondaryPhone, contact.AddressLine1,
                contact.AddressLine2, contact.City, contact.State, contact.PostalCode, contact.County,
                contact.Country, languages.Select(value => new CareComply.Api.Contracts.Clients.ClientLanguageRequest(
                    value.LanguageCode, value.LanguageName, value.IsPrimary)).ToArray()), version, progress));
    }

    [HttpPut("contact-information")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeStepContinuationResponse>> UpdateContactInformation(
        [FromHeader(Name = TokenHeaderName)] string? token,
        UpdatePublicClientIntakeContactInformationRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var value = request.ContactInformation;
        var result = await sender.Send(new UpdatePublicClientContactInformationCommand(token,
            new ClientContactInput(null, value.Email, value.PrimaryPhone, value.SecondaryPhone,
                value.AddressLine1, value.AddressLine2, value.City, value.State, value.PostalCode,
                value.County, value.Country),
            value.PreferredLanguages.Select(language => new ClientLanguageInput(
                language.LanguageCode, language.LanguageName, language.IsPrimary)).ToArray(),
            request.EditorVersion), cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created || result.EditorVersion is not { } version)
            return MapPublicState(result.Status);
        return Ok(new PublicClientIntakeStepContinuationResponse(version, 4));
    }

    [HttpGet("caregiver-candidates")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<IReadOnlyList<PublicClientCaregiverCandidateResponse>>> CaregiverCandidates(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new ListPublicClientCaregiverCandidatesQuery(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created && result.Candidates is { } candidates
            ? Ok(candidates.Select(value => new PublicClientCaregiverCandidateResponse(
                value.Id, value.DisplayName, value.JobTitle)).ToArray())
            : MapPublicState(result.Status);
    }

    [HttpGet("caregivers")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientCaregiverListResponse>> Caregivers(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new ListPublicClientCaregiversQuery(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created
            && result.Caregivers is { } caregivers && result.Services is { } services
            && result.CreationProgressStep is { } progress
            ? Ok(new PublicClientCaregiverListResponse(caregivers.Select(MapCaregiver).ToArray(), services, progress))
            : MapPublicState(result.Status);
    }

    [HttpPost("caregivers")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientCaregiverResponse>> CreateCaregiver(
        [FromHeader(Name = TokenHeaderName)] string? token, SavePublicClientCaregiverRequest request,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token) || !ParseCaregiverType(request.AssignmentType, out var type))
            return BadRequest();
        var result = await sender.Send(new CreatePublicClientCaregiverCommand(token,
            new(request.CaregiverUserId, type, request.ClientPreference, request.AssignedServices,
                request.StartDate, request.EndDate, request.Notes, request.Relationship,
                request.LivesWithClient)), cancellationToken);
        return MapCaregiverMutation(result, true);
    }

    [HttpPut("caregivers/{assignmentId:guid}")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientCaregiverResponse>> UpdateCaregiver(
        [FromHeader(Name = TokenHeaderName)] string? token, Guid assignmentId,
        SavePublicClientCaregiverRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token) || request.EditorVersion is null
            || !ParseCaregiverType(request.AssignmentType, out var type)) return BadRequest();
        var result = await sender.Send(new UpdatePublicClientCaregiverCommand(token, assignmentId,
            new(type, request.ClientPreference, request.AssignedServices, request.StartDate,
                request.EndDate, request.Notes, request.EditorVersion, request.Relationship,
                request.LivesWithClient)), cancellationToken);
        return MapCaregiverMutation(result, false);
    }

    [HttpPost("caregivers/{assignmentId:guid}/end")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientCaregiverResponse>> EndCaregiver(
        [FromHeader(Name = TokenHeaderName)] string? token, Guid assignmentId,
        EndPublicClientCaregiverRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        return MapCaregiverMutation(await sender.Send(new EndPublicClientCaregiverCommand(
            token, assignmentId, request.EndDate, request.EditorVersion), cancellationToken), false);
    }

    [HttpPost("caregivers/continue")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeStepContinuationResponse>> ContinueCaregivers(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new ContinuePublicClientCaregiversCommand(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created && result.EditorVersion is { } version
            ? Ok(new PublicClientIntakeStepContinuationResponse(version, 5))
            : MapPublicState(result.Status);
    }

    [HttpGet("contacts")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientContactListResponse>> Contacts(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new ListPublicClientContactsQuery(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created
            && result.Contacts is { } contacts && result.CreationProgressStep is { } progress
            ? Ok(new PublicClientContactListResponse(contacts.Select(MapContact).ToArray(), progress))
            : MapPublicState(result.Status);
    }

    [HttpPost("contacts")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientContactResponse>> CreateContact(
        [FromHeader(Name = TokenHeaderName)] string? token, SavePublicClientContactRequest request,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new CreatePublicClientContactCommand(token, request.Type,
            request.Name, request.Relationship, request.Phone, request.Email, request.Address,
            request.IsActive), cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created) return MapPublicState(result.Status);
        return result.Contact is { } contact ? StatusCode(201, MapContact(contact)) : NotFound();
    }

    [HttpPut("contacts/{contactId:guid}")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientContactResponse>> UpdateContact(
        [FromHeader(Name = TokenHeaderName)] string? token, Guid contactId,
        SavePublicClientContactRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new UpdatePublicClientContactCommand(token, contactId,
            request.Type, request.Name, request.Relationship, request.Phone, request.Email,
            request.Address, request.IsActive), cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created) return MapPublicState(result.Status);
        return result.Contact is { } contact ? Ok(MapContact(contact)) : NotFound();
    }

    [HttpPost("contacts/continue")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeStepContinuationResponse>> ContinueContacts(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new ContinuePublicClientContactsCommand(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created && result.EditorVersion is { } version
            ? Ok(new PublicClientIntakeStepContinuationResponse(version, 6))
            : MapPublicState(result.Status);
    }

    [HttpGet("care-coordinators")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientCareCoordinatorListResponse>> CareCoordinators(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new ListPublicClientCareCoordinatorsQuery(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created && result.Coordinators is { } rows
            && result.CreationProgressStep is { } progress
            ? Ok(new PublicClientCareCoordinatorListResponse(rows.Select(MapCoordinator).ToArray(), progress))
            : MapPublicState(result.Status);
    }

    [HttpPost("care-coordinators")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientCareCoordinatorResponse>> CreateCareCoordinator(
        [FromHeader(Name = TokenHeaderName)] string? token, SavePublicClientCareCoordinatorRequest request,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new CreatePublicClientCareCoordinatorCommand(token, request.Name,
            request.CompanyName, request.Phone, request.PhoneExtension, request.Fax, request.Email,
            request.IsActive), cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created) return MapPublicState(result.Status);
        return result.Coordinator is { } value ? StatusCode(201, MapCoordinator(value)) : NotFound();
    }

    [HttpPut("care-coordinators/{coordinatorId:guid}")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientCareCoordinatorResponse>> UpdateCareCoordinator(
        [FromHeader(Name = TokenHeaderName)] string? token, Guid coordinatorId,
        SavePublicClientCareCoordinatorRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new UpdatePublicClientCareCoordinatorCommand(token, coordinatorId,
            request.Name, request.CompanyName, request.Phone, request.PhoneExtension, request.Fax,
            request.Email, request.IsActive), cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created) return MapPublicState(result.Status);
        return result.Coordinator is { } value ? Ok(MapCoordinator(value)) : NotFound();
    }

    [HttpPost("care-coordinators/continue")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeStepContinuationResponse>> ContinueCareCoordinators(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new ContinuePublicClientCareCoordinatorsCommand(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created && result.EditorVersion is { } version
            ? Ok(new PublicClientIntakeStepContinuationResponse(version, 7)) : MapPublicState(result.Status);
    }

    [HttpGet("consultation-providers")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientConsultationProviderListResponse>> ConsultationProviders(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new ListPublicClientConsultationProvidersQuery(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created && result.Providers is { } rows
            && result.CreationProgressStep is { } progress
            ? Ok(new PublicClientConsultationProviderListResponse(rows.Select(MapProvider).ToArray(), progress))
            : MapPublicState(result.Status);
    }

    [HttpPost("consultation-providers")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientConsultationProviderResponse>> CreateConsultationProvider(
        [FromHeader(Name = TokenHeaderName)] string? token,
        SavePublicClientConsultationProviderRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new CreatePublicClientConsultationProviderCommand(token,
            request.Name, request.Phone, request.Email, request.SupportProviderName,
            request.SupportProviderPhone, request.SupportProviderAddress, request.IsActive), cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created) return MapPublicState(result.Status);
        return result.Provider is { } value ? StatusCode(201, MapProvider(value)) : NotFound();
    }

    [HttpPut("consultation-providers/{providerId:guid}")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientConsultationProviderResponse>> UpdateConsultationProvider(
        [FromHeader(Name = TokenHeaderName)] string? token, Guid providerId,
        SavePublicClientConsultationProviderRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new UpdatePublicClientConsultationProviderCommand(token, providerId,
            request.Name, request.Phone, request.Email, request.SupportProviderName,
            request.SupportProviderPhone, request.SupportProviderAddress, request.IsActive), cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created) return MapPublicState(result.Status);
        return result.Provider is { } value ? Ok(MapProvider(value)) : NotFound();
    }

    [HttpPost("consultation-providers/continue")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeStepContinuationResponse>> ContinueConsultationProviders(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(
            new ContinuePublicClientConsultationProvidersCommand(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created && result.EditorVersion is { } version
            ? Ok(new PublicClientIntakeStepContinuationResponse(version, 8)) : MapPublicState(result.Status);
    }

    [HttpGet("physicians")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientPhysicianListResponse>> Physicians(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new ListPublicClientPhysiciansQuery(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created && result.Physicians is { } rows
            && result.CreationProgressStep is { } progress
            ? Ok(new PublicClientPhysicianListResponse(rows.Select(MapPhysician).ToArray(), progress))
            : MapPublicState(result.Status);
    }

    [HttpPost("physicians")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientPhysicianResponse>> CreatePhysician(
        [FromHeader(Name = TokenHeaderName)] string? token,
        SavePublicClientPhysicianRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new CreatePublicClientPhysicianCommand(token, request.Name,
            request.Npi, request.Phone, request.Fax, request.Address, request.ZipCode,
            request.City, request.State, request.IsActive), cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created) return MapPublicState(result.Status);
        return result.Physician is { } value ? StatusCode(201, MapPhysician(value)) : NotFound();
    }

    [HttpPut("physicians/{physicianId:guid}")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientPhysicianResponse>> UpdatePhysician(
        [FromHeader(Name = TokenHeaderName)] string? token, Guid physicianId,
        SavePublicClientPhysicianRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new UpdatePublicClientPhysicianCommand(token, physicianId,
            request.Name, request.Npi, request.Phone, request.Fax, request.Address,
            request.ZipCode, request.City, request.State, request.IsActive), cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created) return MapPublicState(result.Status);
        return result.Physician is { } value ? Ok(MapPhysician(value)) : NotFound();
    }

    [HttpPost("physicians/continue")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeStepContinuationResponse>> ContinuePhysicians(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new ContinuePublicClientPhysiciansCommand(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created && result.EditorVersion is { } version
            ? Ok(new PublicClientIntakeStepContinuationResponse(version, 9)) : MapPublicState(result.Status);
    }

    [HttpGet("responsible-parties")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientResponsiblePartyListResponse>> ResponsibleParties(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new ListPublicClientResponsiblePartiesQuery(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created
            && result.ResponsibleParties is { } rows && result.CreationProgressStep is { } progress
            ? Ok(new PublicClientResponsiblePartyListResponse(
                rows.Select(MapResponsibleParty).ToArray(), progress))
            : MapPublicState(result.Status);
    }

    [HttpPost("responsible-parties")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientResponsiblePartyResponse>> CreateResponsibleParty(
        [FromHeader(Name = TokenHeaderName)] string? token,
        SavePublicClientResponsiblePartyRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new CreatePublicClientResponsiblePartyCommand(token,
            request.Name, request.Relationship, request.Phone, request.Email, request.Address,
            request.LivesWithClient, request.HasSpenddown, request.IsActive), cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created) return MapPublicState(result.Status);
        return result.ResponsibleParty is { } value
            ? StatusCode(201, MapResponsibleParty(value)) : NotFound();
    }

    [HttpPut("responsible-parties/{responsiblePartyId:guid}")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientResponsiblePartyResponse>> UpdateResponsibleParty(
        [FromHeader(Name = TokenHeaderName)] string? token, Guid responsiblePartyId,
        SavePublicClientResponsiblePartyRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new UpdatePublicClientResponsiblePartyCommand(token,
            responsiblePartyId, request.Name, request.Relationship, request.Phone, request.Email,
            request.Address, request.LivesWithClient, request.HasSpenddown, request.IsActive),
            cancellationToken);
        if (result.Status != PublicClientIntakeCreationStatus.Created) return MapPublicState(result.Status);
        return result.ResponsibleParty is { } value ? Ok(MapResponsibleParty(value)) : NotFound();
    }

    [HttpPost("responsible-parties/continue")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeStepContinuationResponse>> ContinueResponsibleParties(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(
            new ContinuePublicClientResponsiblePartiesCommand(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created && result.EditorVersion is { } version
            ? Ok(new PublicClientIntakeStepContinuationResponse(version, 10)) : MapPublicState(result.Status);
    }

    [HttpGet("hospitalizations")]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientHospitalizationListResponse>> Hospitalizations(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new ListPublicClientHospitalizationsQuery(token), cancellationToken);
        return result.Status == PublicClientIntakeCreationStatus.Created
            && result.Hospitalizations is { } rows && result.CreationProgressStep is { } progress
            ? Ok(new PublicClientHospitalizationListResponse(
                rows.Select(MapHospitalization).ToArray(), progress))
            : MapPublicState(result.Status);
    }

    [HttpPost("hospitalizations")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientHospitalizationResponse>> CreateHospitalization(
        [FromHeader(Name = TokenHeaderName)] string? token,
        SavePublicClientHospitalizationRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new CreatePublicClientHospitalizationCommand(token,
            request.HospitalName, request.AdmittedAtUtc, request.DischargedAtUtc,
            request.Reason, request.Diagnosis, request.DischargeInstructions), cancellationToken);
        return MapHospitalizationMutation(result, true);
    }

    [HttpPut("hospitalizations/{hospitalizationId:guid}")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientHospitalizationResponse>> UpdateHospitalization(
        [FromHeader(Name = TokenHeaderName)] string? token, Guid hospitalizationId,
        SavePublicClientHospitalizationRequest request, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new UpdatePublicClientHospitalizationCommand(token,
            hospitalizationId, request.HospitalName, request.AdmittedAtUtc,
            request.DischargedAtUtc, request.Reason, request.Diagnosis,
            request.DischargeInstructions, request.EditorVersion), cancellationToken);
        return MapHospitalizationMutation(result, false);
    }

    [HttpPost("complete")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicClientIntakeCompletionResponse>> Complete(
        [FromHeader(Name = TokenHeaderName)] string? token, CancellationToken cancellationToken)
    {
        SetNoStore();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        var result = await sender.Send(new CompletePublicClientIntakeCommand(token), cancellationToken);
        if ((result.Status is PublicClientIntakeCreationStatus.Created
            or PublicClientIntakeCreationStatus.Completed)
            && result.CompletedAtUtc is { } completedAtUtc)
        {
            await guestSessions.InvalidateAsync(token, completedAtUtc, cancellationToken);
            ClearGuestCookies();
            return Ok(new PublicClientIntakeCompletionResponse(
                "Completed", completedAtUtc, "Client intake completed successfully."));
        }
        return MapPublicState(result.Status);
    }

    private ActionResult<PublicClientCaregiverResponse> MapCaregiverMutation(
        PublicClientIntakeCaregiverMutationResult result, bool created)
    {
        if (result.Status != PublicClientIntakeCreationStatus.Created) return MapPublicState(result.Status);
        return result.Mutation?.Status switch
        {
            ClientCaregiverMutationStatus.Succeeded when result.Mutation.Assignment is { } assignment =>
                created ? StatusCode(201, MapCaregiver(assignment)) : Ok(MapCaregiver(assignment)),
            ClientCaregiverMutationStatus.InvalidCaregiver => Problem(statusCode: 400,
                title: "Select an active caregiver.", type: ProblemTypes.ClientCaregiverInvalid),
            ClientCaregiverMutationStatus.AssignmentNotFound => Problem(statusCode: 404,
                title: "The caregiver assignment was not found.", type: ProblemTypes.ClientCaregiverNotFound),
            ClientCaregiverMutationStatus.DuplicateActiveAssignment => Problem(statusCode: 409,
                title: "This caregiver already has an active assignment for the client.",
                type: ProblemTypes.ClientCaregiverDuplicate),
            ClientCaregiverMutationStatus.PrimaryConflict => Problem(statusCode: 409,
                title: "The client already has an active primary caregiver.",
                type: ProblemTypes.ClientCaregiverPrimaryConflict),
            _ => Problem(statusCode: 409, title: "The caregiver assignment changed. Refresh and try again.",
                type: ProblemTypes.ClientCaregiverConflict),
        };
    }

    private ActionResult<PublicClientHospitalizationResponse> MapHospitalizationMutation(
        PublicClientIntakeHospitalizationMutationResult result, bool created)
    {
        if (result.Status != PublicClientIntakeCreationStatus.Created)
            return MapPublicState(result.Status);
        return result.Mutation?.Status switch
        {
            HospitalizationMutationStatus.Succeeded when result.Mutation.Value is { } value =>
                created ? StatusCode(201, MapHospitalization(value)) : Ok(MapHospitalization(value)),
            HospitalizationMutationStatus.ClientNotFound or HospitalizationMutationStatus.NotFound => NotFound(),
            _ => Conflict(),
        };
    }

    private static PublicClientCaregiverResponse MapCaregiver(ClientCaregiverItem value) => new(
        value.Id, value.UserId, value.Name, value.Title, value.Type.ToString(),
        value.RelationshipToClient, value.LivesWithClient, value.Services, value.StartDate,
        value.EndDate, value.Status.ToString(), value.IsPreferred, value.Notes, value.EditorVersion);

    private static PublicClientContactResponse MapContact(ClientContactItem value) => new(
        value.Id, value.Type.ToString(), value.Name, value.Relationship, value.Phone,
        value.Email, value.Address, value.IsActive);
    private static PublicClientCareCoordinatorResponse MapCoordinator(CareCoordinatorItem value) => new(
        value.Id, value.Name, value.CompanyName, value.Phone, value.PhoneExtension,
        value.Fax, value.Email, value.IsActive);
    private static PublicClientConsultationProviderResponse MapProvider(ClinicalContactItem value) => new(
        value.Id, value.Name, value.Phone, value.Email, value.SecondaryName,
        value.SecondaryPhone, value.SecondaryAddress, value.IsActive);
    private static PublicClientPhysicianResponse MapPhysician(ClinicalContactItem value) => new(
        value.Id, value.Name, value.Npi, value.Phone, value.Fax, value.Address,
        value.PostalCode, value.City, value.State, value.IsActive);
    private static PublicClientResponsiblePartyResponse MapResponsibleParty(
        ResponsiblePartyItem value) => new(value.Id, value.Name, value.Relationship, value.Phone,
            value.Email, value.Address, value.LivesWithClient, value.HasSpenddown, value.IsActive);
    private static PublicClientHospitalizationResponse MapHospitalization(HospitalizationItem value) => new(
        value.Id, value.HospitalName, value.AdmittedAtUtc, value.DischargedAtUtc,
        value.Reason, value.Diagnosis, value.DischargeInstructions, value.EditorVersion);

    private static bool ParseCaregiverType(string? value, out ClientCaregiverType type) =>
        Enum.TryParse(value, true, out type) && Enum.IsDefined(type);

    private static CareComply.Api.Contracts.Clients.ClientAdditionalProfileRequest ToResponse(
        ClientAdditionalProfileInput profile) => new(
        profile.ProgramType.ToString(),
        profile.ReferralDetails,
        profile.HomeCareRating,
        profile.MedicalEquipment,
        profile.PcaComplexity,
        profile.Demographics,
        profile.Smokes,
        profile.IsVeteran,
        profile.HasPets,
        profile.LivingArrangement,
        profile.PreferredClinic,
        profile.PreferredHospital,
        profile.PreferredAmbulance,
        profile.CaregiverPreference,
        profile.NutritionalRequirements,
        profile.Diet,
        profile.MealSchedule,
        profile.Notes,
        profile.AdditionalInformation);

    private void SetNoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
    }

    private void AppendGuestCookies(string sessionToken, string csrfToken)
    {
        // __Host- cookies must always be Secure and use Path=/ with no Domain.
        // Staging hosts the SPA and API on different sites, so only that
        // environment requires SameSite=None. Production is same-site.
        var sameSite = environment.IsStaging() ? SameSiteMode.None : SameSiteMode.Strict;
        var csrfCookieDomain = environment.IsProduction() ? "aytuzee.com" : null;
        Response.Cookies.Append(SessionCookieName, sessionToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = sameSite,
            Path = "/",
            IsEssential = true,
        });
        Response.Cookies.Append(CsrfCookieName, csrfToken, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = sameSite,
            Path = "/",
            Domain = csrfCookieDomain,
            IsEssential = true,
        });
    }

    private void ClearGuestCookies()
    {
        Response.Cookies.Delete(SessionCookieName, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(CsrfCookieName, new CookieOptions
        {
            Path = "/",
            Domain = environment.IsProduction() ? "aytuzee.com" : null,
        });
    }
}
