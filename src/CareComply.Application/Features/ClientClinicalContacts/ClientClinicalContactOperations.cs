using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Features.Clients;
using CareComply.Domain.Clients;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.ClientClinicalContacts;

public sealed record ListConsultationProvidersQuery(Guid ClientId)
    : IQuery<ClientClinicalContactResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.ClientConsultationProvider.View;
}

public sealed record ListPhysiciansQuery(Guid ClientId)
    : IQuery<ClientClinicalContactResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.ClientPhysician.View;
}

public sealed record CreateConsultationProviderCommand(
    Guid ClientId,
    string? Name,
    string? Phone,
    string? Email,
    string? SupportProviderName,
    string? SupportProviderPhone,
    string? SupportProviderAddress,
    bool? IsActive) : ICommand<ClientClinicalContactResult>, IRequirePermission,
    IAuditableRequest<ClientClinicalContactResult>
{
    public string RequiredPermission => Permissions.ClientConsultationProvider.Create;

    public AuditEvent CreateAuditEvent(ClientClinicalContactResult response) =>
        ClientClinicalContactAudit.Create(
            "ClientConsultationProvider.Created",
            "ClientConsultationProvider",
            ClientId,
            response,
            null);
}

public sealed record UpdateConsultationProviderCommand(
    Guid ClientId,
    Guid ContactId,
    string? Name,
    string? Phone,
    string? Email,
    string? SupportProviderName,
    string? SupportProviderPhone,
    string? SupportProviderAddress,
    bool? IsActive) : ICommand<ClientClinicalContactResult>, IRequirePermission,
    IAuditableRequest<ClientClinicalContactResult>
{
    public string RequiredPermission => Permissions.ClientConsultationProvider.Edit;

    public AuditEvent CreateAuditEvent(ClientClinicalContactResult response) =>
        ClientClinicalContactAudit.Create(
            "ClientConsultationProvider.Updated",
            "ClientConsultationProvider",
            ClientId,
            response,
            ContactId);
}

public sealed record CreatePhysicianCommand(
    Guid ClientId,
    string? Name,
    string? Npi,
    string? Phone,
    string? Fax,
    string? Address,
    string? PostalCode,
    string? City,
    string? State,
    bool? IsActive) : ICommand<ClientClinicalContactResult>, IRequirePermission,
    IAuditableRequest<ClientClinicalContactResult>
{
    public string RequiredPermission => Permissions.ClientPhysician.Create;

    public AuditEvent CreateAuditEvent(ClientClinicalContactResult response) =>
        ClientClinicalContactAudit.Create(
            "ClientPhysician.Created",
            "ClientPhysician",
            ClientId,
            response,
            null);
}

public sealed record UpdatePhysicianCommand(
    Guid ClientId,
    Guid ContactId,
    string? Name,
    string? Npi,
    string? Phone,
    string? Fax,
    string? Address,
    string? PostalCode,
    string? City,
    string? State,
    bool? IsActive) : ICommand<ClientClinicalContactResult>, IRequirePermission,
    IAuditableRequest<ClientClinicalContactResult>
{
    public string RequiredPermission => Permissions.ClientPhysician.Edit;

    public AuditEvent CreateAuditEvent(ClientClinicalContactResult response) =>
        ClientClinicalContactAudit.Create(
            "ClientPhysician.Updated",
            "ClientPhysician",
            ClientId,
            response,
            ContactId);
}

public sealed record ClientClinicalContactResult(
    IReadOnlyList<ClinicalContactItem>? Items,
    ClinicalContactItem? Item,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ClientClinicalContactResult>
{
    public static ClientClinicalContactResult Listed(IReadOnlyList<ClinicalContactItem>? items) =>
        new(items, null, null);

    public static ClientClinicalContactResult Changed(ClinicalContactItem? item) =>
        new(null, item, null);

    public static ClientClinicalContactResult Denied(AuthorizationFailure failure) =>
        new(null, null, failure);
}


public sealed class ListClientClinicalContactsHandler(IClientClinicalContactStore store)
    : IQueryHandler<ListConsultationProvidersQuery, ClientClinicalContactResult>,
      IQueryHandler<ListPhysiciansQuery, ClientClinicalContactResult>
{
    public async ValueTask<ClientClinicalContactResult> Handle(
        ListConsultationProvidersQuery query,
        CancellationToken cancellationToken) =>
        ClientClinicalContactResult.Listed(
            await store.ListAsync(
                query.ClientId,
                ClientContactType.ConsultationProvider,
                cancellationToken));

    public async ValueTask<ClientClinicalContactResult> Handle(
        ListPhysiciansQuery query,
        CancellationToken cancellationToken) =>
        ClientClinicalContactResult.Listed(
            await store.ListAsync(query.ClientId, ClientContactType.Physician, cancellationToken));
}

public sealed class CreateConsultationProviderHandler(IClientClinicalContactStore store)
    : ICommandHandler<CreateConsultationProviderCommand, ClientClinicalContactResult>
{
    public async ValueTask<ClientClinicalContactResult> Handle(
        CreateConsultationProviderCommand command,
        CancellationToken cancellationToken) =>
        ClientClinicalContactResult.Changed(
            await store.CreateConsultationProviderAsync(
                command.ClientId,
                new(command.Name!, command.Phone, command.Email, command.SupportProviderName,
                    command.SupportProviderPhone, command.SupportProviderAddress,
                    command.IsActive!.Value),
                cancellationToken));
}

public sealed class UpdateConsultationProviderHandler(IClientClinicalContactStore store)
    : ICommandHandler<UpdateConsultationProviderCommand, ClientClinicalContactResult>
{
    public async ValueTask<ClientClinicalContactResult> Handle(
        UpdateConsultationProviderCommand command,
        CancellationToken cancellationToken) =>
        ClientClinicalContactResult.Changed(
            await store.UpdateConsultationProviderAsync(
                command.ClientId,
                command.ContactId,
                new(command.Name!, command.Phone, command.Email, command.SupportProviderName,
                    command.SupportProviderPhone, command.SupportProviderAddress,
                    command.IsActive!.Value),
                cancellationToken));
}

public sealed class CreatePhysicianHandler(IClientClinicalContactStore store)
    : ICommandHandler<CreatePhysicianCommand, ClientClinicalContactResult>
{
    public async ValueTask<ClientClinicalContactResult> Handle(
        CreatePhysicianCommand command,
        CancellationToken cancellationToken) =>
        ClientClinicalContactResult.Changed(
            await store.CreatePhysicianAsync(
                command.ClientId,
                new(command.Name!, command.Npi, command.Phone, command.Fax, command.Address,
                    command.PostalCode, command.City, command.State, command.IsActive!.Value),
                cancellationToken));
}

public sealed class UpdatePhysicianHandler(IClientClinicalContactStore store)
    : ICommandHandler<UpdatePhysicianCommand, ClientClinicalContactResult>
{
    public async ValueTask<ClientClinicalContactResult> Handle(
        UpdatePhysicianCommand command,
        CancellationToken cancellationToken) =>
        ClientClinicalContactResult.Changed(
            await store.UpdatePhysicianAsync(
                command.ClientId,
                command.ContactId,
                new(command.Name!, command.Npi, command.Phone, command.Fax, command.Address,
                    command.PostalCode, command.City, command.State, command.IsActive!.Value),
                cancellationToken));
}

public abstract class ConsultationProviderValidator<T> : AbstractValidator<T>
    where T : class
{
    protected void AddRules(
        Func<T, string?> name,
        Func<T, string?> phone,
        Func<T, string?> email,
        Func<T, string?> supportName,
        Func<T, string?> supportPhone,
        Func<T, string?> supportAddress,
        Func<T, bool?> isActive)
    {
        ContactRule(name, "Name", ClientContact.MaximumNameLength, true);
        ContactRule(phone, "Phone", ClientContact.MaximumPhoneLength);
        PhoneRule(phone, "Phone");
        RuleFor(x => email(x))
            .MaximumLength(ClientContact.MaximumEmailLength).OverridePropertyName("Email")
            .WithMessage("Email is too long.")
            .EmailAddress().OverridePropertyName("Email").WithMessage("Enter a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(email(x)));
        ContactRule(supportName, "SupportProviderName", ClientContact.MaximumNameLength);
        ContactRule(supportPhone, "SupportProviderPhone", ClientContact.MaximumPhoneLength);
        PhoneRule(supportPhone, "SupportProviderPhone");
        ContactRule(supportAddress, "SupportProviderAddress", ClientContact.MaximumAddressLength);
        RuleFor(x => isActive(x)).NotNull().OverridePropertyName("IsActive")
            .WithMessage("Select a status.");
    }

    private void PhoneRule(Func<T, string?> value, string field) =>
        RuleFor(x => value(x)).Must(ClientValidation.IsValidUsPhone)
            .OverridePropertyName(field).WithMessage($"{field} must contain exactly 10 US digits.")
            .When(x => !string.IsNullOrWhiteSpace(value(x)));

    private void ContactRule(
        Func<T, string?> value,
        string field,
        int maximum,
        bool required = false)
    {
        var rule = RuleFor(x => value(x)).Cascade(CascadeMode.Stop);
        if (required)
            rule.Must(item => !string.IsNullOrWhiteSpace(item)).OverridePropertyName(field)
                .WithMessage("Name is required.");
        rule.MaximumLength(maximum).OverridePropertyName(field).WithMessage($"{field} is too long.");
    }
}

public abstract class PhysicianValidator<T> : AbstractValidator<T>
    where T : class
{
    protected void AddRules(
        Func<T, string?> name,
        Func<T, string?> npi,
        Func<T, string?> phone,
        Func<T, string?> fax,
        Func<T, string?> address,
        Func<T, string?> postalCode,
        Func<T, string?> city,
        Func<T, string?> state,
        Func<T, bool?> isActive)
    {
        RuleFor(x => name(x)).Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value)).OverridePropertyName("Name")
            .WithMessage("Name is required.")
            .MaximumLength(ClientContact.MaximumNameLength).OverridePropertyName("Name")
            .WithMessage("Name is too long.");
        RuleFor(x => npi(x)).Matches("^[0-9]{10}$").OverridePropertyName("Npi")
            .WithMessage("NPI must contain exactly 10 digits.")
            .When(x => !string.IsNullOrWhiteSpace(npi(x)));
        Maximum(phone, "Phone", ClientContact.MaximumPhoneLength);
        Maximum(fax, "Fax", ClientContact.MaximumPhoneLength);
        Phone(phone, "Phone");
        Phone(fax, "Fax");
        Maximum(address, "Address", ClientContact.MaximumAddressLength);
        Maximum(postalCode, "PostalCode", ClientContact.MaximumPostalCodeLength);
        RuleFor(x => postalCode(x)).Must(ClientValidation.IsValidUsPostalCode)
            .OverridePropertyName("PostalCode")
            .WithMessage("Postal code must use 12345 or 12345-6789 format.");
        Maximum(city, "City", ClientContact.MaximumCityLength);
        Maximum(state, "State", ClientContact.MaximumStateLength);
        RuleFor(x => isActive(x)).NotNull().OverridePropertyName("IsActive")
            .WithMessage("Select a status.");
    }

    private void Maximum(Func<T, string?> value, string field, int maximum) =>
        RuleFor(x => value(x)).MaximumLength(maximum).OverridePropertyName(field)
            .WithMessage($"{field} is too long.");

    private void Phone(Func<T, string?> value, string field) =>
        RuleFor(x => value(x)).Must(ClientValidation.IsValidUsPhone)
            .OverridePropertyName(field).WithMessage($"{field} must contain exactly 10 US digits.")
            .When(x => !string.IsNullOrWhiteSpace(value(x)));
}

public sealed class CreateConsultationProviderValidator
    : ConsultationProviderValidator<CreateConsultationProviderCommand>
{
    public CreateConsultationProviderValidator() =>
        AddRules(x => x.Name, x => x.Phone, x => x.Email, x => x.SupportProviderName,
            x => x.SupportProviderPhone, x => x.SupportProviderAddress, x => x.IsActive);
}

public sealed class UpdateConsultationProviderValidator
    : ConsultationProviderValidator<UpdateConsultationProviderCommand>
{
    public UpdateConsultationProviderValidator() =>
        AddRules(x => x.Name, x => x.Phone, x => x.Email, x => x.SupportProviderName,
            x => x.SupportProviderPhone, x => x.SupportProviderAddress, x => x.IsActive);
}

public sealed class CreatePhysicianValidator : PhysicianValidator<CreatePhysicianCommand>
{
    public CreatePhysicianValidator() =>
        AddRules(x => x.Name, x => x.Npi, x => x.Phone, x => x.Fax, x => x.Address,
            x => x.PostalCode, x => x.City, x => x.State, x => x.IsActive);
}

public sealed class UpdatePhysicianValidator : PhysicianValidator<UpdatePhysicianCommand>
{
    public UpdatePhysicianValidator() =>
        AddRules(x => x.Name, x => x.Npi, x => x.Phone, x => x.Fax, x => x.Address,
            x => x.PostalCode, x => x.City, x => x.State, x => x.IsActive);
}

internal static class ClientClinicalContactAudit
{
    public static AuditEvent Create(
        string action,
        string resourceType,
        Guid clientId,
        ClientClinicalContactResult response,
        Guid? requestedId)
    {
        var succeeded = response.Item is not null;
        return new(
            action,
            resourceType,
            (response.Item?.Id ?? requestedId)?.ToString("N") ?? "not-created",
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(("clientId", clientId.ToString("N")))
                : AuditMetadata.Create(("reasonCode", "not-found")));
    }
}
