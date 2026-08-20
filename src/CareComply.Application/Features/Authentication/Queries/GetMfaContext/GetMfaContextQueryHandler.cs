using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Queries.GetMfaContext;

public sealed class GetMfaContextQueryHandler(IIdentityMfa identityMfa)
    : IQueryHandler<GetMfaContextQuery, MfaContextKind>
{
    public async ValueTask<MfaContextKind> Handle(
        GetMfaContextQuery query,
        CancellationToken cancellationToken) =>
        await identityMfa.GetContextAsync(cancellationToken);
}
