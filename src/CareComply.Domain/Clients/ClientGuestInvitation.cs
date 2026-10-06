namespace CareComply.Domain.Clients;

public sealed class ClientGuestInvitation
{
    public const int MaximumClientNameLength = 200;
    public const int MaximumEmailLength = 320;
    public const int MaximumMobileNumberLength = 30;

    private ClientGuestInvitation() { }

    private ClientGuestInvitation(
        Guid id,
        Guid companyId,
        string clientName,
        string email,
        string? mobileNumber,
        byte[] tokenHash,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        if (id == Guid.Empty || companyId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Invitation identifiers are required.");
        if (tokenHash.Length != 32) throw new ArgumentException("A SHA-256 token hash is required.", nameof(tokenHash));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAtUtc, createdAtUtc);

        Id = id;
        CompanyId = companyId;
        ClientName = NormalizeRequired(clientName, MaximumClientNameLength, nameof(clientName));
        Email = NormalizeRequired(email, MaximumEmailLength, nameof(email)).ToLowerInvariant();
        MobileNumber = NormalizeOptional(mobileNumber, MaximumMobileNumberLength, nameof(mobileNumber));
        TokenHash = tokenHash.ToArray();
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string ClientName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? MobileNumber { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public DateTimeOffset? OpenedAtUtc { get; private set; }
    public DateTimeOffset? RedeemedAtUtc { get; private set; }
    public Guid? CreatedClientId { get; private set; }
    public DateTimeOffset? ClientCreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ClientGuestInvitation Create(
        Guid id,
        Guid companyId,
        string clientName,
        string email,
        string? mobileNumber,
        byte[] tokenHash,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId) =>
        new(id, companyId, clientName, email, mobileNumber, tokenHash, expiresAtUtc, createdAtUtc, createdByUserId);

    public bool CanSend(DateTimeOffset nowUtc) =>
        RevokedAtUtc is null && CompletedAtUtc is null && nowUtc < ExpiresAtUtc;

    public void MarkSent(DateTimeOffset sentAtUtc)
    {
        if (!CanSend(sentAtUtc)) throw new InvalidOperationException("The invitation cannot be sent.");
        SentAtUtc = sentAtUtc;
    }

    public void MarkOpened(DateTimeOffset openedAtUtc) => OpenedAtUtc = openedAtUtc;

    public void Redeem(DateTimeOffset redeemedAtUtc)
    {
        if (RedeemedAtUtc is not null) throw new InvalidOperationException("The invitation has already been redeemed.");
        RedeemedAtUtc = redeemedAtUtc;
        OpenedAtUtc ??= redeemedAtUtc;
    }

    public void BindCreatedClient(Guid clientId, DateTimeOffset createdAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(clientId, Guid.Empty);
        CreatedClientId = clientId;
        ClientCreatedAtUtc = createdAtUtc;
    }

    public void Complete(DateTimeOffset completedAtUtc) => CompletedAtUtc = completedAtUtc;

    public void Revoke(DateTimeOffset revokedAtUtc) => RevokedAtUtc = revokedAtUtc;

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
