using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Features.Authentication.Commands.Login;

namespace CareComply.Application.Tests;

public sealed class LoginCommandHandlerTests
{
    public static TheoryData<IdentityAuthenticationOutcome, LoginStatus> Outcomes => new()
    {
        { IdentityAuthenticationOutcome.Succeeded, LoginStatus.Succeeded },
        { IdentityAuthenticationOutcome.RequiresTwoFactor, LoginStatus.MfaRequired },
        { IdentityAuthenticationOutcome.RequiresMfaEnrollment, LoginStatus.MfaEnrollmentRequired },
        { IdentityAuthenticationOutcome.Failed, LoginStatus.InvalidCredentials },
        { IdentityAuthenticationOutcome.Unavailable, LoginStatus.Unavailable },
    };

    [Theory]
    [MemberData(nameof(Outcomes))]
    public async Task MapsIdentityOutcome(
        IdentityAuthenticationOutcome identityOutcome,
        LoginStatus expectedStatus)
    {
        var identity = new StubIdentityAuthentication(identityOutcome);
        var handler = new LoginCommandHandler(identity);
        var command = new LoginCommand("person@example.test", "password");
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await handler.Handle(command, cancellationTokenSource.Token);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(command.Email, identity.Email);
        Assert.Equal(command.Password, identity.Password);
        Assert.Equal(cancellationTokenSource.Token, identity.CancellationToken);
    }

    [Fact]
    public async Task UnexpectedIdentityExceptionPropagates()
    {
        var expectedException = new InvalidOperationException("Infrastructure failure.");
        var handler = new LoginCommandHandler(new ThrowingIdentityAuthentication(expectedException));

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(
                new LoginCommand("person@example.test", "password"),
                CancellationToken.None));

        Assert.Same(expectedException, actualException);
    }

    private sealed class StubIdentityAuthentication(IdentityAuthenticationOutcome outcome)
        : IIdentityAuthentication
    {
        public string? Email { get; private set; }

        public string? Password { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<IdentityAuthenticationOutcome> AuthenticateAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            Email = email;
            Password = password;
            CancellationToken = cancellationToken;
            return Task.FromResult(outcome);
        }
    }

    private sealed class ThrowingIdentityAuthentication(Exception exception)
        : IIdentityAuthentication
    {
        public Task<IdentityAuthenticationOutcome> AuthenticateAsync(
            string email,
            string password,
            CancellationToken cancellationToken) =>
            Task.FromException<IdentityAuthenticationOutcome>(exception);
    }
}
