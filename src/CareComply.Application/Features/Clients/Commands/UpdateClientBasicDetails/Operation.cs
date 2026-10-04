using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.Clients.Commands.UpdateClientBasicDetails;

public sealed record UpdateClientBasicDetailsCommand(
    Guid ClientId,
    ClientIdentityInput Identity,
    ClientCareSetupInput CareSetup,
    string? InsuranceProvider,
    IReadOnlyList<ClientServiceInput> Services,
    string EditorVersion)
    : ICommand<UpdateClientBasicDetailsResult>, IRequirePermission,
        IAuditableRequest<UpdateClientBasicDetailsResult>
{
    public string RequiredPermission => Permissions.Client.Edit;

    public AuditEvent CreateAuditEvent(UpdateClientBasicDetailsResult response)
    {
        var status = response.Administration?.Status;
        var succeeded = status is ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges;
        return new(
            "Client.Updated",
            "Client",
            ClientId.ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(
                    ("changedSectionCount", (response.Administration?.ChangedSections.Count ?? 0)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("changedSections", string.Join(',', response.Administration?.ChangedSections ?? [])))
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }
}

public sealed record UpdateClientBasicDetailsResult(
    ClientAdministrationResult? Administration,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<UpdateClientBasicDetailsResult>
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Administration?.Status.ToString()
        ?? "Unavailable";

    public static UpdateClientBasicDetailsResult Completed(ClientAdministrationResult administration) =>
        new(administration, null);

    public static UpdateClientBasicDetailsResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed class UpdateClientBasicDetailsCommandHandler(IClientAdministration administration)
    : ICommandHandler<UpdateClientBasicDetailsCommand, UpdateClientBasicDetailsResult>
{
    public async ValueTask<UpdateClientBasicDetailsResult> Handle(
        UpdateClientBasicDetailsCommand command,
        CancellationToken cancellationToken) =>
        UpdateClientBasicDetailsResult.Completed(await administration.UpdateBasicDetailsAsync(
            new(
                command.ClientId,
                command.Identity,
                command.CareSetup,
                command.InsuranceProvider,
                command.Services,
                command.EditorVersion),
            cancellationToken));
}

public sealed class UpdateClientBasicDetailsCommandValidator
    : AbstractValidator<UpdateClientBasicDetailsCommand>
{
    public UpdateClientBasicDetailsCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(command => command.Identity)
            .NotNull()
            .SetValidator(new ClientIdentityInputValidator(timeProvider));
        RuleFor(command => command.CareSetup)
            .NotNull()
            .SetValidator(new ClientCareSetupInputValidator());
        RuleFor(command => command.InsuranceProvider)
            .MaximumLength(CareComply.Domain.Clients.Client.MaximumInsuranceLength);
        RuleFor(command => command.Services)
            .NotNull()
            .Must(services => services.Count <= ClientValidation.MaximumServices)
            .WithMessage($"No more than {ClientValidation.MaximumServices} active services may be selected.")
            .Must(ClientValidation.HasUniqueServiceCodes)
            .WithMessage("Duplicate service codes are not allowed.");
        RuleForEach(command => command.Services).SetValidator(new ClientServiceInputValidator());
        RuleFor(command => command.EditorVersion)
            .Must(ClientValidation.IsValidEditorVersion)
            .WithMessage("A valid editor version is required.");
    }
}
