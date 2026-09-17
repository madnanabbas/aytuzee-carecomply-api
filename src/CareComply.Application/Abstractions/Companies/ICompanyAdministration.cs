namespace CareComply.Application.Abstractions.Companies;

public interface ICompanyAdministration
{
    Task<IReadOnlyList<CompanySummary>> ListAsync(CancellationToken cancellationToken);
    Task<CompanySummary?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<CompanyMutation> CreateAsync(string name, string code, CancellationToken cancellationToken);
    Task<CompanyMutation> UpdateAsync(Guid id, string name, string code, bool isActive, string editorVersion, CancellationToken cancellationToken);
    Task<CompanyMutation> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record CompanySummary(Guid Id, string Name, string Code, bool IsActive, int UserCount, int ClientCount, string EditorVersion);
public sealed record CompanyMutation(CompanyMutationStatus Status, CompanySummary? Company = null);
public enum CompanyMutationStatus { Succeeded, NotFound, DuplicateCode, Conflict, InUse, Forbidden }
