namespace CareComply.Api.Contracts.Companies;

public sealed record CompanyResponse(Guid Id, string Name, string Code, bool IsActive, int UserCount, int ClientCount, string EditorVersion);
public sealed record CreateCompanyRequest(string Name, string Code);
public sealed record UpdateCompanyRequest(string Name, string Code, bool IsActive, string EditorVersion);
