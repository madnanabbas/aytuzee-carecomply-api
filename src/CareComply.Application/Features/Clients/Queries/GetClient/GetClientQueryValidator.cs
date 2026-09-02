using FluentValidation;

namespace CareComply.Application.Features.Clients.Queries.GetClient;

public sealed class GetClientQueryValidator : AbstractValidator<GetClientQuery>
{
    public GetClientQueryValidator() => RuleFor(query => query.ClientId).NotEmpty();
}
