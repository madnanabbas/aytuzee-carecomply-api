using CareComply.Application.Abstractions.Employees;
using Microsoft.AspNetCore.DataProtection;

namespace CareComply.Infrastructure.Employees;

internal sealed class DataProtectionEmployeeSensitiveData(
    IDataProtectionProvider dataProtectionProvider) : IEmployeeSensitiveData
{
    private readonly IDataProtector ssnProtector = dataProtectionProvider.CreateProtector(
        "CareComply.Employee.SSN.v1");
    private readonly IDataProtector workforceSecretProtector = dataProtectionProvider.CreateProtector(
        "CareComply.Employee.EVV.v1");

    public string ProtectSsn(string ssn) => ssnProtector.Protect(ssn);

    public string ProtectWorkforceSecret(string secret) => workforceSecretProtector.Protect(secret);
}
