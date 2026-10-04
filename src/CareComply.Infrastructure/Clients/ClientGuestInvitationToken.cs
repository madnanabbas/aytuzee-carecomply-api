using System.Security.Cryptography;

namespace CareComply.Infrastructure.Clients;

internal static class ClientGuestInvitationToken
{
    public const int ByteLength = 32;

    public static (string PublicToken, byte[] Hash) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(ByteLength);
        return (Base64UrlEncode(bytes), SHA256.HashData(bytes));
    }

    public static byte[] DecodeAndHash(string publicToken) =>
        SHA256.HashData(Base64UrlDecode(publicToken));

    public static bool TryHash(string publicToken, out byte[] hash)
    {
        try
        {
            var bytes = Base64UrlDecode(publicToken);
            if (bytes.Length != ByteLength)
            {
                hash = [];
                return false;
            }
            hash = SHA256.HashData(bytes);
            return true;
        }
        catch (FormatException)
        {
            hash = [];
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded,
        };
        return Convert.FromBase64String(padded);
    }
}
