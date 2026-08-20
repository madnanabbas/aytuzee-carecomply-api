using CareComply.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CareComply.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        if (!Permissions.IsDefined(permission))
        {
            throw new ArgumentException(
                "The permission must be defined by the canonical permission catalog.",
                nameof(permission));
        }

        Permission = permission;
        Policy = PermissionPolicyProvider.PolicyPrefix + permission;
    }

    public string Permission { get; }
}
