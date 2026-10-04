using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using FluentValidation;

namespace CareComply.Application.Features.Clients.Commands.CreateClientBasicDetails;

public sealed class CreateClientBasicDetailsCommandValidator
    : AbstractValidator<CreateClientBasicDetailsCommand>
{
    public CreateClientBasicDetailsCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(command => command.Identity)
            .NotNull()
            .SetValidator(new ClientIdentityInputValidator(timeProvider));
        RuleFor(command => command.CareSetup)
            .NotNull()
            .SetValidator(new ClientCareSetupInputValidator());
        RuleFor(command => command.InsuranceProvider)
            .MaximumLength(Client.MaximumInsuranceLength);
        RuleFor(command => command.Services)
            .NotNull()
            .Must(services => services.Count <= ClientValidation.MaximumServices)
            .WithMessage($"No more than {ClientValidation.MaximumServices} active services may be selected.")
            .Must(ClientValidation.HasUniqueServiceCodes)
            .WithMessage("Duplicate service codes are not allowed.");
        RuleForEach(command => command.Services).SetValidator(new ClientServiceInputValidator());
    }
}
