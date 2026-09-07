using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.Identity;

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
        Id = Guid.CreateVersion7();
    }

    public Guid CompanyId { get; set; }
}
