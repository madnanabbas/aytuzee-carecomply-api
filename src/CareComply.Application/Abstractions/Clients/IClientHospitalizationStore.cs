namespace CareComply.Application.Abstractions.Clients;
public interface IClientHospitalizationStore { Task<HospitalizationList?> List(Guid clientId, CancellationToken ct); Task<HospitalizationMutation> Create(HospitalizationCreate request, CancellationToken ct); Task<HospitalizationMutation> Update(HospitalizationUpdate request, CancellationToken ct); }
public sealed record HospitalizationItem(Guid Id, string HospitalName, DateTimeOffset AdmittedAtUtc, DateTimeOffset? DischargedAtUtc, string Reason, string Diagnosis, string DischargeInstructions, string EditorVersion);
public sealed record HospitalizationList(IReadOnlyList<HospitalizationItem> Items);
public sealed record HospitalizationCreate(Guid ClientId, string HospitalName, DateTimeOffset AdmittedAtUtc, DateTimeOffset? DischargedAtUtc, string Reason, string? Diagnosis, string? DischargeInstructions, Guid ActorId, DateTimeOffset NowUtc);
public sealed record HospitalizationUpdate(Guid ClientId, Guid Id, string HospitalName, DateTimeOffset AdmittedAtUtc, DateTimeOffset? DischargedAtUtc, string Reason, string? Diagnosis, string? DischargeInstructions, string EditorVersion, DateTimeOffset NowUtc);
public enum HospitalizationMutationStatus { Succeeded, ClientNotFound, NotFound, Conflict }
public sealed record HospitalizationMutation(HospitalizationMutationStatus Status, HospitalizationItem? Value = null);
