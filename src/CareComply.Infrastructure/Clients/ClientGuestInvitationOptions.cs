namespace CareComply.Infrastructure.Clients;

public sealed class ClientGuestInvitationOptions
{
    public const string SectionName = "ClientGuestInvitation";

    public string PublicClientIntakeUrl { get; init; } = string.Empty;
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(7);
    public TimeSpan GuestSessionInactivityTimeout { get; init; } = TimeSpan.FromMinutes(60);
    public TimeSpan GuestSessionAbsoluteLifetime { get; init; } = TimeSpan.FromHours(8);

    public bool IsValid() => Lifetime > TimeSpan.Zero
        && GuestSessionInactivityTimeout > TimeSpan.Zero
        && GuestSessionAbsoluteLifetime >= GuestSessionInactivityTimeout
        && Uri.TryCreate(PublicClientIntakeUrl, UriKind.Absolute, out var uri)
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && (uri.Scheme == Uri.UriSchemeHttps
            || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));

    public bool TryBuildGuestLink(string publicToken, out string guestLink)
    {
        if (!IsValid())
        {
            guestLink = string.Empty;
            return false;
        }
        guestLink = new UriBuilder(PublicClientIntakeUrl)
        {
            Fragment = $"token={publicToken}",
        }.Uri.AbsoluteUri;
        return true;
    }
}
