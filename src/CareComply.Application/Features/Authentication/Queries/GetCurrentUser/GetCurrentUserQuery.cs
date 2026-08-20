using Mediator;

namespace CareComply.Application.Features.Authentication.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery : IQuery<CurrentUser?>;
