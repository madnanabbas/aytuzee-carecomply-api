using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityUserReader(
    UserManager<ApplicationUser> userManager,
    CareComplyDbContext context)
    : IIdentityUserReader
{
    public async Task<IdentityUserSnapshot?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is not { IsActive: true } || string.IsNullOrWhiteSpace(user.Email))
        {
            return null;
        }

        var companyName = await context.Companies.AsNoTracking()
            .Where(company => company.Id == user.CompanyId && company.IsActive)
            .Select(company => company.Name)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(companyName)) return null;

        return new IdentityUserSnapshot(
            user.Id,
            user.Email,
            string.Join(' ', user.FirstName, user.LastName).Trim(),
            user.CompanyId,
            companyName,
            user.IsPlatformAdministrator);
    }
}
