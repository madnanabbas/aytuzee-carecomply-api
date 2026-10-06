namespace CareComply.Application.Abstractions.Clients;

public interface IClientSupervisorCandidateReader
{
    Task<ClientSupervisorCandidateReadResult> ListAsync(
        Guid locationId,
        CancellationToken cancellationToken);
}

public enum ClientSupervisorCandidateReadStatus
{
    Succeeded,
    Unauthenticated,
    NotFound,
}

public sealed record ClientSupervisorCandidateReadResult(
    ClientSupervisorCandidateReadStatus Status,
    IReadOnlyList<ClientSupervisorCandidate> Candidates)
{
    public static ClientSupervisorCandidateReadResult Succeeded(
        IReadOnlyList<ClientSupervisorCandidate> candidates) =>
        new(ClientSupervisorCandidateReadStatus.Succeeded, candidates);

    public static ClientSupervisorCandidateReadResult Unauthenticated() =>
        new(ClientSupervisorCandidateReadStatus.Unauthenticated, []);

    public static ClientSupervisorCandidateReadResult NotFound() =>
        new(ClientSupervisorCandidateReadStatus.NotFound, []);
}

public sealed record ClientSupervisorCandidate(
    Guid Id,
    string DisplayName,
    string JobTitle);
