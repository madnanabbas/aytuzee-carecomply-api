using CareComply.Application.Features.Clients;
using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using FluentValidation;

namespace CareComply.Application.Features.Clients.Queries.ListClients;

public sealed class ListClientsQueryValidator : AbstractValidator<ListClientsQuery>
{
    public ListClientsQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(ClientValidation.MaximumSearchLength);
        RuleFor(query => query.Location).MaximumLength(Client.MaximumLocationLength);
        RuleFor(query => query.ServiceCode).MaximumLength(ClientService.MaximumCodeLength);
        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);
        RuleFor(query => query.ProgramType).IsInEnum().When(query => query.ProgramType.HasValue);
        RuleFor(query => query.Sort).IsInEnum();
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, ClientValidation.MaximumPageSize);
    }
}
