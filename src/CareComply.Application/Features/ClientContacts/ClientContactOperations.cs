using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Features.Clients;
using CareComply.Domain.Clients;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.ClientContacts;

public sealed record ListClientContactsQuery(Guid ClientId)
    : IQuery<ClientContactResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.ClientContact.View;
}

public sealed record CreateClientContactCommand(
    Guid ClientId,
    string? Type,
    string? Name,
    string? Relationship,
    string? Phone,
    string? Email,
    string? Address,
    bool? IsActive) : ICommand<ClientContactResult>, IRequirePermission,
    IAuditableRequest<ClientContactResult>
{
    public string RequiredPermission => Permissions.ClientContact.Create;

    public AuditEvent CreateAuditEvent(ClientContactResult response) =>
        ClientContactAudit.Create("ClientContact.Created", ClientId, response, null);
}

public sealed record UpdateClientContactCommand(
    Guid ClientId,
    Guid ContactId,
    string? Type,
    string? Name,
    string? Relationship,
    string? Phone,
    string? Email,
    string? Address,
    bool? IsActive) : ICommand<ClientContactResult>, IRequirePermission,
    IAuditableRequest<ClientContactResult>
{
    public string RequiredPermission => Permissions.ClientContact.Edit;

    public AuditEvent CreateAuditEvent(ClientContactResult response) =>
        ClientContactAudit.Create("ClientContact.Updated", ClientId, response, ContactId);
}

public sealed record ClientContactResult(
    IReadOnlyList<ClientContactItem>? Items,
    ClientContactItem? Item,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ClientContactResult>
{
    public static ClientContactResult Listed(IReadOnlyList<ClientContactItem>? items) =>
        new(items, null, null);

    public static ClientContactResult Changed(ClientContactItem? item) =>
        new(null, item, null);

    public static ClientContactResult Denied(AuthorizationFailure failure) =>
        new(null, null, failure);
}


public sealed class ListClientContactsHandler(IClientContactStore store)
    : IQueryHandler<ListClientContactsQuery, ClientContactResult>
{
    public async ValueTask<ClientContactResult> Handle(
        ListClientContactsQuery query,
        CancellationToken cancellationToken) =>
        ClientContactResult.Listed(await store.ListAsync(query.ClientId, cancellationToken));
}

public sealed class CreateClientContactHandler(IClientContactStore store)
    : ICommandHandler<CreateClientContactCommand, ClientContactResult>
{
    public async ValueTask<ClientContactResult> Handle(
        CreateClientContactCommand command,
        CancellationToken cancellationToken) =>
        ClientContactResult.Changed(
            await store.CreateAsync(command.ClientId, ToInput(command), cancellationToken));

    private static SaveClientContact ToInput(CreateClientContactCommand command) =>
        new(ParseType(command.Type), command.Name!, command.Relationship!, command.Phone!,
            command.Email, command.Address, command.IsActive!.Value);

    private static ClientContactType ParseType(string? type) =>
        type == "Emergency" ? ClientContactType.Emergency : ClientContactType.General;
}

public sealed class UpdateClientContactHandler(IClientContactStore store)
    : ICommandHandler<UpdateClientContactCommand, ClientContactResult>
{
    public async ValueTask<ClientContactResult> Handle(
        UpdateClientContactCommand command,
        CancellationToken cancellationToken) =>
        ClientContactResult.Changed(
            await store.UpdateAsync(
                command.ClientId,
                command.ContactId,
                new(
                    command.Type == "Emergency" ? ClientContactType.Emergency : ClientContactType.General,
                    command.Name!,
                    command.Relationship!,
                    command.Phone!,
                    command.Email,
                    command.Address,
                    command.IsActive!.Value),
                cancellationToken));
}

public abstract class ClientContactValidator<T> : AbstractValidator<T>
    where T : class
{
    protected void AddRules(
        Func<T, string?> type,
        Func<T, string?> name,
        Func<T, string?> relationship,
        Func<T, string?> phone,
        Func<T, string?> email,
        Func<T, string?> address,
        Func<T, bool?> isActive)
    {
        RuleFor(x => type(x)).Must(value => value is "General" or "Emergency")
            .OverridePropertyName("Type").WithMessage("Select General or Emergency.");
        Required(x => name(x), "Name", ClientContact.MaximumNameLength);
        Required(x => relationship(x), "Relationship", ClientContact.MaximumRelationshipLength);
        Required(x => phone(x), "Phone", ClientContact.MaximumPhoneLength);
        RuleFor(x => phone(x)).Must(ClientValidation.IsValidUsPhone)
            .OverridePropertyName("Phone").WithMessage("Phone must contain exactly 10 US digits.")
            .When(x => !string.IsNullOrWhiteSpace(phone(x)));
        RuleFor(x => email(x))
            .Cascade(CascadeMode.Stop)
            .MaximumLength(ClientContact.MaximumEmailLength).OverridePropertyName("Email")
            .WithMessage("Enter a valid email address.")
            .EmailAddress().OverridePropertyName("Email").WithMessage("Enter a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(email(x)));
        RuleFor(x => address(x)).MaximumLength(ClientContact.MaximumAddressLength)
            .OverridePropertyName("Address").WithMessage("Address is too long.");
        RuleFor(x => isActive(x)).NotNull().OverridePropertyName("IsActive")
            .WithMessage("Select a status.");
    }

    private void Required(Func<T, string?> value, string field, int maximum) =>
        RuleFor(x => value(x))
            .Cascade(CascadeMode.Stop)
            .Must(item => !string.IsNullOrWhiteSpace(item)).OverridePropertyName(field)
            .WithMessage($"{field} is required.")
            .MaximumLength(maximum).OverridePropertyName(field).WithMessage($"{field} is too long.");
}

public sealed class CreateClientContactValidator : ClientContactValidator<CreateClientContactCommand>
{
    public CreateClientContactValidator() =>
        AddRules(x => x.Type, x => x.Name, x => x.Relationship, x => x.Phone,
            x => x.Email, x => x.Address, x => x.IsActive);
}

public sealed class UpdateClientContactValidator : ClientContactValidator<UpdateClientContactCommand>
{
    public UpdateClientContactValidator() =>
        AddRules(x => x.Type, x => x.Name, x => x.Relationship, x => x.Phone,
            x => x.Email, x => x.Address, x => x.IsActive);
}

internal static class ClientContactAudit
{
    public static AuditEvent Create(
        string action,
        Guid clientId,
        ClientContactResult response,
        Guid? requestedId)
    {
        var succeeded = response.Item is not null;
        return new(
            action,
            "ClientContact",
            (response.Item?.Id ?? requestedId)?.ToString("N") ?? "not-created",
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(("clientId", clientId.ToString("N")))
                : AuditMetadata.Create(("reasonCode", "not-found")));
    }
}
