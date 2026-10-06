using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Identity;

internal sealed class CareComplyPasswordResetTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<CareComplyPasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(
        dataProtectionProvider,
        Microsoft.Extensions.Options.Options.Create(new DataProtectionTokenProviderOptions
        {
            Name = options.Value.Name,
            TokenLifespan = options.Value.TokenLifespan,
        }),
        logger)
{
    public const string ProviderName = "CareComplyPasswordReset";
}

public sealed class CareComplyPasswordResetTokenProviderOptions
    : DataProtectionTokenProviderOptions;
