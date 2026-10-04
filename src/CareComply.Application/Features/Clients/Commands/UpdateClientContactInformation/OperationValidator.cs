using CareComply.Application.Features.Clients;
using FluentValidation;

namespace CareComply.Application.Features.Clients.Commands.UpdateClientContactInformation;

public sealed class UpdateClientContactInformationCommandValidator
    : AbstractValidator<UpdateClientContactInformationCommand>
{
    public UpdateClientContactInformationCommandValidator()
    {
        RuleFor(command => command.ClientId).NotEmpty();
        RuleFor(command => command.Contact).NotNull().SetValidator(new ClientContactInputValidator());
        RuleFor(command => command.Languages)
            .NotNull()
            .Must(languages => languages.Count <= ClientValidation.MaximumLanguages)
            .WithMessage($"No more than {ClientValidation.MaximumLanguages} languages may be selected.")
            .Must(ClientValidation.HasUniqueLanguageCodes)
            .WithMessage("Duplicate language codes are not allowed.")
            .Must(ClientValidation.HasExactlyOnePrimaryLanguage)
            .WithMessage("Exactly one preferred language must be marked as primary.");
        RuleForEach(command => command.Languages).SetValidator(new ClientLanguageInputValidator());
        RuleFor(command => command.EditorVersion)
            .Must(ClientValidation.IsValidEditorVersion)
            .WithMessage("Editor version is invalid.");
    }
}
