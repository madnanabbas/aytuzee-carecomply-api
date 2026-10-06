using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientGuestInvitationCreator(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantAccessor,
    IOptions<ClientGuestInvitationOptions> options,
    TimeProvider timeProvider) : IClientGuestInvitationCreator
{
    public async Task<ClientGuestInvitationCreationResult> CreateAsync(
        ClientGuestInvitationCreationRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessor.GetAsync(cancellationToken);
        if (tenant is null) return new(ClientGuestInvitationCreationStatus.NotFound);

        var companyId = tenant.IsPlatformAdministrator ? request.CompanyId : tenant.CompanyId;
        if (companyId is null
            || (!tenant.IsPlatformAdministrator && request.CompanyId is not null && request.CompanyId != tenant.CompanyId)
            || !await context.Companies.AsNoTracking().AnyAsync(
                company => company.Id == companyId && company.IsActive,
                cancellationToken))
            return new(ClientGuestInvitationCreationStatus.NotFound);

        if (!options.Value.IsValid())
            return new(ClientGuestInvitationCreationStatus.InvalidConfiguration);

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(options.Value.Lifetime);
        var token = ClientGuestInvitationToken.Generate();
        var invitation = ClientGuestInvitation.Create(
            Guid.CreateVersion7(),
            companyId.Value,
            request.ClientName,
            request.Email,
            request.MobileNumber,
            token.Hash,
            expiresAt,
            now,
            tenant.UserId);

        context.ClientGuestInvitations.Add(invitation);
        await context.SaveChangesAsync(cancellationToken);

        _ = options.Value.TryBuildGuestLink(token.PublicToken, out var guestLink);
        return new(ClientGuestInvitationCreationStatus.Succeeded, invitation.Id, guestLink, expiresAt);
    }
}
