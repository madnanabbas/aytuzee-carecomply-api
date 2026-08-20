using System.Diagnostics;
using CareComply.Application.Features.Authentication.Commands.Login;

namespace CareComply.Application.Tests;

public sealed class LoginCommandSecurityTests
{
    [Fact]
    public void StringAndDebuggerFormattingRedactPassword()
    {
        var password = $"credential-{Guid.NewGuid():N}";
        var command = new LoginCommand("person@example.test", password);

        Assert.Equal(password, command.Password);
        Assert.DoesNotContain(password, command.ToString(), StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", command.ToString(), StringComparison.Ordinal);

        var debuggerDisplay = Assert.Single(
            typeof(LoginCommand).GetCustomAttributes(typeof(DebuggerDisplayAttribute), false)
                .Cast<DebuggerDisplayAttribute>());
        Assert.DoesNotContain(password, debuggerDisplay.Value, StringComparison.Ordinal);
        Assert.Contains("Password=[REDACTED]", debuggerDisplay.Value, StringComparison.Ordinal);
    }
}
