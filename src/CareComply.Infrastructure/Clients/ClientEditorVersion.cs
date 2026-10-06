using System.Security.Cryptography;

namespace CareComply.Infrastructure.Clients;

internal static class ClientEditorVersion
{
    public static string Create(byte[] rowVersion)
    {
        ArgumentNullException.ThrowIfNull(rowVersion);
        var digest = SHA256.HashData(rowVersion);
        return Convert.ToHexString(digest);
    }

    public static bool Matches(byte[] rowVersion, string editorVersion)
    {
        ArgumentNullException.ThrowIfNull(rowVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(editorVersion);

        var expected = System.Text.Encoding.ASCII.GetBytes(Create(rowVersion));
        var supplied = System.Text.Encoding.ASCII.GetBytes(editorVersion.ToUpperInvariant());
        return expected.Length == supplied.Length
            && CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}
