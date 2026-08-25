using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.Users.Queries.ListUsers;

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    private static readonly string[] MfaStatuses = ["enabled", "notEnabled"];
    private static readonly string[] SortFields = ["name", "email", "status", "mfa"];
    private static readonly string[] SortDirections = ["asc", "desc"];

    public ListUsersQueryValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(200);
        RuleFor(query => query.MfaStatus)
            .Must(value => IsOptionalMember(value, MfaStatuses))
            .WithMessage("MFA status must be enabled or notEnabled.");
        RuleFor(query => query.SortBy)
            .Must(value => IsOptionalMember(value, SortFields))
            .WithMessage("Sort field is not supported.");
        RuleFor(query => query.SortDirection)
            .Must(value => IsOptionalMember(value, SortDirections))
            .WithMessage("Sort direction must be asc or desc.");
        RuleFor(query => query.PageNumber)
            .InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }

    private static bool IsOptionalMember(string? value, IEnumerable<string> members) =>
        string.IsNullOrWhiteSpace(value)
        || members.Contains(value, StringComparer.OrdinalIgnoreCase);
}
