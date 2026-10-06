using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Queries.GetMfaContext;

public sealed class GetMfaContextQueryHandler(IIdentityMfa identityMfa)
    : IQueryHandler<GetMfaContextQuery, MfaContext>
{
    public async ValueTask<MfaContext> Handle(
        GetMfaContextQuery query,
        CancellationToken cancellationToken) =>
        await identityMfa.GetContextAsync(cancellationToken);
}
