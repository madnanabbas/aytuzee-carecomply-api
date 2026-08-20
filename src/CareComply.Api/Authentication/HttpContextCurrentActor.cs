using CareComply.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CareComply.Api.Authentication;

public sealed class HttpContextCurrentActor(
    IHttpContextAccessor httpContextAccessor,
    IOptions<IdentityOptions> identityOptions)
    : ICurrentActor
{
    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated is true;

    public Guid? UserId
    {
        get
        {
            var claimType = identityOptions.Value.ClaimsIdentity.UserIdClaimType;
            var value = httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }
}
