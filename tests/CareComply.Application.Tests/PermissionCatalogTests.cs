using System.Text.RegularExpressions;
using CareComply.Application.Authorization;

namespace CareComply.Application.Tests;

public sealed partial class PermissionCatalogTests
{
    [Fact]
    public void CatalogContainsExpectedUniqueValidPermissions()
    {
        Assert.Equal(
            84,
            Permissions.All.Count);

        Assert.Equal(
            84,
            Permissions.All
                .Distinct(StringComparer.Ordinal)
                .Count());

        Assert.All(
            Permissions.All,
            permission =>
                Assert.Matches(
                    PermissionFormat(),
                    permission));

        Assert.False(
            Permissions.IsDefined("Client.Delete"));
    }

    [GeneratedRegex(
        "^[A-Z][A-Za-z]*\\.[A-Z][A-Za-z]*$")]
    private static partial Regex PermissionFormat();
}
