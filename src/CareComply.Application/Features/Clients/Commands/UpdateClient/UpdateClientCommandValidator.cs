using CareComply.Application.Features.Clients;
using CareComply.Application.Abstractions.Clients;
using FluentValidation;

namespace CareComply.Application.Features.Clients.Commands.UpdateClient;

public sealed class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientCommandValidator(TimeProvider timeProvider)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        RuleFor(command => command.ClientId).NotEmpty();
        RuleFor(command => command.Identity).NotNull().SetValidator(new ClientIdentityInputValidator(timeProvider));
        RuleFor(command => command.Contact).NotNull().SetValidator(new ClientContactInputValidator());
        RuleFor(command => command.CareSetup).NotNull().SetValidator(new ClientCareSetupInputValidator());
        RuleFor(command => command.Safety).NotNull().SetValidator(new ClientSafetyInputValidator());
        RuleFor(command => command.AdditionalProfile).NotNull().SetValidator(new ClientAdditionalProfileInputValidator());
        RuleFor(command => command.Services)
            .NotNull()
            .Must(services => services.Count <= ClientValidation.MaximumServices)
            .WithMessage($"No more than {ClientValidation.MaximumServices} active services may be selected.")
            .Must(ClientValidation.HasUniqueServiceCodes)
            .WithMessage("Duplicate service codes are not allowed.");
        RuleForEach(command => command.Services).SetValidator(new ClientServiceInputValidator());
        RuleFor(command => command.Languages)
            .NotNull()
            .Must(languages => languages.Count <= ClientValidation.MaximumLanguages)
            .WithMessage($"No more than {ClientValidation.MaximumLanguages} languages may be selected.")
            .Must(ClientValidation.HasUniqueLanguageCodes)
            .WithMessage("Duplicate language codes are not allowed.")
            .Must(ClientValidation.HasExactlyOnePrimaryLanguage)
            .WithMessage("Exactly one preferred language must be marked as primary.");
        RuleForEach(command => command.Languages).SetValidator(new ClientLanguageInputValidator());
        RuleFor(command => command.ServiceChangeEffectiveDate)
            .Must((command, effectiveDate) =>
                !effectiveDate.HasValue || effectiveDate.Value >= command.CareSetup.SocDate)
            .WithMessage("Service change effective date cannot be before the SOC date.");
        RuleFor(command => command.ServiceChangeEffectiveDate)
            .LessThanOrEqualTo(today)
            .When(command => command.ServiceChangeEffectiveDate.HasValue)
            .WithMessage("Future service changes are not supported by the current active-service endpoint.");
        RuleFor(command => command.EditorVersion)
            .Must(ClientValidation.IsValidEditorVersion)
            .WithMessage("Editor version is invalid.");
    }
}
