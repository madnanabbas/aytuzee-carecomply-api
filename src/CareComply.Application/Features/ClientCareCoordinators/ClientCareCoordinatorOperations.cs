using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Features.Clients;
using CareComply.Domain.Clients;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.ClientCareCoordinators;

public sealed record ListClientCareCoordinatorsQuery(Guid ClientId)
    : IQuery<ClientCareCoordinatorResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.ClientCareCoordinator.View;
}

public sealed record CreateClientCareCoordinatorCommand(
    Guid ClientId,
    string? Name,
    string? CompanyName,
    string? Phone,
    string? PhoneExtension,
    string? Fax,
    string? Email,
    bool? IsActive) : ICommand<ClientCareCoordinatorResult>, IRequirePermission,
    IAuditableRequest<ClientCareCoordinatorResult>
{
    public string RequiredPermission => Permissions.ClientCareCoordinator.Create;

    public AuditEvent CreateAuditEvent(ClientCareCoordinatorResult response) =>
        ClientCareCoordinatorAudit.Create(
            "ClientCareCoordinator.Created",
            ClientId,
            response,
            null);
}

public sealed record UpdateClientCareCoordinatorCommand(
    Guid ClientId,
    Guid CoordinatorId,
    string? Name,
    string? CompanyName,
    string? Phone,
    string? PhoneExtension,
    string? Fax,
    string? Email,
    bool? IsActive) : ICommand<ClientCareCoordinatorResult>, IRequirePermission,
    IAuditableRequest<ClientCareCoordinatorResult>
{
    public string RequiredPermission => Permissions.ClientCareCoordinator.Edit;

    public AuditEvent CreateAuditEvent(ClientCareCoordinatorResult response) =>
        ClientCareCoordinatorAudit.Create(
            "ClientCareCoordinator.Updated",
            ClientId,
            response,
            CoordinatorId);
}

public sealed record ClientCareCoordinatorResult(
    IReadOnlyList<CareCoordinatorItem>? Items,
    CareCoordinatorItem? Item,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ClientCareCoordinatorResult>
{
    public static ClientCareCoordinatorResult Listed(IReadOnlyList<CareCoordinatorItem>? items) =>
        new(items, null, null);

    public static ClientCareCoordinatorResult Changed(CareCoordinatorItem? item) =>
        new(null, item, null);

    public static ClientCareCoordinatorResult Denied(AuthorizationFailure failure) =>
        new(null, null, failure);
}


public sealed class ListClientCareCoordinatorsHandler(IClientCareCoordinatorStore store)
    : IQueryHandler<ListClientCareCoordinatorsQuery, ClientCareCoordinatorResult>
{
    public async ValueTask<ClientCareCoordinatorResult> Handle(
        ListClientCareCoordinatorsQuery query,
        CancellationToken cancellationToken) =>
        ClientCareCoordinatorResult.Listed(
            await store.ListAsync(query.ClientId, cancellationToken));
}

public sealed class CreateClientCareCoordinatorHandler(IClientCareCoordinatorStore store)
    : ICommandHandler<CreateClientCareCoordinatorCommand, ClientCareCoordinatorResult>
{
    public async ValueTask<ClientCareCoordinatorResult> Handle(
        CreateClientCareCoordinatorCommand command,
        CancellationToken cancellationToken) =>
        ClientCareCoordinatorResult.Changed(
            await store.CreateAsync(
                command.ClientId,
                ToInput(command),
                cancellationToken));

    private static SaveCareCoordinator ToInput(CreateClientCareCoordinatorCommand command) =>
        new(
            command.Name!,
            command.CompanyName,
            command.Phone,
            command.PhoneExtension,
            command.Fax,
            command.Email,
            command.IsActive!.Value);
}

public sealed class UpdateClientCareCoordinatorHandler(IClientCareCoordinatorStore store)
    : ICommandHandler<UpdateClientCareCoordinatorCommand, ClientCareCoordinatorResult>
{
    public async ValueTask<ClientCareCoordinatorResult> Handle(
        UpdateClientCareCoordinatorCommand command,
        CancellationToken cancellationToken) =>
        ClientCareCoordinatorResult.Changed(
            await store.UpdateAsync(
                command.ClientId,
                command.CoordinatorId,
                new(
                    command.Name!,
                    command.CompanyName,
                    command.Phone,
                    command.PhoneExtension,
                    command.Fax,
                    command.Email,
                    command.IsActive!.Value),
                cancellationToken));
}

public abstract class ClientCareCoordinatorValidator<T> : AbstractValidator<T>
    where T : class
{
    protected void AddRules(
        Func<T, string?> name,
        Func<T, string?> companyName,
        Func<T, string?> phone,
        Func<T, string?> extension,
        Func<T, string?> fax,
        Func<T, string?> email,
        Func<T, bool?> isActive)
    {
        RuleFor(x => name(x))
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value)).OverridePropertyName("Name")
            .WithMessage("Name is required.")
            .MaximumLength(ClientContact.MaximumNameLength).OverridePropertyName("Name")
            .WithMessage("Name is too long.");
        RuleFor(x => companyName(x)).MaximumLength(ClientContact.MaximumCompanyLength)
            .OverridePropertyName("CompanyName").WithMessage("CompanyName is too long.");
        RuleFor(x => phone(x)).MaximumLength(ClientContact.MaximumPhoneLength)
            .OverridePropertyName("Phone").WithMessage("Phone is too long.");
        RuleFor(x => phone(x)).Must(ClientValidation.IsValidUsPhone)
            .OverridePropertyName("Phone").WithMessage("Phone must contain exactly 10 US digits.")
            .When(x => !string.IsNullOrWhiteSpace(phone(x)));
        RuleFor(x => extension(x)).MaximumLength(ClientContact.MaximumExtensionLength)
            .OverridePropertyName("PhoneExtension").WithMessage("PhoneExtension is too long.");
        RuleFor(x => extension(x)).Matches("^[0-9]+$")
            .OverridePropertyName("PhoneExtension").WithMessage("Phone extension must contain digits only.")
            .When(x => !string.IsNullOrWhiteSpace(extension(x)));
        RuleFor(x => fax(x)).MaximumLength(ClientContact.MaximumPhoneLength)
            .OverridePropertyName("Fax").WithMessage("Fax is too long.");
        RuleFor(x => fax(x)).Must(ClientValidation.IsValidUsPhone)
            .OverridePropertyName("Fax").WithMessage("Fax must contain exactly 10 US digits.")
            .When(x => !string.IsNullOrWhiteSpace(fax(x)));
        RuleFor(x => email(x))
            .MaximumLength(ClientContact.MaximumEmailLength).OverridePropertyName("Email")
            .WithMessage("Email is too long.")
            .EmailAddress().OverridePropertyName("Email").WithMessage("Enter a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(email(x)));
        RuleFor(x => isActive(x)).NotNull().OverridePropertyName("IsActive")
            .WithMessage("Select a status.");
    }
}

public sealed class CreateClientCareCoordinatorValidator
    : ClientCareCoordinatorValidator<CreateClientCareCoordinatorCommand>
{
    public CreateClientCareCoordinatorValidator() =>
        AddRules(x => x.Name, x => x.CompanyName, x => x.Phone, x => x.PhoneExtension,
            x => x.Fax, x => x.Email, x => x.IsActive);
}

public sealed class UpdateClientCareCoordinatorValidator
    : ClientCareCoordinatorValidator<UpdateClientCareCoordinatorCommand>
{
    public UpdateClientCareCoordinatorValidator() =>
        AddRules(x => x.Name, x => x.CompanyName, x => x.Phone, x => x.PhoneExtension,
            x => x.Fax, x => x.Email, x => x.IsActive);
}

internal static class ClientCareCoordinatorAudit
{
    public static AuditEvent Create(
        string action,
        Guid clientId,
        ClientCareCoordinatorResult response,
        Guid? requestedId)
    {
        var succeeded = response.Item is not null;
        return new(
            action,
            "ClientCareCoordinator",
            (response.Item?.Id ?? requestedId)?.ToString("N") ?? "not-created",
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(("clientId", clientId.ToString("N")))
                : AuditMetadata.Create(("reasonCode", "not-found")));
    }
}
