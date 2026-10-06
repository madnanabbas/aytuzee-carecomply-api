using CareComply.Application.Features.Clients;
using CareComply.Domain.Clients;
using FluentValidation;

namespace CareComply.Application.Features.Clients.Commands.ChangeClientStatus;

public sealed class ChangeClientStatusCommandValidator : AbstractValidator<ChangeClientStatusCommand>
{
    public ChangeClientStatusCommandValidator()
    {
        RuleFor(command => command.ClientId).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.DischargeDate)
            .NotNull()
            .When(command => command.Status == ClientStatus.Discharged)
            .WithMessage("Discharge date is required for a discharged client.");
        RuleFor(command => command.DischargeReason)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .When(command => command.Status == ClientStatus.Discharged)
            .WithMessage("Discharge reason is required for a discharged client.")
            .MaximumLength(Client.MaximumTextLength);
        RuleFor(command => command.DischargeDate)
            .Null()
            .When(command => command.Status != ClientStatus.Discharged)
            .WithMessage("Discharge date is only valid when the client is discharged.");
        RuleFor(command => command.EditorVersion)
            .Must(ClientValidation.IsValidEditorVersion)
            .WithMessage("Editor version is invalid.");
    }
}
