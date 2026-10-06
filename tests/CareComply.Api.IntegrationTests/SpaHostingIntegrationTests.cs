using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CareComply.Api.IntegrationTests;

public sealed class SpaHostingIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task PublishedAngularRouteReturnsIndexAndUnknownApiRemainsNotFound()
    {
        using var webRoot = new TemporaryWebRoot();
        await File.WriteAllTextAsync(
            Path.Combine(webRoot.Path, "index.html"),
            "<!doctype html><html><body>CareComply staging shell</body></html>",
            CancellationToken.None);

        using var hostingFactory = factory.WithWebHostBuilder(builder =>
            builder.UseWebRoot(webRoot.Path));
        using var client = hostingFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using var angularResponse = await client.GetAsync(
            "/auth/reset-password?userId=opaque&token=opaque",
            CancellationToken.None);
        using var apiResponse = await client.GetAsync(
            "/api/route-that-does-not-exist",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, angularResponse.StatusCode);
        Assert.Equal("text/html", angularResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "CareComply staging shell",
            await angularResponse.Content.ReadAsStringAsync(
                CancellationToken.None),
            StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, apiResponse.StatusCode);
    }

    [Fact]
    public async Task RuntimeConfigurationIsServedWithoutCaching()
    {
        using var webRoot = new TemporaryWebRoot();
        var configDirectory = Directory.CreateDirectory(Path.Combine(webRoot.Path, "config"));
        await File.WriteAllTextAsync(
            Path.Combine(configDirectory.FullName, "app-config.json"),
            "{\"apiBaseUrl\":\"/api\"}",
            CancellationToken.None);

        using var hostingFactory = factory.WithWebHostBuilder(builder =>
            builder.UseWebRoot(webRoot.Path));
        using var client = hostingFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using var response = await client.GetAsync(
            "/config/app-config.json",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.Contains("no-cache", response.Headers.Pragma.ToString(), StringComparison.Ordinal);
    }

    private sealed class TemporaryWebRoot : IDisposable
    {
        public TemporaryWebRoot()
        {
            Path = Directory.CreateTempSubdirectory("carecomply-spa-").FullName;
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
