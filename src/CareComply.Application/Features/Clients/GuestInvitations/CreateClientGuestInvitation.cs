using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.Clients.GuestInvitations;

public sealed record CreateClientGuestInvitationCommand(
    string ClientName,
    string Email,
    string? MobileNumber,
    Guid? CompanyId)
    : ICommand<CreateClientGuestInvitationResult>, IRequirePermission,
        IAuditableRequest<CreateClientGuestInvitationResult>
{
    public string RequiredPermission => Permissions.Client.GuestInvite;

    public AuditEvent CreateAuditEvent(CreateClientGuestInvitationResult response)
    {
        var succeeded = response.Creation?.Status == ClientGuestInvitationCreationStatus.Succeeded;
        return new(
            "ClientGuestInvitation.Created",
            "ClientGuestInvitation",
            response.Creation?.InvitationId?.ToString("N") ?? "not-created",
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Empty
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }
}

public sealed record CreateClientGuestInvitationResult(
    ClientGuestInvitationCreationResult? Creation,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<CreateClientGuestInvitationResult>
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Creation?.Status.ToString()
        ?? "Unavailable";

    public static CreateClientGuestInvitationResult Completed(ClientGuestInvitationCreationResult creation) =>
        new(creation, null);

    public static CreateClientGuestInvitationResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed class CreateClientGuestInvitationCommandHandler(IClientGuestInvitationCreator creator)
    : ICommandHandler<CreateClientGuestInvitationCommand, CreateClientGuestInvitationResult>
{
    public async ValueTask<CreateClientGuestInvitationResult> Handle(
        CreateClientGuestInvitationCommand command,
        CancellationToken cancellationToken) =>
        CreateClientGuestInvitationResult.Completed(await creator.CreateAsync(
            new(command.ClientName, command.Email, command.MobileNumber, command.CompanyId),
            cancellationToken));
}

public sealed class CreateClientGuestInvitationCommandValidator
    : AbstractValidator<CreateClientGuestInvitationCommand>
{
    public CreateClientGuestInvitationCommandValidator()
    {
        RuleFor(command => command.ClientName)
            .NotEmpty().WithMessage("Client name is required.")
            .MaximumLength(ClientGuestInvitation.MaximumClientNameLength);
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(ClientGuestInvitation.MaximumEmailLength)
            .Must(email => !string.IsNullOrWhiteSpace(email)
                && new EmailAddressAttribute().IsValid(email.Trim()))
            .WithMessage("Email must be a valid email address.");
        RuleFor(command => command.MobileNumber)
            .MaximumLength(ClientGuestInvitation.MaximumMobileNumberLength)
            .Must(mobile => string.IsNullOrWhiteSpace(mobile)
                || Regex.IsMatch(mobile.Trim(), @"^\+?[0-9 ()\-.]{7,30}$"))
            .WithMessage("Mobile number must be a valid phone number.")
            .When(command => command.MobileNumber is not null);
    }
}
