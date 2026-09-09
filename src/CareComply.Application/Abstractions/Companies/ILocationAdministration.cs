namespace CareComply.Application.Abstractions.Companies;

public interface ILocationAdministration
{
    Task<IReadOnlyList<CompanyLocationItem>> ListAsync(Guid? companyId, CancellationToken cancellationToken);
    Task<CompanyLocationItem?> CreateAsync(Guid? companyId, string name, CancellationToken cancellationToken);
}
public sealed record CompanyLocationItem(Guid Id, Guid CompanyId, string Name, bool IsMain, bool IsActive);
