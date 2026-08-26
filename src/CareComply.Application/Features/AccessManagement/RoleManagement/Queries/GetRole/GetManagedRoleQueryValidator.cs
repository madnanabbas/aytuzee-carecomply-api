using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetRole;

public sealed class GetManagedRoleQueryValidator : AbstractValidator<GetManagedRoleQuery>
{
    public GetManagedRoleQueryValidator() => RuleFor(query => query.RoleId).NotEmpty();
}
