using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.Clients.GuestInvitations;

public sealed record SendClientGuestInvitationCommand(Guid InvitationId, string Token)
    : ICommand<SendClientGuestInvitationResult>, IRequirePermission,
        IAuditableRequest<SendClientGuestInvitationResult>
{
    public string RequiredPermission => Permissions.Client.GuestInvite;

    public AuditEvent CreateAuditEvent(SendClientGuestInvitationResult response)
    {
        var succeeded = response.Send?.Status == ClientGuestInvitationSendStatus.Succeeded;
        return new(
            "ClientGuestInvitation.Sent",
            "ClientGuestInvitation",
            InvitationId.ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded ? AuditMetadata.Empty : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }

    public override string ToString() =>
        $"SendClientGuestInvitationCommand {{ InvitationId = {InvitationId}, Token = [REDACTED] }}";
}

public sealed record SendClientGuestInvitationResult(
    ClientGuestInvitationSendResult? Send,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<SendClientGuestInvitationResult>
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Send?.Status.ToString()
        ?? "Unavailable";

    public static SendClientGuestInvitationResult Completed(ClientGuestInvitationSendResult send) =>
        new(send, null);

    public static SendClientGuestInvitationResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed class SendClientGuestInvitationCommandHandler(IClientGuestInvitationSender sender)
    : ICommandHandler<SendClientGuestInvitationCommand, SendClientGuestInvitationResult>
{
    public async ValueTask<SendClientGuestInvitationResult> Handle(
        SendClientGuestInvitationCommand command,
        CancellationToken cancellationToken) =>
        SendClientGuestInvitationResult.Completed(await sender.SendAsync(
            command.InvitationId,
            command.Token,
            cancellationToken));
}

public sealed class SendClientGuestInvitationCommandValidator
    : AbstractValidator<SendClientGuestInvitationCommand>
{
    public SendClientGuestInvitationCommandValidator()
    {
        RuleFor(command => command.InvitationId).NotEmpty();
        RuleFor(command => command.Token)
            .NotEmpty().WithMessage("Invitation token is required.")
            .MaximumLength(128)
            .Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("Invitation token is invalid.");
    }
}
