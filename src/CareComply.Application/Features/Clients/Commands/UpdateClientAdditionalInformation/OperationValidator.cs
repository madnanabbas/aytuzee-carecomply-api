using CareComply.Application.Features.Clients;
using FluentValidation;

namespace CareComply.Application.Features.Clients.Commands.UpdateClientAdditionalInformation;

public sealed class UpdateClientAdditionalInformationCommandValidator
    : AbstractValidator<UpdateClientAdditionalInformationCommand>
{
    public UpdateClientAdditionalInformationCommandValidator()
    {
        RuleFor(command => command.ClientId).NotEmpty();
        RuleFor(command => command.AdditionalProfile)
            .NotNull()
            .SetValidator(new ClientAdditionalProfileInputValidator());
        RuleFor(command => command.EditorVersion)
            .Must(ClientValidation.IsValidEditorVersion)
            .WithMessage("Editor version is invalid.");
    }
}
