using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Features.Clients;
using CareComply.Domain.Clients;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.ClientResponsibleParties;

public sealed record ListClientResponsiblePartiesQuery(Guid ClientId)
    : IQuery<ClientResponsiblePartyResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.ClientResponsibleParty.View;
}

public sealed record CreateClientResponsiblePartyCommand(
    Guid ClientId,
    string? Name,
    string? Relationship,
    string? Phone,
    string? Email,
    string? Address,
    bool? LivesWithClient,
    bool? HasSpenddown,
    bool? IsActive) : ICommand<ClientResponsiblePartyResult>, IRequirePermission,
    IAuditableRequest<ClientResponsiblePartyResult>
{
    public string RequiredPermission => Permissions.ClientResponsibleParty.Create;

    public AuditEvent CreateAuditEvent(ClientResponsiblePartyResult response) =>
        ClientResponsiblePartyAudit.Create(
            "ClientResponsibleParty.Created",
            ClientId,
            response,
            null);
}

public sealed record UpdateClientResponsiblePartyCommand(
    Guid ClientId,
    Guid PartyId,
    string? Name,
    string? Relationship,
    string? Phone,
    string? Email,
    string? Address,
    bool? LivesWithClient,
    bool? HasSpenddown,
    bool? IsActive) : ICommand<ClientResponsiblePartyResult>, IRequirePermission,
    IAuditableRequest<ClientResponsiblePartyResult>
{
    public string RequiredPermission => Permissions.ClientResponsibleParty.Edit;

    public AuditEvent CreateAuditEvent(ClientResponsiblePartyResult response) =>
        ClientResponsiblePartyAudit.Create(
            "ClientResponsibleParty.Updated",
            ClientId,
            response,
            PartyId);
}

public sealed record ClientResponsiblePartyResult(
    IReadOnlyList<ResponsiblePartyItem>? Items,
    ResponsiblePartyItem? Item,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ClientResponsiblePartyResult>
{
    public static ClientResponsiblePartyResult Listed(IReadOnlyList<ResponsiblePartyItem>? items) =>
        new(items, null, null);

    public static ClientResponsiblePartyResult Changed(ResponsiblePartyItem? item) =>
        new(null, item, null);

    public static ClientResponsiblePartyResult Denied(AuthorizationFailure failure) =>
        new(null, null, failure);
}


public sealed class ListClientResponsiblePartiesHandler(IClientResponsiblePartyStore store)
    : IQueryHandler<ListClientResponsiblePartiesQuery, ClientResponsiblePartyResult>
{
    public async ValueTask<ClientResponsiblePartyResult> Handle(
        ListClientResponsiblePartiesQuery query,
        CancellationToken cancellationToken) =>
        ClientResponsiblePartyResult.Listed(
            await store.ListAsync(query.ClientId, cancellationToken));
}

public sealed class CreateClientResponsiblePartyHandler(IClientResponsiblePartyStore store)
    : ICommandHandler<CreateClientResponsiblePartyCommand, ClientResponsiblePartyResult>
{
    public async ValueTask<ClientResponsiblePartyResult> Handle(
        CreateClientResponsiblePartyCommand command,
        CancellationToken cancellationToken) =>
        ClientResponsiblePartyResult.Changed(
            await store.CreateAsync(command.ClientId, ToInput(command), cancellationToken));

    private static SaveResponsibleParty ToInput(CreateClientResponsiblePartyCommand command) =>
        new(command.Name!, command.Relationship!, command.Phone!, command.Email, command.Address,
            command.LivesWithClient!.Value, command.HasSpenddown!.Value,
            command.IsActive!.Value);
}

public sealed class UpdateClientResponsiblePartyHandler(IClientResponsiblePartyStore store)
    : ICommandHandler<UpdateClientResponsiblePartyCommand, ClientResponsiblePartyResult>
{
    public async ValueTask<ClientResponsiblePartyResult> Handle(
        UpdateClientResponsiblePartyCommand command,
        CancellationToken cancellationToken) =>
        ClientResponsiblePartyResult.Changed(
            await store.UpdateAsync(
                command.ClientId,
                command.PartyId,
                new(command.Name!, command.Relationship!, command.Phone!, command.Email,
                    command.Address, command.LivesWithClient!.Value,
                    command.HasSpenddown!.Value, command.IsActive!.Value),
                cancellationToken));
}

public abstract class ClientResponsiblePartyValidator<T> : AbstractValidator<T>
    where T : class
{
    protected void AddRules(
        Func<T, string?> name,
        Func<T, string?> relationship,
        Func<T, string?> phone,
        Func<T, string?> email,
        Func<T, string?> address,
        Func<T, bool?> livesWithClient,
        Func<T, bool?> hasSpenddown,
        Func<T, bool?> isActive)
    {
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
        RuleFor(x => livesWithClient(x)).NotNull().OverridePropertyName("LivesWithClient")
            .WithMessage("Select whether this person lives with the client.");
        RuleFor(x => hasSpenddown(x)).NotNull().OverridePropertyName("HasSpenddown")
            .WithMessage("Select whether this person has spenddown responsibility.");
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

public sealed class CreateClientResponsiblePartyValidator
    : ClientResponsiblePartyValidator<CreateClientResponsiblePartyCommand>
{
    public CreateClientResponsiblePartyValidator() =>
        AddRules(x => x.Name, x => x.Relationship, x => x.Phone, x => x.Email,
            x => x.Address, x => x.LivesWithClient, x => x.HasSpenddown, x => x.IsActive);
}

public sealed class UpdateClientResponsiblePartyValidator
    : ClientResponsiblePartyValidator<UpdateClientResponsiblePartyCommand>
{
    public UpdateClientResponsiblePartyValidator() =>
        AddRules(x => x.Name, x => x.Relationship, x => x.Phone, x => x.Email,
            x => x.Address, x => x.LivesWithClient, x => x.HasSpenddown, x => x.IsActive);
}

internal static class ClientResponsiblePartyAudit
{
    public static AuditEvent Create(
        string action,
        Guid clientId,
        ClientResponsiblePartyResult response,
        Guid? requestedId)
    {
        var succeeded = response.Item is not null;
        return new(
            action,
            "ClientResponsibleParty",
            (response.Item?.Id ?? requestedId)?.ToString("N") ?? "not-created",
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(("clientId", clientId.ToString("N")))
                : AuditMetadata.Create(("reasonCode", "not-found")));
    }
}
