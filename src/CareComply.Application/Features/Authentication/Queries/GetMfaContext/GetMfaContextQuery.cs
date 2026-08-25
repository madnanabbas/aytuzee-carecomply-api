using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Queries.GetMfaContext;

public sealed record GetMfaContextQuery : IQuery<MfaContext>;
