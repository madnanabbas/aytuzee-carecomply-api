using CareComply.Application.Abstractions.Clients;
using Mediator;

namespace CareComply.Application.Features.Clients.GuestInvitations;

public sealed record ResolvePublicClientGuestInvitationQuery(string Token)
    : IQuery<PublicClientGuestInvitationResult>;

public sealed class ResolvePublicClientGuestInvitationQueryHandler(
    IClientGuestInvitationPublicResolver resolver)
    : IQueryHandler<ResolvePublicClientGuestInvitationQuery, PublicClientGuestInvitationResult>
{
    public ValueTask<PublicClientGuestInvitationResult> Handle(
        ResolvePublicClientGuestInvitationQuery query,
        CancellationToken cancellationToken) =>
        new(resolver.ResolveAsync(query.Token, cancellationToken));
}
