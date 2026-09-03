using CareComply.Application.Authorization;

namespace CareComply.Application.Features.Clients.Queries.RevealClientSsn;

public sealed record RevealClientSsnResult(string? Ssn, AuthorizationFailure? AuthorizationFailure)
{
    public static RevealClientSsnResult Succeeded(string? ssn) => new(ssn, null);
    public static RevealClientSsnResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed class RevealClientSsnAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<RevealClientSsnResult>
{
    public RevealClientSsnResult Create(AuthorizationFailure failure) => RevealClientSsnResult.Denied(failure);
}
