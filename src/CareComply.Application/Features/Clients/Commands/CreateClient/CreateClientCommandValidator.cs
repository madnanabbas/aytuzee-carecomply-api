using CareComply.Application.Features.Clients;
using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using FluentValidation;

namespace CareComply.Application.Features.Clients.Commands.CreateClient;

public sealed class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(command => command.Identity).NotNull().SetValidator(new ClientIdentityInputValidator(timeProvider));
        RuleFor(command => command.Contact).NotNull().SetValidator(new ClientContactInputValidator());
        RuleFor(command => command.CareSetup).NotNull().SetValidator(new ClientCareSetupInputValidator());
        RuleFor(command => command.Safety).NotNull().SetValidator(new ClientSafetyInputValidator());
        RuleFor(command => command.AdditionalProfile).NotNull().SetValidator(new ClientAdditionalProfileInputValidator());
        RuleFor(command => command.InitialStatus).IsInEnum();
        RuleFor(command => command.DischargeDate)
            .NotNull()
            .When(command => command.InitialStatus == ClientStatus.Discharged)
            .WithMessage("Discharge date is required for a discharged client.");
        RuleFor(command => command.DischargeReason)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .When(command => command.InitialStatus == ClientStatus.Discharged)
            .WithMessage("Discharge reason is required for a discharged client.")
            .MaximumLength(Client.MaximumTextLength);
        RuleFor(command => command.DischargeDate)
            .Must((command, dischargeDate) =>
                !dischargeDate.HasValue || dischargeDate.Value >= command.CareSetup.SocDate)
            .WithMessage("Discharge date cannot be before the SOC date.");
        RuleFor(command => command.DischargeDate)
            .Null()
            .When(command => command.InitialStatus != ClientStatus.Discharged)
            .WithMessage("Discharge date is only valid when the client is discharged.");
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
    }
}
