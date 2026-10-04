using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.Clients.Commands.CreateClientBasicDetails;

public sealed record CreateClientBasicDetailsCommand(
    ClientIdentityInput Identity,
    ClientCareSetupInput CareSetup,
    string? InsuranceProvider,
    IReadOnlyList<ClientServiceInput> Services)
    : ICommand<CreateClientBasicDetailsResult>, IRequirePermission,
        IAuditableRequest<CreateClientBasicDetailsResult>
{
    public string RequiredPermission => Permissions.Client.Create;

    public AuditEvent CreateAuditEvent(CreateClientBasicDetailsResult response)
    {
        var succeeded = response.Administration?.Status == ClientAdministrationStatus.Succeeded;
        return new(
            "Client.Created",
            "Client",
            response.Administration?.Client?.Id.ToString("N") ?? "not-created",
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(
                    ("status", "Pending"),
                    ("serviceCount", Services.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }
}

public sealed record CreateClientBasicDetailsResult(
    ClientAdministrationResult? Administration,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<CreateClientBasicDetailsResult>
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Administration?.Status.ToString()
        ?? "Unavailable";

    public static CreateClientBasicDetailsResult Completed(ClientAdministrationResult administration) =>
        new(administration, null);

    public static CreateClientBasicDetailsResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed class CreateClientBasicDetailsCommandHandler(IClientAdministration administration)
    : ICommandHandler<CreateClientBasicDetailsCommand, CreateClientBasicDetailsResult>
{
    public async ValueTask<CreateClientBasicDetailsResult> Handle(
        CreateClientBasicDetailsCommand command,
        CancellationToken cancellationToken) =>
        CreateClientBasicDetailsResult.Completed(await administration.CreateBasicDetailsAsync(
            new(command.Identity, command.CareSetup, command.InsuranceProvider, command.Services),
            cancellationToken));
}
