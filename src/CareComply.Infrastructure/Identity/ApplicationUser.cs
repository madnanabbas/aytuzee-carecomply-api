using Microsoft.AspNetCore.Identity;
using CareComply.Application.Abstractions.Identity;

namespace CareComply.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool RequiresMfa { get; set; }

    public UserMfaMethod MfaMethod { get; set; } = UserMfaMethod.Authenticator;

    public DateTimeOffset? LastLoginAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
