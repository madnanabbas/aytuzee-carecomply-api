using CareComply.Application.Abstractions.Identity;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Identity;

internal sealed class ConfiguredPasswordResetLinkFactory(
    IOptions<PasswordRecoveryOptions> options) : IPasswordResetLinkFactory
{
    public string? Create(Guid userId, string urlSafeToken)
    {
        if (!TryGetTrustedBaseUri(options.Value.FrontendBaseUrl, out var baseUri))
        {
            return null;
        }

        var builder = new UriBuilder(baseUri)
        {
            Path = $"{baseUri.AbsolutePath.TrimEnd('/')}/auth/reset-password",
            Query = $"userId={Uri.EscapeDataString(userId.ToString())}&token={Uri.EscapeDataString(urlSafeToken)}",
        };

        return builder.Uri.AbsoluteUri;
    }

    private static bool TryGetTrustedBaseUri(string value, out Uri baseUri)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            || !string.IsNullOrEmpty(candidate.UserInfo)
            || !string.IsNullOrEmpty(candidate.Query)
            || !string.IsNullOrEmpty(candidate.Fragment)
            || (candidate.Scheme != Uri.UriSchemeHttps && !IsLocalHttp(candidate)))
        {
            baseUri = null!;
            return false;
        }

        baseUri = candidate;
        return true;
    }

    private static bool IsLocalHttp(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp
        && (uri.IsLoopback
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
}
