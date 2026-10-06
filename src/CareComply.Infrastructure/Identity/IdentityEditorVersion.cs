using System.Security.Cryptography;
using System.Text;

namespace CareComply.Infrastructure.Identity;

internal static class IdentityEditorVersion
{
    public static string Create(string concurrencyStamp)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(concurrencyStamp));
        return Convert.ToHexString(digest);
    }

    public static bool Matches(string concurrencyStamp, string editorVersion)
    {
        var expected = Encoding.ASCII.GetBytes(Create(concurrencyStamp));
        var supplied = Encoding.ASCII.GetBytes(editorVersion.ToUpperInvariant());
        return expected.Length == supplied.Length
            && CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}
