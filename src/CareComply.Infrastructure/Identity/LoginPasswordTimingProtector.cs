using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Identity;

internal sealed class LoginPasswordTimingProtector
{
    private readonly string _dummyPasswordHash;
    private readonly PasswordHasher<ApplicationUser> _passwordHasher;
    private readonly ApplicationUser _user = new();

    public LoginPasswordTimingProtector(IOptions<PasswordHasherOptions> options)
    {
        _passwordHasher = new PasswordHasher<ApplicationUser>(options);
        _dummyPasswordHash = _passwordHasher.HashPassword(
            _user,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public void Verify(string suppliedPassword) =>
        _ = _passwordHasher.VerifyHashedPassword(
            _user,
            _dummyPasswordHash,
            suppliedPassword);
}
