using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Features.Authentication.Commands.BeginMfaEnrollment;
using CareComply.Application.Features.Authentication.Commands.CancelMfaChallenge;
using CareComply.Application.Features.Authentication.Commands.ConfirmMfaEnrollment;
using CareComply.Application.Features.Authentication.Commands.VerifyMfa;
using CareComply.Application.Features.Authentication.Commands.VerifyMfaRecoveryCode;
using CareComply.Application.Features.Authentication.Commands.ResendMfaEmailCode;
using CareComply.Application.Features.Authentication.Queries.GetMfaContext;

namespace CareComply.Application.Tests;

public sealed class MfaCommandHandlerTests
{
    [Fact]
    public async Task HandlersDelegateOnlyTheirFocusedMfaOperationsAndForwardCancellation()
    {
        var identity = new RecordingIdentityMfa();
        using var cancellation = new CancellationTokenSource();

        Assert.Equal(
            new MfaContext(MfaContextKind.VerificationRequired, UserMfaMethod.Authenticator),
            await new GetMfaContextQueryHandler(identity).Handle(
                new GetMfaContextQuery(),
                cancellation.Token));
        Assert.NotNull(await new BeginMfaEnrollmentCommandHandler(identity).Handle(
            new BeginMfaEnrollmentCommand(),
            cancellation.Token));
        Assert.Equal(
            MfaSignInStatus.Succeeded,
            (await new ConfirmMfaEnrollmentCommandHandler(identity).Handle(
                new ConfirmMfaEnrollmentCommand("123456"),
                cancellation.Token)).Status);
        Assert.Equal(
            MfaSignInStatus.Succeeded,
            await new VerifyMfaCommandHandler(identity).Handle(
                new VerifyMfaCommand("654321"),
                cancellation.Token));
        Assert.Equal(
            MfaEmailSendStatus.Succeeded,
            await new ResendMfaEmailCodeCommandHandler(identity).Handle(
                new ResendMfaEmailCodeCommand(),
                cancellation.Token));
        Assert.Equal(
            MfaSignInStatus.Succeeded,
            await new VerifyMfaRecoveryCodeCommandHandler(identity).Handle(
                new VerifyMfaRecoveryCodeCommand("recovery-code"),
                cancellation.Token));
        await new CancelMfaChallengeCommandHandler(identity).Handle(
            new CancelMfaChallengeCommand(),
            cancellation.Token);

        Assert.All(identity.CancellationTokens, token => Assert.Equal(cancellation.Token, token));
        Assert.Equal("123456", identity.EnrollmentCode);
        Assert.Equal("654321", identity.AuthenticatorCode);
        Assert.Equal("recovery-code", identity.RecoveryCode);
        Assert.True(identity.Cancelled);
    }

    [Fact]
    public void SensitiveCommandsAndResultsRedactStringFormatting()
    {
        const string secret = "credential-value-that-must-not-leak";
        object[] values =
        [
            new ConfirmMfaEnrollmentCommand(secret),
            new VerifyMfaCommand(secret),
            new VerifyMfaRecoveryCodeCommand(secret),
            new MfaEnrollmentData(secret, secret),
            new MfaEnrollmentConfirmation(MfaSignInStatus.Succeeded, [secret]),
        ];

        Assert.All(values, value =>
        {
            Assert.DoesNotContain(secret, value.ToString(), StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", value.ToString(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task UnexpectedInfrastructureFailurePropagates()
    {
        var expected = new InvalidOperationException("Infrastructure failure.");
        var handler = new VerifyMfaCommandHandler(new ThrowingIdentityMfa(expected));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(new VerifyMfaCommand("123456"), CancellationToken.None));

        Assert.Same(expected, actual);
    }

    private sealed class RecordingIdentityMfa : IIdentityMfa
    {
        public List<CancellationToken> CancellationTokens { get; } = [];
        public string? EnrollmentCode { get; private set; }
        public string? AuthenticatorCode { get; private set; }
        public string? RecoveryCode { get; private set; }
        public bool Cancelled { get; private set; }

        public Task<MfaContext> GetContextAsync(CancellationToken cancellationToken)
        {
            CancellationTokens.Add(cancellationToken);
            return Task.FromResult(new MfaContext(
                MfaContextKind.VerificationRequired,
                UserMfaMethod.Authenticator));
        }

        public Task<MfaEnrollmentData?> BeginEnrollmentAsync(CancellationToken cancellationToken)
        {
            CancellationTokens.Add(cancellationToken);
            return Task.FromResult<MfaEnrollmentData?>(new("setup", "uri"));
        }

        public Task<MfaEnrollmentConfirmation> ConfirmEnrollmentAsync(
            string code,
            CancellationToken cancellationToken)
        {
            EnrollmentCode = code;
            CancellationTokens.Add(cancellationToken);
            return Task.FromResult(new MfaEnrollmentConfirmation(
                MfaSignInStatus.Succeeded,
                ["recovery"]));
        }

        public Task<MfaSignInStatus> VerifyAuthenticatorAsync(
            string code,
            CancellationToken cancellationToken)
        {
            AuthenticatorCode = code;
            CancellationTokens.Add(cancellationToken);
            return Task.FromResult(MfaSignInStatus.Succeeded);
        }

        public Task<MfaSignInStatus> VerifyRecoveryCodeAsync(
            string recoveryCode,
            CancellationToken cancellationToken)
        {
            RecoveryCode = recoveryCode;
            CancellationTokens.Add(cancellationToken);
            return Task.FromResult(MfaSignInStatus.Succeeded);
        }

        public Task<MfaEmailSendStatus> ResendEmailCodeAsync(
            CancellationToken cancellationToken)
        {
            CancellationTokens.Add(cancellationToken);
            return Task.FromResult(MfaEmailSendStatus.Succeeded);
        }

        public Task CancelAsync(CancellationToken cancellationToken)
        {
            Cancelled = true;
            CancellationTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingIdentityMfa(Exception exception) : IIdentityMfa
    {
        public Task<MfaContext> GetContextAsync(CancellationToken cancellationToken) =>
            Task.FromException<MfaContext>(exception);

        public Task<MfaEnrollmentData?> BeginEnrollmentAsync(CancellationToken cancellationToken) =>
            Task.FromException<MfaEnrollmentData?>(exception);

        public Task<MfaEnrollmentConfirmation> ConfirmEnrollmentAsync(
            string code,
            CancellationToken cancellationToken) =>
            Task.FromException<MfaEnrollmentConfirmation>(exception);

        public Task<MfaSignInStatus> VerifyAuthenticatorAsync(
            string code,
            CancellationToken cancellationToken) =>
            Task.FromException<MfaSignInStatus>(exception);

        public Task<MfaSignInStatus> VerifyRecoveryCodeAsync(
            string recoveryCode,
            CancellationToken cancellationToken) =>
            Task.FromException<MfaSignInStatus>(exception);

        public Task<MfaEmailSendStatus> ResendEmailCodeAsync(
            CancellationToken cancellationToken) =>
            Task.FromException<MfaEmailSendStatus>(exception);

        public Task CancelAsync(CancellationToken cancellationToken) =>
            Task.FromException(exception);
    }
}
