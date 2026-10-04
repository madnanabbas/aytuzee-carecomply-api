using CareComply.Application.Features.Clients;
using FluentValidation;

namespace CareComply.Application.Features.Clients.Commands.CompleteClientCreation;

public sealed class CompleteClientCreationCommandValidator
    : AbstractValidator<CompleteClientCreationCommand>
{
    public CompleteClientCreationCommandValidator()
    {
        RuleFor(command => command.ClientId).NotEmpty();
        RuleFor(command => command.EditorVersion)
            .Must(ClientValidation.IsValidEditorVersion)
            .WithMessage("Editor version is invalid.");
    }
}
