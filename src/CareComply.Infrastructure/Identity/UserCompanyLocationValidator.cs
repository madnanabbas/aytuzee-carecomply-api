using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Identity;

internal sealed class UserCompanyLocationValidator(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantScopeAccessor)
{
    public async Task<UserCompanyLocationValidation> ValidateAsync(
        ApplicationUser user,
        UserGeneralUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null)
        {
            return UserCompanyLocationValidation.Failed(UserAdministrationStatus.NotFound);
        }

        var targetCompanyId = user.IsPlatformAdministrator
            ? null
            : tenant.IsPlatformAdministrator
                ? request.CompanyId
                : tenant.CompanyId;
        if (!user.IsPlatformAdministrator
            && (targetCompanyId is null
                || !await context.Companies.AnyAsync(
                    company => company.Id == targetCompanyId && company.IsActive,
                    cancellationToken)))
        {
            return UserCompanyLocationValidation.Failed(UserAdministrationStatus.CompanyNotFound);
        }

        var locationIds = request.LocationIds is null
            ? await context.UserLocations
                .Where(location => location.UserId == user.Id)
                .Select(location => location.LocationId)
                .ToArrayAsync(cancellationToken)
            : request.LocationIds.Distinct().ToArray();
        if (!user.IsPlatformAdministrator
            && (locationIds.Length == 0
                || await context.CompanyLocations.CountAsync(
                    location => locationIds.Contains(location.Id)
                        && location.CompanyId == targetCompanyId
                        && location.IsActive,
                    cancellationToken) != locationIds.Length))
        {
            return UserCompanyLocationValidation.Failed(UserAdministrationStatus.CompanyNotFound);
        }

        return new(true, default, targetCompanyId, locationIds);
    }
}

internal sealed record UserCompanyLocationValidation(
    bool IsValid,
    UserAdministrationStatus FailureStatus,
    Guid? CompanyId,
    IReadOnlyList<Guid> LocationIds)
{
    public static UserCompanyLocationValidation Failed(UserAdministrationStatus status) =>
        new(false, status, null, []);
}
