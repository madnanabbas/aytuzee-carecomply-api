using CareComply.Api.Authorization;
using CareComply.Api.Contracts.Clients;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Application.Authorization;
using CareComply.Application.Features.Clients.Commands.ChangeClientStatus;
using CareComply.Application.Features.Clients.Commands.CreateClient;
using CareComply.Application.Features.Clients.Commands.UpdateClient;
using CareComply.Application.Features.Clients.Queries.GetClient;
using CareComply.Application.Features.Clients.Queries.ListClients;
using CareComply.Application.Features.Clients.Queries.RevealClientSsn;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/clients")]
public sealed class ClientsController(ISender sender, CareComplyDbContext db, ITenantScopeAccessor tenants) : ControllerBase
{
    [HttpGet("supervisor-candidates")]
    [HasPermission(Permissions.Client.View)]
    public async Task<ActionResult<IReadOnlyList<ClientSupervisorCandidateResponse>>> SupervisorCandidates(
        [FromQuery] Guid locationId, CancellationToken cancellationToken)
    {
        var tenant = await tenants.GetAsync(cancellationToken);
        if (tenant is null) return Unauthorized();
        var location = await db.CompanyLocations.AsNoTracking().Where(x => x.Id == locationId && x.IsActive)
            .Select(x => new { x.CompanyId }).SingleOrDefaultAsync(cancellationToken);
        if (location is null || (!tenant.IsPlatformAdministrator &&
            (tenant.CompanyId != location.CompanyId || !await db.UserLocations.AnyAsync(x => x.UserId == tenant.UserId && x.LocationId == locationId, cancellationToken))))
            return NotFound();

        var employees = await db.Users.AsNoTracking().Where(user => user.CompanyId == location.CompanyId && user.IsEmployee && user.IsActive &&
            db.UserLocations.Any(access => access.UserId == user.Id && access.LocationId == locationId))
            .OrderBy(user => user.LastName).ThenBy(user => user.FirstName)
            .Select(user => new ClientSupervisorCandidateResponse(user.Id, user.FirstName + " " + user.LastName, user.JobTitle))
            .ToArrayAsync(cancellationToken);
        return Ok(employees);
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
            page.Items.Select(MapListItem).ToArray(),
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
            ? Ok(MapClient(client))
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
            MapIdentity(request.Identity, gender),
            MapContact(request.Contact),
            MapCareSetup(request.CareSetup),
            MapSafety(request.Safety),
            MapAdditionalProfile(request.AdditionalProfile, programType),
            initialStatus,
            request.DischargeDate,
            request.DischargeReason,
            MapServices(request.Services),
            MapLanguages(request.Languages)), cancellationToken);

        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        var mutation = result.Administration;
        return mutation?.Status switch
        {
            ClientAdministrationStatus.Succeeded when mutation.Client is { } client =>
                CreatedAtAction(nameof(Get), new { clientId = client.Id }, MapClient(client)),
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
            MapIdentity(request.Identity, gender),
            MapContact(request.Contact),
            MapCareSetup(request.CareSetup),
            MapSafety(request.Safety),
            MapAdditionalProfile(request.AdditionalProfile, programType),
            MapServices(request.Services),
            MapLanguages(request.Languages),
            request.ServiceChangeEffectiveDate,
            request.EditorVersion), cancellationToken);

        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return MapMutation(result.Administration);
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

    private ActionResult<ClientDetailsResponse> MapMutation(ClientAdministrationResult? mutation) =>
        mutation?.Status switch
        {
            ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges
                when mutation.Client is { } client => Ok(MapClient(client)),
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

    private static ClientIdentityInput MapIdentity(
        ClientIdentityRequest request,
        ClientGender gender) => new(
        request.FirstName,
        request.MiddleName,
        request.LastName,
        request.PreferredName,
        request.DateOfBirth,
        gender,
        request.PmiNumber,
        request.Ssn);

    private static ClientContactInput MapContact(ClientContactRequest request) => new(
        request.InsuranceProvider,
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

    private static ClientCareSetupInput MapCareSetup(ClientCareSetupRequest request) => new(
        request.IntakeDate,
        request.SocDate,
        request.ReferralSource,
        request.Location,
        request.IsOnboarding,
        request.EvvAuthorizedHoursPerWeek,
        request.LocationId,
        request.SupervisorUserId);

    private static ClientSafetyInput MapSafety(ClientSafetyRequest request) => new(
        request.SafetyMeasures,
        request.SpecialEquipment,
        request.Allergies,
        request.OtherServices);

    private static ClientAdditionalProfileInput MapAdditionalProfile(
        ClientAdditionalProfileRequest request,
        ClientProgramType programType) => new(
        programType,
        request.ReferralDetails,
        request.HomeCareRating,
        request.MedicalEquipment,
        request.PcaComplexity,
        request.Demographics,
        request.Smokes,
        request.IsVeteran,
        request.HasPets,
        request.LivingArrangement,
        request.PreferredClinic,
        request.PreferredHospital,
        request.PreferredAmbulance,
        request.CaregiverPreference,
        request.NutritionalRequirements,
        request.Diet,
        request.MealSchedule,
        request.Notes,
        request.AdditionalInformation);

    private static ClientServiceInput[] MapServices(
        IReadOnlyList<ClientServiceRequest>? services) =>
        services?.Select(service => new ClientServiceInput(service.Code, service.Name)).ToArray() ?? [];

    private static ClientLanguageInput[] MapLanguages(
        IReadOnlyList<ClientLanguageRequest>? languages) =>
        languages?.Select(language => new ClientLanguageInput(
            language.LanguageCode,
            language.LanguageName,
            language.IsPrimary)).ToArray() ?? [];

    private static ClientListItemResponse MapListItem(ClientDirectoryItem client) => new(
        client.Id,
        BuildDisplayName(client.FirstName, client.MiddleName, client.LastName),
        client.PreferredName,
        client.PmiNumber,
        client.DateOfBirth,
        client.Gender.ToString(),
        client.Status.ToString(),
        client.Location,
        client.SocDate,
        client.IsOnboarding,
        client.ProgramType.ToString(),
        client.Services.Select(MapService).ToArray(),
        client.UpdatedAtUtc);

    private static ClientDetailsResponse MapClient(ClientDirectoryDetails client) => new(
        client.Id,
        new(
            client.FirstName,
            client.MiddleName,
            client.LastName,
            client.PreferredName,
            client.DateOfBirth,
            client.Gender.ToString(),
            client.PmiNumber,
            client.HasSsn),
        new(
            client.InsuranceProvider,
            client.Email,
            client.PrimaryPhone,
            client.SecondaryPhone,
            client.AddressLine1,
            client.AddressLine2,
            client.City,
            client.State,
            client.PostalCode,
            client.County,
            client.Country),
        new(
            client.IntakeDate,
            client.SocDate,
            client.ReferralSource,
            client.Status.ToString(),
            client.Location,
            client.IsOnboarding,
            client.EvvAuthorizedHoursPerWeek,
            client.DischargeDate,
            client.DischargeReason,
            client.LocationId,
            client.SupervisorUserId),
        new(
            client.SafetyMeasures,
            client.SpecialEquipment,
            client.Allergies,
            client.OtherServices),
        new(
            client.AdditionalProfile.ProgramType.ToString(),
            client.AdditionalProfile.ReferralDetails,
            client.AdditionalProfile.HomeCareRating,
            client.AdditionalProfile.MedicalEquipment,
            client.AdditionalProfile.PcaComplexity,
            client.AdditionalProfile.Demographics,
            client.AdditionalProfile.Smokes,
            client.AdditionalProfile.IsVeteran,
            client.AdditionalProfile.HasPets,
            client.AdditionalProfile.LivingArrangement,
            client.AdditionalProfile.PreferredClinic,
            client.AdditionalProfile.PreferredHospital,
            client.AdditionalProfile.PreferredAmbulance,
            client.AdditionalProfile.CaregiverPreference,
            client.AdditionalProfile.NutritionalRequirements,
            client.AdditionalProfile.Diet,
            client.AdditionalProfile.MealSchedule,
            client.AdditionalProfile.Notes,
            client.AdditionalProfile.AdditionalInformation),
        client.Services.Select(MapService).ToArray(),
        client.Languages.Select(language => new ClientLanguageResponse(
            language.LanguageCode,
            language.LanguageName,
            language.IsPrimary)).ToArray(),
        client.CreatedAtUtc,
        client.UpdatedAtUtc,
        client.EditorVersion);

    private static ClientServiceResponse MapService(ClientDirectoryService service) => new(
        service.Id,
        service.Code,
        service.Name,
        service.StartDate,
        service.EndDate,
        service.IsActive);

    private static string BuildDisplayName(string firstName, string middleName, string lastName) =>
        string.IsNullOrWhiteSpace(middleName)
            ? $"{firstName} {lastName}"
            : $"{firstName} {middleName} {lastName}";
}
