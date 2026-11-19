using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Abstractions.ClientDocuments;
using CareComply.Infrastructure;
using CareComply.Infrastructure.Forms;
using CareComply.Infrastructure.ClientDocuments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class FormContentStoreRegistrationTests
{
    [Fact]
    public void SelectsLocalProviderByConfiguration()
    {
        using var provider = CreateServices(new Dictionary<string, string?>
        {
            ["FormContentStorage:Provider"] = "Local",
            ["FormContentStorage:LocalRootPath"] = Path.GetTempPath(),
            ["FormContentStorage:ClientDocumentLocalRootPath"] = Path.GetTempPath(),
        }).BuildServiceProvider();

        Assert.IsType<LocalFormContentStore>(provider.GetRequiredService<IFormContentStore>());
        Assert.IsType<LocalClientDocumentContentStore>(provider.GetRequiredService<IClientDocumentContentStore>());
    }

    [Fact]
    public void SelectsAzureBlobProviderWithoutContactingAzure()
    {
        using var provider = CreateServices(new Dictionary<string, string?>
        {
            ["FormContentStorage:Provider"] = "AzureBlob",
            ["FormContentStorage:AzureBlob:AccountEndpoint"] = "https://synthetic.blob.core.windows.net",
            ["FormContentStorage:AzureBlob:ContainerName"] = "form-templates",
            ["FormContentStorage:AzureBlob:ClientDocumentContainerName"] = "client-documents",
        }).BuildServiceProvider();

        Assert.IsType<AzureBlobFormContentStore>(provider.GetRequiredService<IFormContentStore>());
        Assert.IsType<AzureBlobClientDocumentContentStore>(provider.GetRequiredService<IClientDocumentContentStore>());
    }

    [Fact]
    public void MissingClientDocumentLocalRootFailsClosedWithoutEchoingPaths()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateServices(new()
        {
            ["FormContentStorage:Provider"] = "Local",
            ["FormContentStorage:LocalRootPath"] = @"C:\synthetic\forms",
        }));
        Assert.DoesNotContain("synthetic", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingClientDocumentAzureContainerFailsClosedWithoutEchoingEndpoint()
    {
        const string endpoint = "https://synthetic.blob.core.windows.net";
        var exception = Assert.Throws<InvalidOperationException>(() => CreateServices(new()
        {
            ["FormContentStorage:Provider"] = "AzureBlob",
            ["FormContentStorage:AzureBlob:AccountEndpoint"] = endpoint,
            ["FormContentStorage:AzureBlob:ContainerName"] = "form-templates",
        }));
        Assert.DoesNotContain(endpoint, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Unsupported", "", "")]
    [InlineData("AzureBlob", "", "form-templates")]
    [InlineData("AzureBlob", "http://account.example", "form-templates")]
    [InlineData("AzureBlob", "https://account.example?secret=value", "form-templates")]
    [InlineData("AzureBlob", "https://account.example/unexpected", "form-templates")]
    [InlineData("AzureBlob", "https://account.example", "Form-Templates")]
    [InlineData("AzureBlob", "https://account.example", "")]
    public void InvalidConfigurationFailsClosedWithoutEchoingValues(
        string provider,
        string endpoint,
        string container)
    {
        var values = new Dictionary<string, string?>
        {
            ["FormContentStorage:Provider"] = provider,
            ["FormContentStorage:AzureBlob:AccountEndpoint"] = endpoint,
            ["FormContentStorage:AzureBlob:ContainerName"] = container,
            ["FormContentStorage:AzureBlob:ClientDocumentContainerName"] = container,
        };

        var exception = Assert.Throws<InvalidOperationException>(() => CreateServices(values));

        if (endpoint.Length > 0)
            Assert.DoesNotContain(endpoint, exception.Message, StringComparison.Ordinal);
        if (container.Length > 0)
            Assert.DoesNotContain(container, exception.Message, StringComparison.Ordinal);
    }

    private static ServiceCollection CreateServices(Dictionary<string, string?> values)
    {
        values["ConnectionStrings:CareComply"] =
            "Server=127.0.0.1,1433;Database=Synthetic;User Id=synthetic;Password=not-used;TrustServerCertificate=True";
        values["ClientGuestInvitation:PublicClientIntakeUrl"] = "https://localhost/client-intake";
        values["Authentication:Mfa:Issuer"] = "CareComply";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services;
    }
}
