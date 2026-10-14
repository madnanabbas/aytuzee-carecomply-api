using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CareComply.Api.IntegrationTests;

public sealed class ClientServiceAgreementPersistenceModelTests
{
    [Fact]
    public void ConfiguresClientAgreementAndAgreementLineRelationshipsAsRestrictedHistory()
    {
        var options = new DbContextOptionsBuilder<CareComplyDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CareComplyModelTest")
            .Options;
        using var context = new CareComplyDbContext(options);

        var model = context.GetService<IDesignTimeModel>().Model;
        var agreementType = model.FindEntityType(typeof(ClientServiceAgreement));
        var agreementForeignKeys = agreementType!.GetForeignKeys().ToArray();
        Assert.Equal(3, agreementForeignKeys.Length);
        Assert.Equal(
            [typeof(Client), typeof(ClientCoverage), typeof(CareComply.Domain.Companies.Payer)],
            agreementForeignKeys
                .Select(foreignKey => foreignKey.PrincipalEntityType.ClrType)
                .OrderBy(type => type.Name)
                .ToArray());
        Assert.All(agreementForeignKeys, foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));

        var lineType = model.FindEntityType(typeof(ClientServiceAgreementLine));
        var lineForeignKey = Assert.Single(lineType!.GetForeignKeys());
        Assert.Equal(
            typeof(ClientServiceAgreement),
            lineForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, lineForeignKey.DeleteBehavior);

        var diagnosisType = model.FindEntityType(
            typeof(ClientServiceAgreementLineDiagnosis));
        var diagnosisForeignKey = Assert.Single(diagnosisType!.GetForeignKeys());
        Assert.Equal(typeof(ClientServiceAgreementLine), diagnosisForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, diagnosisForeignKey.DeleteBehavior);
        var primaryIndex = Assert.Single(diagnosisType.GetIndexes());
        Assert.True(primaryIndex.IsUnique);
        Assert.Equal("[IsPrimary] = 1", primaryIndex.GetFilter());

        Assert.Contains(agreementType.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_ClientServiceAgreements_Status");
        Assert.Contains(agreementType.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_ClientServiceAgreements_Priority");
        Assert.Contains(lineType.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_ClientServiceAgreementLines_UnitType");
    }
}
