using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.Users.Queries.GetUser;

public sealed class GetUserQueryValidator : AbstractValidator<GetUserQuery>
{
    public GetUserQueryValidator() => RuleFor(query => query.UserId).NotEmpty();
}
