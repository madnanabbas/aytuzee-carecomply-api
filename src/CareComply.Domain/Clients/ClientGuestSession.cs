namespace CareComply.Domain.Clients;

public sealed class ClientGuestSession
{
    private ClientGuestSession() { }

    private ClientGuestSession(
        Guid id,
        Guid invitationId,
        byte[] sessionTokenHash,
        byte[] csrfTokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (id == Guid.Empty || invitationId == Guid.Empty)
            throw new ArgumentException("Session identifiers are required.");
        if (sessionTokenHash.Length != 32 || csrfTokenHash.Length != 32)
            throw new ArgumentException("SHA-256 credential hashes are required.");
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAtUtc, createdAtUtc);
        Id = id;
        ClientGuestInvitationId = invitationId;
        SessionTokenHash = sessionTokenHash.ToArray();
        CsrfTokenHash = csrfTokenHash.ToArray();
        CreatedAtUtc = createdAtUtc;
        LastActivityAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ClientGuestInvitationId { get; private set; }
    public byte[] SessionTokenHash { get; private set; } = [];
    public byte[] CsrfTokenHash { get; private set; } = [];
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastActivityAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ClientGuestSession Create(
        Guid id,
        Guid invitationId,
        byte[] sessionTokenHash,
        byte[] csrfTokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc) =>
        new(id, invitationId, sessionTokenHash, csrfTokenHash, createdAtUtc, expiresAtUtc);

    public void Touch(DateTimeOffset nowUtc) => LastActivityAtUtc = nowUtc;
    public void Complete(DateTimeOffset nowUtc) => CompletedAtUtc = nowUtc;
    public void Revoke(DateTimeOffset nowUtc) => RevokedAtUtc = nowUtc;
}
