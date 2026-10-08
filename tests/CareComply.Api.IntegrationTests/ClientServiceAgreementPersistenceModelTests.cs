using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

        var agreementType = context.Model.FindEntityType(typeof(ClientServiceAgreement));
        var agreementForeignKey = Assert.Single(agreementType!.GetForeignKeys());
        Assert.Equal(typeof(Client), agreementForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, agreementForeignKey.DeleteBehavior);

        var lineType = context.Model.FindEntityType(typeof(ClientServiceAgreementLine));
        var lineForeignKey = Assert.Single(lineType!.GetForeignKeys());
        Assert.Equal(
            typeof(ClientServiceAgreement),
            lineForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, lineForeignKey.DeleteBehavior);
    }
}
