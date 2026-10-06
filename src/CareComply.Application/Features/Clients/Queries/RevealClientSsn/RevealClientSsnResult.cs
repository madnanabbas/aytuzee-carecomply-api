using CareComply.Application.Authorization;

namespace CareComply.Application.Features.Clients.Queries.RevealClientSsn;

public sealed record RevealClientSsnResult(string? Ssn, AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<RevealClientSsnResult>
{
    public static RevealClientSsnResult Succeeded(string? ssn) => new(ssn, null);
    public static RevealClientSsnResult Denied(AuthorizationFailure failure) => new(null, failure);
}
