using CareComply.Infrastructure;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class DatabaseFoundationTests
{
    [Fact]
    public void AddInfrastructureThrowsWhenConnectionStringIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(configuration));

        Assert.Equal(
            "The required connection string 'CareComply' is not configured.",
            exception.Message);
    }

    [Fact]
    public void AddInfrastructureConfiguresSqlServerContextWithConnectionString()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();

        services.AddInfrastructure(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
        Assert.True(context.Database.CreateExecutionStrategy().RetriesOnFailure);
        Assert.Equal("dbo", context.Model.GetDefaultSchema());
    }

    private static IConfiguration CreateConfiguration()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:CareComply"] =
                "Server=example.invalid;Database=CareComplyTests;Integrated Security=True;Encrypt=True;TrustServerCertificate=False",
            ["FormContentStorage:LocalRootPath"] = Path.GetTempPath(),
            ["FormContentStorage:ClientDocumentLocalRootPath"] = Path.GetTempPath(),
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }
}
