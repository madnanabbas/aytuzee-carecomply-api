using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using CareComply.Application.Features.ClientContacts;
using CareComply.Application.Features.ClientCareCoordinators;
using CareComply.Application.Features.ClientClinicalContacts;
using CareComply.Application.Features.ClientResponsibleParties;
using CareComply.Application.Features.ClientHospitalizations;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.Clients.GuestInvitations;

public sealed record ListPublicClientIntakeLocationsQuery(string Token)
    : IQuery<PublicClientIntakeLocationsResult>;

public sealed class ListPublicClientIntakeLocationsQueryHandler(IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<ListPublicClientIntakeLocationsQuery, PublicClientIntakeLocationsResult>
{
    public ValueTask<PublicClientIntakeLocationsResult> Handle(
        ListPublicClientIntakeLocationsQuery query, CancellationToken cancellationToken) =>
        new(resolver.ListLocationsAsync(query.Token, cancellationToken));
}

public sealed record ListPublicClientIntakeSupervisorsQuery(string Token, Guid LocationId)
    : IQuery<PublicClientIntakeSupervisorsResult>;

public sealed class ListPublicClientIntakeSupervisorsQueryValidator
    : AbstractValidator<ListPublicClientIntakeSupervisorsQuery>
{
    public ListPublicClientIntakeSupervisorsQueryValidator() =>
        RuleFor(query => query.LocationId).NotEmpty();
}

public sealed class ListPublicClientIntakeSupervisorsQueryHandler(IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<ListPublicClientIntakeSupervisorsQuery, PublicClientIntakeSupervisorsResult>
{
    public ValueTask<PublicClientIntakeSupervisorsResult> Handle(
        ListPublicClientIntakeSupervisorsQuery query, CancellationToken cancellationToken) =>
        new(resolver.ListSupervisorsAsync(query.Token, query.LocationId, cancellationToken));
}

public sealed record CreatePublicClientBasicDetailsCommand(
    string Token,
    ClientIdentityInput Identity,
    ClientCareSetupInput CareSetup,
    string? InsuranceProvider,
    IReadOnlyList<ClientServiceInput> Services)
    : ICommand<PublicClientIntakeCreationResult>;

public sealed class CreatePublicClientBasicDetailsCommandValidator
    : AbstractValidator<CreatePublicClientBasicDetailsCommand>
{
    public CreatePublicClientBasicDetailsCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(command => command.Token).NotEmpty();
        RuleFor(command => command.Identity).NotNull()
            .SetValidator(new ClientIdentityInputValidator(timeProvider));
        RuleFor(command => command.CareSetup).NotNull()
            .SetValidator(new ClientCareSetupInputValidator());
        RuleFor(command => command.CareSetup.LocationId).NotEmpty();
        RuleFor(command => command.InsuranceProvider).MaximumLength(Client.MaximumInsuranceLength);
        RuleFor(command => command.Services).NotNull()
            .Must(services => services.Count <= ClientValidation.MaximumServices)
            .WithMessage($"No more than {ClientValidation.MaximumServices} active services may be selected.")
            .Must(ClientValidation.HasUniqueServiceCodes)
            .WithMessage("Duplicate service codes are not allowed.");
        RuleForEach(command => command.Services).SetValidator(new ClientServiceInputValidator());
    }
}

public sealed class CreatePublicClientBasicDetailsCommandHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<CreatePublicClientBasicDetailsCommand, PublicClientIntakeCreationResult>
{
    public ValueTask<PublicClientIntakeCreationResult> Handle(
        CreatePublicClientBasicDetailsCommand command, CancellationToken cancellationToken) =>
        new(resolver.CreateBasicDetailsAsync(
            command.Token,
            new ClientBasicDetailsCreateRequest(
                command.Identity,
                command.CareSetup with { IsOnboarding = true, Location = null },
                command.InsuranceProvider,
                command.Services),
            cancellationToken));
}

public sealed record GetPublicClientAdditionalInformationQuery(string Token)
    : IQuery<PublicClientIntakeAdditionalInformationResult>;

public sealed class GetPublicClientAdditionalInformationQueryHandler(
    IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<GetPublicClientAdditionalInformationQuery, PublicClientIntakeAdditionalInformationResult>
{
    public ValueTask<PublicClientIntakeAdditionalInformationResult> Handle(
        GetPublicClientAdditionalInformationQuery query,
        CancellationToken cancellationToken) =>
        new(resolver.GetAdditionalInformationAsync(query.Token, cancellationToken));
}

public sealed record UpdatePublicClientAdditionalInformationCommand(
    string Token,
    ClientAdditionalProfileInput AdditionalProfile,
    string EditorVersion)
    : ICommand<PublicClientIntakeAdditionalInformationResult>;

public sealed class UpdatePublicClientAdditionalInformationCommandValidator
    : AbstractValidator<UpdatePublicClientAdditionalInformationCommand>
{
    public UpdatePublicClientAdditionalInformationCommandValidator()
    {
        RuleFor(command => command.Token).NotEmpty();
        RuleFor(command => command.AdditionalProfile)
            .NotNull()
            .SetValidator(new ClientAdditionalProfileInputValidator());
        RuleFor(command => command.EditorVersion)
            .Must(ClientValidation.IsValidEditorVersion)
            .WithMessage("Editor version is invalid.");
    }
}

public sealed class UpdatePublicClientAdditionalInformationCommandHandler(
    IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<UpdatePublicClientAdditionalInformationCommand, PublicClientIntakeAdditionalInformationResult>
{
    public ValueTask<PublicClientIntakeAdditionalInformationResult> Handle(
        UpdatePublicClientAdditionalInformationCommand command,
        CancellationToken cancellationToken) =>
        new(resolver.UpdateAdditionalInformationAsync(
            command.Token,
            command.AdditionalProfile,
            command.EditorVersion,
            cancellationToken));
}

public sealed record GetPublicClientContactInformationQuery(string Token)
    : IQuery<PublicClientIntakeContactInformationResult>;

public sealed class GetPublicClientContactInformationQueryHandler(IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<GetPublicClientContactInformationQuery, PublicClientIntakeContactInformationResult>
{
    public ValueTask<PublicClientIntakeContactInformationResult> Handle(
        GetPublicClientContactInformationQuery query, CancellationToken cancellationToken) =>
        new(resolver.GetContactInformationAsync(query.Token, cancellationToken));
}

public sealed record UpdatePublicClientContactInformationCommand(
    string Token, ClientContactInput Contact, IReadOnlyList<ClientLanguageInput> Languages,
    string EditorVersion) : ICommand<PublicClientIntakeContactInformationResult>;

public sealed class UpdatePublicClientContactInformationCommandValidator
    : AbstractValidator<UpdatePublicClientContactInformationCommand>
{
    public UpdatePublicClientContactInformationCommandValidator()
    {
        RuleFor(command => command.Token).NotEmpty();
        RuleFor(command => command.Contact).NotNull().SetValidator(new ClientContactInputValidator());
        RuleFor(command => command.Languages).NotNull()
            .Must(ClientValidation.HasUniqueLanguageCodes).WithMessage("Duplicate language codes are not allowed.")
            .Must(ClientValidation.HasExactlyOnePrimaryLanguage)
            .WithMessage("Exactly one preferred language must be marked as primary.");
        RuleForEach(command => command.Languages).SetValidator(new ClientLanguageInputValidator());
        RuleFor(command => command.EditorVersion).Must(ClientValidation.IsValidEditorVersion)
            .WithMessage("Editor version is invalid.");
    }
}

public sealed class UpdatePublicClientContactInformationCommandHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<UpdatePublicClientContactInformationCommand, PublicClientIntakeContactInformationResult>
{
    public ValueTask<PublicClientIntakeContactInformationResult> Handle(
        UpdatePublicClientContactInformationCommand command, CancellationToken cancellationToken) =>
        new(resolver.UpdateContactInformationAsync(command.Token, command.Contact, command.Languages,
            command.EditorVersion, cancellationToken));
}

public sealed record ListPublicClientCaregiverCandidatesQuery(string Token)
    : IQuery<PublicClientIntakeCaregiverCandidatesResult>;
public sealed class ListPublicClientCaregiverCandidatesHandler(IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<ListPublicClientCaregiverCandidatesQuery, PublicClientIntakeCaregiverCandidatesResult>
{
    public ValueTask<PublicClientIntakeCaregiverCandidatesResult> Handle(
        ListPublicClientCaregiverCandidatesQuery query, CancellationToken cancellationToken) =>
        new(resolver.ListCaregiverCandidatesAsync(query.Token, cancellationToken));
}

public sealed record ListPublicClientCaregiversQuery(string Token)
    : IQuery<PublicClientIntakeCaregiversResult>;
public sealed class ListPublicClientCaregiversHandler(IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<ListPublicClientCaregiversQuery, PublicClientIntakeCaregiversResult>
{
    public ValueTask<PublicClientIntakeCaregiversResult> Handle(
        ListPublicClientCaregiversQuery query, CancellationToken cancellationToken) =>
        new(resolver.ListCaregiversAsync(query.Token, cancellationToken));
}

public sealed record CreatePublicClientCaregiverCommand(string Token, ClientCaregiverCreateInput Input)
    : ICommand<PublicClientIntakeCaregiverMutationResult>;
public sealed record UpdatePublicClientCaregiverCommand(
    string Token, Guid AssignmentId, ClientCaregiverUpdateInput Input)
    : ICommand<PublicClientIntakeCaregiverMutationResult>;
public sealed record EndPublicClientCaregiverCommand(
    string Token, Guid AssignmentId, DateOnly EndDate, string EditorVersion)
    : ICommand<PublicClientIntakeCaregiverMutationResult>;
public sealed record ContinuePublicClientCaregiversCommand(string Token)
    : ICommand<PublicClientIntakeCreationResult>;

public sealed class CreatePublicClientCaregiverValidator : AbstractValidator<CreatePublicClientCaregiverCommand>
{
    public CreatePublicClientCaregiverValidator() => RuleFor(value => value.Input)
        .SetValidator(new PublicCaregiverCreateInputValidator());
}
public sealed class UpdatePublicClientCaregiverValidator : AbstractValidator<UpdatePublicClientCaregiverCommand>
{
    public UpdatePublicClientCaregiverValidator()
    {
        RuleFor(value => value.AssignmentId).NotEmpty();
        RuleFor(value => value.Input).SetValidator(new PublicCaregiverUpdateInputValidator());
    }
}
public sealed class EndPublicClientCaregiverValidator : AbstractValidator<EndPublicClientCaregiverCommand>
{
    public EndPublicClientCaregiverValidator()
    {
        RuleFor(value => value.AssignmentId).NotEmpty();
        RuleFor(value => value.EndDate).NotEmpty();
        RuleFor(value => value.EditorVersion).NotEmpty().Length(64);
    }
}
internal sealed class PublicCaregiverCreateInputValidator : AbstractValidator<ClientCaregiverCreateInput>
{
    public PublicCaregiverCreateInputValidator()
    {
        RuleFor(value => value.CaregiverUserId).NotEmpty();
        RuleFor(value => value.Type).IsInEnum();
        RuleFor(value => value.StartDate).NotEmpty();
        RuleFor(value => value.EndDate).GreaterThanOrEqualTo(value => value.StartDate)
            .When(value => value.EndDate.HasValue);
        RuleFor(value => value.Services).MaximumLength(ClientCaregiver.MaximumServicesLength);
        RuleFor(value => value.Notes).MaximumLength(ClientCaregiver.MaximumNotesLength);
        RuleFor(value => value.RelationshipToClient).MaximumLength(ClientCaregiver.MaximumRelationshipLength);
    }
}
internal sealed class PublicCaregiverUpdateInputValidator : AbstractValidator<ClientCaregiverUpdateInput>
{
    public PublicCaregiverUpdateInputValidator()
    {
        RuleFor(value => value.Type).IsInEnum();
        RuleFor(value => value.StartDate).NotEmpty();
        RuleFor(value => value.EndDate).GreaterThanOrEqualTo(value => value.StartDate)
            .When(value => value.EndDate.HasValue);
        RuleFor(value => value.Services).MaximumLength(ClientCaregiver.MaximumServicesLength);
        RuleFor(value => value.Notes).MaximumLength(ClientCaregiver.MaximumNotesLength);
        RuleFor(value => value.RelationshipToClient).MaximumLength(ClientCaregiver.MaximumRelationshipLength);
        RuleFor(value => value.EditorVersion).NotEmpty().Length(64);
    }
}

public sealed class CreatePublicClientCaregiverHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<CreatePublicClientCaregiverCommand, PublicClientIntakeCaregiverMutationResult>
{
    public ValueTask<PublicClientIntakeCaregiverMutationResult> Handle(
        CreatePublicClientCaregiverCommand command, CancellationToken cancellationToken) =>
        new(resolver.CreateCaregiverAsync(command.Token, command.Input, cancellationToken));
}
public sealed class UpdatePublicClientCaregiverHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<UpdatePublicClientCaregiverCommand, PublicClientIntakeCaregiverMutationResult>
{
    public ValueTask<PublicClientIntakeCaregiverMutationResult> Handle(
        UpdatePublicClientCaregiverCommand command, CancellationToken cancellationToken) =>
        new(resolver.UpdateCaregiverAsync(command.Token, command.AssignmentId, command.Input, cancellationToken));
}
public sealed class EndPublicClientCaregiverHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<EndPublicClientCaregiverCommand, PublicClientIntakeCaregiverMutationResult>
{
    public ValueTask<PublicClientIntakeCaregiverMutationResult> Handle(
        EndPublicClientCaregiverCommand command, CancellationToken cancellationToken) =>
        new(resolver.EndCaregiverAsync(command.Token, command.AssignmentId, command.EndDate,
            command.EditorVersion, cancellationToken));
}
public sealed class ContinuePublicClientCaregiversHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<ContinuePublicClientCaregiversCommand, PublicClientIntakeCreationResult>
{
    public ValueTask<PublicClientIntakeCreationResult> Handle(
        ContinuePublicClientCaregiversCommand command, CancellationToken cancellationToken) =>
        new(resolver.ContinueFromCaregiversAsync(command.Token, cancellationToken));
}

public sealed record ListPublicClientContactsQuery(string Token)
    : IQuery<PublicClientIntakeContactsResult>;
public sealed record CreatePublicClientContactCommand(
    string Token, string? Type, string? Name, string? Relationship, string? Phone,
    string? Email, string? Address, bool? IsActive)
    : ICommand<PublicClientIntakeContactMutationResult>;
public sealed record UpdatePublicClientContactCommand(
    string Token, Guid ContactId, string? Type, string? Name, string? Relationship,
    string? Phone, string? Email, string? Address, bool? IsActive)
    : ICommand<PublicClientIntakeContactMutationResult>;
public sealed record ContinuePublicClientContactsCommand(string Token)
    : ICommand<PublicClientIntakeCreationResult>;

public sealed class CreatePublicClientContactValidator
    : ClientContactValidator<CreatePublicClientContactCommand>
{
    public CreatePublicClientContactValidator() => AddRules(
        value => value.Type, value => value.Name, value => value.Relationship,
        value => value.Phone, value => value.Email, value => value.Address,
        value => value.IsActive);
}
public sealed class UpdatePublicClientContactValidator
    : ClientContactValidator<UpdatePublicClientContactCommand>
{
    public UpdatePublicClientContactValidator()
    {
        RuleFor(value => value.ContactId).NotEmpty();
        AddRules(value => value.Type, value => value.Name, value => value.Relationship,
            value => value.Phone, value => value.Email, value => value.Address,
            value => value.IsActive);
    }
}

public sealed class ListPublicClientContactsHandler(IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<ListPublicClientContactsQuery, PublicClientIntakeContactsResult>
{
    public ValueTask<PublicClientIntakeContactsResult> Handle(
        ListPublicClientContactsQuery query, CancellationToken cancellationToken) =>
        new(resolver.ListContactsAsync(query.Token, cancellationToken));
}
public sealed class CreatePublicClientContactHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<CreatePublicClientContactCommand, PublicClientIntakeContactMutationResult>
{
    public ValueTask<PublicClientIntakeContactMutationResult> Handle(
        CreatePublicClientContactCommand command, CancellationToken cancellationToken) =>
        new(resolver.CreateContactAsync(command.Token, ToInput(command.Type, command.Name,
            command.Relationship, command.Phone, command.Email, command.Address, command.IsActive),
            cancellationToken));

    internal static SaveClientContact ToInput(
        string? type, string? name, string? relationship, string? phone,
        string? email, string? address, bool? isActive) =>
        new(type == "Emergency" ? ClientContactType.Emergency : ClientContactType.General,
            name!, relationship!, phone!, email, address, isActive!.Value);
}
public sealed class UpdatePublicClientContactHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<UpdatePublicClientContactCommand, PublicClientIntakeContactMutationResult>
{
    public ValueTask<PublicClientIntakeContactMutationResult> Handle(
        UpdatePublicClientContactCommand command, CancellationToken cancellationToken) =>
        new(resolver.UpdateContactAsync(command.Token, command.ContactId,
            CreatePublicClientContactHandler.ToInput(command.Type, command.Name,
                command.Relationship, command.Phone, command.Email, command.Address, command.IsActive),
            cancellationToken));
}
public sealed class ContinuePublicClientContactsHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<ContinuePublicClientContactsCommand, PublicClientIntakeCreationResult>
{
    public ValueTask<PublicClientIntakeCreationResult> Handle(
        ContinuePublicClientContactsCommand command, CancellationToken cancellationToken) =>
        new(resolver.ContinueFromContactsAsync(command.Token, cancellationToken));
}

public sealed record ListPublicClientCareCoordinatorsQuery(string Token) : IQuery<PublicClientIntakeCareCoordinatorsResult>;
public sealed record CreatePublicClientCareCoordinatorCommand(string Token, string? Name, string? CompanyName,
    string? Phone, string? PhoneExtension, string? Fax, string? Email, bool? IsActive)
    : ICommand<PublicClientIntakeCareCoordinatorMutationResult>;
public sealed record UpdatePublicClientCareCoordinatorCommand(string Token, Guid CoordinatorId, string? Name,
    string? CompanyName, string? Phone, string? PhoneExtension, string? Fax, string? Email, bool? IsActive)
    : ICommand<PublicClientIntakeCareCoordinatorMutationResult>;
public sealed record ContinuePublicClientCareCoordinatorsCommand(string Token) : ICommand<PublicClientIntakeCreationResult>;

public sealed class CreatePublicClientCareCoordinatorValidator : ClientCareCoordinatorValidator<CreatePublicClientCareCoordinatorCommand>
{
    public CreatePublicClientCareCoordinatorValidator() => AddRules(x => x.Name, x => x.CompanyName,
        x => x.Phone, x => x.PhoneExtension, x => x.Fax, x => x.Email, x => x.IsActive);
}
public sealed class UpdatePublicClientCareCoordinatorValidator : ClientCareCoordinatorValidator<UpdatePublicClientCareCoordinatorCommand>
{
    public UpdatePublicClientCareCoordinatorValidator()
    {
        RuleFor(x => x.CoordinatorId).NotEmpty();
        AddRules(x => x.Name, x => x.CompanyName, x => x.Phone, x => x.PhoneExtension,
            x => x.Fax, x => x.Email, x => x.IsActive);
    }
}
public sealed class ListPublicClientCareCoordinatorsHandler(IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<ListPublicClientCareCoordinatorsQuery, PublicClientIntakeCareCoordinatorsResult>
{
    public ValueTask<PublicClientIntakeCareCoordinatorsResult> Handle(ListPublicClientCareCoordinatorsQuery query,
        CancellationToken cancellationToken) => new(resolver.ListCareCoordinatorsAsync(query.Token, cancellationToken));
}
public sealed class CreatePublicClientCareCoordinatorHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<CreatePublicClientCareCoordinatorCommand, PublicClientIntakeCareCoordinatorMutationResult>
{
    public ValueTask<PublicClientIntakeCareCoordinatorMutationResult> Handle(CreatePublicClientCareCoordinatorCommand command,
        CancellationToken cancellationToken) => new(resolver.CreateCareCoordinatorAsync(command.Token,
            ToInput(command.Name, command.CompanyName, command.Phone, command.PhoneExtension, command.Fax,
                command.Email, command.IsActive), cancellationToken));
    internal static SaveCareCoordinator ToInput(string? name, string? companyName, string? phone,
        string? extension, string? fax, string? email, bool? active) =>
        new(name!, companyName, phone, extension, fax, email, active!.Value);
}
public sealed class UpdatePublicClientCareCoordinatorHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<UpdatePublicClientCareCoordinatorCommand, PublicClientIntakeCareCoordinatorMutationResult>
{
    public ValueTask<PublicClientIntakeCareCoordinatorMutationResult> Handle(UpdatePublicClientCareCoordinatorCommand command,
        CancellationToken cancellationToken) => new(resolver.UpdateCareCoordinatorAsync(command.Token, command.CoordinatorId,
            CreatePublicClientCareCoordinatorHandler.ToInput(command.Name, command.CompanyName, command.Phone,
                command.PhoneExtension, command.Fax, command.Email, command.IsActive), cancellationToken));
}
public sealed class ContinuePublicClientCareCoordinatorsHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<ContinuePublicClientCareCoordinatorsCommand, PublicClientIntakeCreationResult>
{
    public ValueTask<PublicClientIntakeCreationResult> Handle(ContinuePublicClientCareCoordinatorsCommand command,
        CancellationToken cancellationToken) => new(resolver.ContinueFromCareCoordinatorsAsync(command.Token, cancellationToken));
}

public sealed record ListPublicClientConsultationProvidersQuery(string Token)
    : IQuery<PublicClientIntakeConsultationProvidersResult>;
public sealed record CreatePublicClientConsultationProviderCommand(string Token, string? Name,
    string? Phone, string? Email, string? SupportProviderName, string? SupportProviderPhone,
    string? SupportProviderAddress, bool? IsActive)
    : ICommand<PublicClientIntakeConsultationProviderMutationResult>;
public sealed record UpdatePublicClientConsultationProviderCommand(string Token, Guid ProviderId,
    string? Name, string? Phone, string? Email, string? SupportProviderName,
    string? SupportProviderPhone, string? SupportProviderAddress, bool? IsActive)
    : ICommand<PublicClientIntakeConsultationProviderMutationResult>;
public sealed record ContinuePublicClientConsultationProvidersCommand(string Token)
    : ICommand<PublicClientIntakeCreationResult>;

public sealed class CreatePublicClientConsultationProviderValidator
    : ConsultationProviderValidator<CreatePublicClientConsultationProviderCommand>
{
    public CreatePublicClientConsultationProviderValidator() => AddRules(x => x.Name, x => x.Phone,
        x => x.Email, x => x.SupportProviderName, x => x.SupportProviderPhone,
        x => x.SupportProviderAddress, x => x.IsActive);
}
public sealed class UpdatePublicClientConsultationProviderValidator
    : ConsultationProviderValidator<UpdatePublicClientConsultationProviderCommand>
{
    public UpdatePublicClientConsultationProviderValidator()
    {
        RuleFor(x => x.ProviderId).NotEmpty();
        AddRules(x => x.Name, x => x.Phone, x => x.Email, x => x.SupportProviderName,
            x => x.SupportProviderPhone, x => x.SupportProviderAddress, x => x.IsActive);
    }
}
public sealed class ListPublicClientConsultationProvidersHandler(IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<ListPublicClientConsultationProvidersQuery, PublicClientIntakeConsultationProvidersResult>
{
    public ValueTask<PublicClientIntakeConsultationProvidersResult> Handle(
        ListPublicClientConsultationProvidersQuery query, CancellationToken cancellationToken) =>
        new(resolver.ListConsultationProvidersAsync(query.Token, cancellationToken));
}
public sealed class CreatePublicClientConsultationProviderHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<CreatePublicClientConsultationProviderCommand, PublicClientIntakeConsultationProviderMutationResult>
{
    public ValueTask<PublicClientIntakeConsultationProviderMutationResult> Handle(
        CreatePublicClientConsultationProviderCommand command, CancellationToken cancellationToken) =>
        new(resolver.CreateConsultationProviderAsync(command.Token, ToInput(command.Name, command.Phone,
            command.Email, command.SupportProviderName, command.SupportProviderPhone,
            command.SupportProviderAddress, command.IsActive), cancellationToken));
    internal static SaveConsultationProvider ToInput(string? name, string? phone, string? email,
        string? supportName, string? supportPhone, string? supportAddress, bool? active) =>
        new(name!, phone, email, supportName, supportPhone, supportAddress, active!.Value);
}
public sealed class UpdatePublicClientConsultationProviderHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<UpdatePublicClientConsultationProviderCommand, PublicClientIntakeConsultationProviderMutationResult>
{
    public ValueTask<PublicClientIntakeConsultationProviderMutationResult> Handle(
        UpdatePublicClientConsultationProviderCommand command, CancellationToken cancellationToken) =>
        new(resolver.UpdateConsultationProviderAsync(command.Token, command.ProviderId,
            CreatePublicClientConsultationProviderHandler.ToInput(command.Name, command.Phone,
                command.Email, command.SupportProviderName, command.SupportProviderPhone,
                command.SupportProviderAddress, command.IsActive), cancellationToken));
}
public sealed class ContinuePublicClientConsultationProvidersHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<ContinuePublicClientConsultationProvidersCommand, PublicClientIntakeCreationResult>
{
    public ValueTask<PublicClientIntakeCreationResult> Handle(
        ContinuePublicClientConsultationProvidersCommand command, CancellationToken cancellationToken) =>
        new(resolver.ContinueFromConsultationProvidersAsync(command.Token, cancellationToken));
}

public sealed record ListPublicClientPhysiciansQuery(string Token)
    : IQuery<PublicClientIntakePhysiciansResult>;
public sealed record CreatePublicClientPhysicianCommand(string Token, string? Name, string? Npi,
    string? Phone, string? Fax, string? Address, string? PostalCode, string? City,
    string? State, bool? IsActive) : ICommand<PublicClientIntakePhysicianMutationResult>;
public sealed record UpdatePublicClientPhysicianCommand(string Token, Guid PhysicianId,
    string? Name, string? Npi, string? Phone, string? Fax, string? Address,
    string? PostalCode, string? City, string? State, bool? IsActive)
    : ICommand<PublicClientIntakePhysicianMutationResult>;
public sealed record ContinuePublicClientPhysiciansCommand(string Token)
    : ICommand<PublicClientIntakeCreationResult>;

public sealed class CreatePublicClientPhysicianValidator
    : PhysicianValidator<CreatePublicClientPhysicianCommand>
{
    public CreatePublicClientPhysicianValidator() => AddRules(x => x.Name, x => x.Npi,
        x => x.Phone, x => x.Fax, x => x.Address, x => x.PostalCode, x => x.City,
        x => x.State, x => x.IsActive);
}
public sealed class UpdatePublicClientPhysicianValidator
    : PhysicianValidator<UpdatePublicClientPhysicianCommand>
{
    public UpdatePublicClientPhysicianValidator()
    {
        RuleFor(x => x.PhysicianId).NotEmpty();
        AddRules(x => x.Name, x => x.Npi, x => x.Phone, x => x.Fax, x => x.Address,
            x => x.PostalCode, x => x.City, x => x.State, x => x.IsActive);
    }
}
public sealed class ListPublicClientPhysiciansHandler(IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<ListPublicClientPhysiciansQuery, PublicClientIntakePhysiciansResult>
{
    public ValueTask<PublicClientIntakePhysiciansResult> Handle(
        ListPublicClientPhysiciansQuery query, CancellationToken cancellationToken) =>
        new(resolver.ListPhysiciansAsync(query.Token, cancellationToken));
}
public sealed class CreatePublicClientPhysicianHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<CreatePublicClientPhysicianCommand, PublicClientIntakePhysicianMutationResult>
{
    public ValueTask<PublicClientIntakePhysicianMutationResult> Handle(
        CreatePublicClientPhysicianCommand command, CancellationToken cancellationToken) =>
        new(resolver.CreatePhysicianAsync(command.Token, ToInput(command.Name, command.Npi,
            command.Phone, command.Fax, command.Address, command.PostalCode, command.City,
            command.State, command.IsActive), cancellationToken));
    internal static SavePhysician ToInput(string? name, string? npi, string? phone,
        string? fax, string? address, string? postalCode, string? city, string? state,
        bool? active) => new(name!, npi, phone, fax, address, postalCode, city, state, active!.Value);
}
public sealed class UpdatePublicClientPhysicianHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<UpdatePublicClientPhysicianCommand, PublicClientIntakePhysicianMutationResult>
{
    public ValueTask<PublicClientIntakePhysicianMutationResult> Handle(
        UpdatePublicClientPhysicianCommand command, CancellationToken cancellationToken) =>
        new(resolver.UpdatePhysicianAsync(command.Token, command.PhysicianId,
            CreatePublicClientPhysicianHandler.ToInput(command.Name, command.Npi, command.Phone,
                command.Fax, command.Address, command.PostalCode, command.City, command.State,
                command.IsActive), cancellationToken));
}
public sealed class ContinuePublicClientPhysiciansHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<ContinuePublicClientPhysiciansCommand, PublicClientIntakeCreationResult>
{
    public ValueTask<PublicClientIntakeCreationResult> Handle(
        ContinuePublicClientPhysiciansCommand command, CancellationToken cancellationToken) =>
        new(resolver.ContinueFromPhysiciansAsync(command.Token, cancellationToken));
}

public sealed record ListPublicClientResponsiblePartiesQuery(string Token)
    : IQuery<PublicClientIntakeResponsiblePartiesResult>;
public sealed record CreatePublicClientResponsiblePartyCommand(string Token, string? Name,
    string? Relationship, string? Phone, string? Email, string? Address,
    bool? LivesWithClient, bool? HasSpenddown, bool? IsActive)
    : ICommand<PublicClientIntakeResponsiblePartyMutationResult>;
public sealed record UpdatePublicClientResponsiblePartyCommand(string Token, Guid ResponsiblePartyId,
    string? Name, string? Relationship, string? Phone, string? Email, string? Address,
    bool? LivesWithClient, bool? HasSpenddown, bool? IsActive)
    : ICommand<PublicClientIntakeResponsiblePartyMutationResult>;
public sealed record ContinuePublicClientResponsiblePartiesCommand(string Token)
    : ICommand<PublicClientIntakeCreationResult>;

public sealed class CreatePublicClientResponsiblePartyValidator
    : ClientResponsiblePartyValidator<CreatePublicClientResponsiblePartyCommand>
{
    public CreatePublicClientResponsiblePartyValidator() => AddRules(x => x.Name,
        x => x.Relationship, x => x.Phone, x => x.Email, x => x.Address,
        x => x.LivesWithClient, x => x.HasSpenddown, x => x.IsActive);
}
public sealed class UpdatePublicClientResponsiblePartyValidator
    : ClientResponsiblePartyValidator<UpdatePublicClientResponsiblePartyCommand>
{
    public UpdatePublicClientResponsiblePartyValidator()
    {
        RuleFor(x => x.ResponsiblePartyId).NotEmpty();
        AddRules(x => x.Name, x => x.Relationship, x => x.Phone, x => x.Email,
            x => x.Address, x => x.LivesWithClient, x => x.HasSpenddown, x => x.IsActive);
    }
}
public sealed class ListPublicClientResponsiblePartiesHandler(IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<ListPublicClientResponsiblePartiesQuery, PublicClientIntakeResponsiblePartiesResult>
{
    public ValueTask<PublicClientIntakeResponsiblePartiesResult> Handle(
        ListPublicClientResponsiblePartiesQuery query, CancellationToken cancellationToken) =>
        new(resolver.ListResponsiblePartiesAsync(query.Token, cancellationToken));
}
public sealed class CreatePublicClientResponsiblePartyHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<CreatePublicClientResponsiblePartyCommand, PublicClientIntakeResponsiblePartyMutationResult>
{
    public ValueTask<PublicClientIntakeResponsiblePartyMutationResult> Handle(
        CreatePublicClientResponsiblePartyCommand command, CancellationToken cancellationToken) =>
        new(resolver.CreateResponsiblePartyAsync(command.Token, ToInput(command.Name,
            command.Relationship, command.Phone, command.Email, command.Address,
            command.LivesWithClient, command.HasSpenddown, command.IsActive), cancellationToken));
    internal static SaveResponsibleParty ToInput(string? name, string? relationship, string? phone,
        string? email, string? address, bool? livesWithClient, bool? hasSpenddown,
        bool? isActive) => new(name!, relationship!, phone!, email, address,
            livesWithClient!.Value, hasSpenddown!.Value, isActive!.Value);
}
public sealed class UpdatePublicClientResponsiblePartyHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<UpdatePublicClientResponsiblePartyCommand, PublicClientIntakeResponsiblePartyMutationResult>
{
    public ValueTask<PublicClientIntakeResponsiblePartyMutationResult> Handle(
        UpdatePublicClientResponsiblePartyCommand command, CancellationToken cancellationToken) =>
        new(resolver.UpdateResponsiblePartyAsync(command.Token, command.ResponsiblePartyId,
            CreatePublicClientResponsiblePartyHandler.ToInput(command.Name, command.Relationship,
                command.Phone, command.Email, command.Address, command.LivesWithClient,
                command.HasSpenddown, command.IsActive), cancellationToken));
}
public sealed class ContinuePublicClientResponsiblePartiesHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<ContinuePublicClientResponsiblePartiesCommand, PublicClientIntakeCreationResult>
{
    public ValueTask<PublicClientIntakeCreationResult> Handle(
        ContinuePublicClientResponsiblePartiesCommand command, CancellationToken cancellationToken) =>
        new(resolver.ContinueFromResponsiblePartiesAsync(command.Token, cancellationToken));
}

public sealed record ListPublicClientHospitalizationsQuery(string Token)
    : IQuery<PublicClientIntakeHospitalizationsResult>;
public sealed record CreatePublicClientHospitalizationCommand(string Token, string HospitalName,
    DateTimeOffset AdmittedAtUtc, DateTimeOffset? DischargedAtUtc, string Reason,
    string? Diagnosis, string? DischargeInstructions)
    : ICommand<PublicClientIntakeHospitalizationMutationResult>;
public sealed record UpdatePublicClientHospitalizationCommand(string Token, Guid HospitalizationId,
    string HospitalName, DateTimeOffset AdmittedAtUtc, DateTimeOffset? DischargedAtUtc,
    string Reason, string? Diagnosis, string? DischargeInstructions, string? EditorVersion)
    : ICommand<PublicClientIntakeHospitalizationMutationResult>;

public sealed class CreatePublicClientHospitalizationValidator
    : ClientHospitalizationValidator<CreatePublicClientHospitalizationCommand>
{
    public CreatePublicClientHospitalizationValidator() => AddRules(
        x => x.HospitalName, x => x.AdmittedAtUtc, x => x.DischargedAtUtc,
        x => x.Reason, x => x.Diagnosis, x => x.DischargeInstructions);
}

public sealed class UpdatePublicClientHospitalizationValidator
    : ClientHospitalizationValidator<UpdatePublicClientHospitalizationCommand>
{
    public UpdatePublicClientHospitalizationValidator()
    {
        RuleFor(x => x.HospitalizationId).NotEmpty();
        AddRules(x => x.HospitalName, x => x.AdmittedAtUtc, x => x.DischargedAtUtc,
            x => x.Reason, x => x.Diagnosis, x => x.DischargeInstructions);
        RuleFor(x => x.EditorVersion).NotEmpty().Length(64);
    }
}

public sealed class ListPublicClientHospitalizationsHandler(IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<ListPublicClientHospitalizationsQuery, PublicClientIntakeHospitalizationsResult>
{
    public ValueTask<PublicClientIntakeHospitalizationsResult> Handle(
        ListPublicClientHospitalizationsQuery query, CancellationToken cancellationToken) =>
        new(resolver.ListHospitalizationsAsync(query.Token, cancellationToken));
}

public sealed class CreatePublicClientHospitalizationHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<CreatePublicClientHospitalizationCommand, PublicClientIntakeHospitalizationMutationResult>
{
    public ValueTask<PublicClientIntakeHospitalizationMutationResult> Handle(
        CreatePublicClientHospitalizationCommand command, CancellationToken cancellationToken) =>
        new(resolver.CreateHospitalizationAsync(command.Token, new(command.HospitalName,
            command.AdmittedAtUtc, command.DischargedAtUtc, command.Reason, command.Diagnosis,
            command.DischargeInstructions, null), cancellationToken));
}

public sealed class UpdatePublicClientHospitalizationHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<UpdatePublicClientHospitalizationCommand, PublicClientIntakeHospitalizationMutationResult>
{
    public ValueTask<PublicClientIntakeHospitalizationMutationResult> Handle(
        UpdatePublicClientHospitalizationCommand command, CancellationToken cancellationToken) =>
        new(resolver.UpdateHospitalizationAsync(command.Token, command.HospitalizationId,
            new(command.HospitalName, command.AdmittedAtUtc, command.DischargedAtUtc,
                command.Reason, command.Diagnosis, command.DischargeInstructions,
                command.EditorVersion), cancellationToken));
}

public sealed record CompletePublicClientIntakeCommand(string Token)
    : ICommand<PublicClientIntakeCompletionResult>;

public sealed class CompletePublicClientIntakeHandler(IClientGuestInvitationPublicResolver resolver)
    : ICommandHandler<CompletePublicClientIntakeCommand, PublicClientIntakeCompletionResult>
{
    public ValueTask<PublicClientIntakeCompletionResult> Handle(
        CompletePublicClientIntakeCommand command, CancellationToken cancellationToken) =>
        new(resolver.CompleteAsync(command.Token, cancellationToken));
}
