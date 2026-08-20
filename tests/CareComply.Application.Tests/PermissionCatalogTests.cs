using System.Text.RegularExpressions;
using CareComply.Application.Authorization;

namespace CareComply.Application.Tests;

public sealed partial class PermissionCatalogTests
{
    [Fact]
    public void CatalogMatchesDocumentationAndFrontendExactly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var documentedPermissions = DocumentedPermissionValue()
            .Matches(File.ReadAllText(Path.Combine(repositoryRoot, "docs", "04-permissions.md")))
            .Select(match => match.Groups[1].Value)
            .ToArray();
        var frontendPermissions = FrontendPermissionValue()
            .Matches(
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "frontend",
                "src",
                "app",
                "core",
                "authorization",
                "permissions.ts")))
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(48, Permissions.All.Count);
        Assert.Equal(48, Permissions.All.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            Permissions.All.Order(StringComparer.Ordinal),
            documentedPermissions.Order(StringComparer.Ordinal));
        Assert.Equal(
            Permissions.All.Order(StringComparer.Ordinal),
            frontendPermissions.Order(StringComparer.Ordinal));
        Assert.All(
            Permissions.All,
            permission => Assert.Matches(PermissionFormat(), permission));
        Assert.False(Permissions.IsDefined("Client.Delete"));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "04-permissions.md"))
                && File.Exists(Path.Combine(
                    directory.FullName,
                    "frontend",
                    "src",
                    "app",
                    "core",
                    "authorization",
                    "permissions.ts")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The CareComply repository root could not be found.");
    }

    [GeneratedRegex("^- ([A-Z][A-Za-z]*\\.[A-Z][A-Za-z]*)\\r?$", RegexOptions.Multiline)]
    private static partial Regex DocumentedPermissionValue();

    [GeneratedRegex("'([A-Z][A-Za-z]*\\.[A-Z][A-Za-z]*)'")]
    private static partial Regex FrontendPermissionValue();

    [GeneratedRegex("^[A-Z][A-Za-z]*\\.[A-Z][A-Za-z]*$")]
    private static partial Regex PermissionFormat();
}
