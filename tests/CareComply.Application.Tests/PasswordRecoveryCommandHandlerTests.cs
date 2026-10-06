using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Features.Authentication.Commands.ForgotPassword;
using CareComply.Application.Features.Authentication.Commands.ResetPassword;

namespace CareComply.Application.Tests;

public sealed class PasswordRecoveryCommandHandlerTests
{
    [Fact]
    public async Task ForgotPasswordEligibleAccountSendsTrustedLink()
    {
        var recovery = new PasswordRecoveryStub
        {
            Challenge = new(Guid.NewGuid(), "person@example.com", "opaque-token"),
        };
        var email = new EmailSenderSpy();
        var link = new LinkFactoryStub("https://app.example.test/auth/reset-password?opaque=true");
        var handler = new ForgotPasswordCommandHandler(recovery, email, link);

        var result = await handler.Handle(
            new ForgotPasswordCommand(" person@example.com "),
            CancellationToken.None);

        Assert.Same(ForgotPasswordResult.Accepted, result);
        Assert.Equal("person@example.com", recovery.RequestedEmail);
        Assert.Equal("person@example.com", email.Recipient);
        Assert.Equal(link.Link, email.ResetUrl);
    }

    [Fact]
    public async Task ForgotPasswordIneligibleAccountReturnsSameOutcomeWithoutEmail()
    {
        var email = new EmailSenderSpy();
        var handler = new ForgotPasswordCommandHandler(
            new PasswordRecoveryStub(),
            email,
            new LinkFactoryStub("https://app.example.test/reset"));

        var result = await handler.Handle(
            new ForgotPasswordCommand("unknown@example.com"),
            CancellationToken.None);

        Assert.Same(ForgotPasswordResult.Accepted, result);
        Assert.Null(email.Recipient);
    }

    [Fact]
    public async Task ForgotPasswordForwardsCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var recovery = new PasswordRecoveryStub();
        var handler = new ForgotPasswordCommandHandler(
            recovery,
            new EmailSenderSpy(),
            new LinkFactoryStub(null));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await handler.Handle(new ForgotPasswordCommand("person@example.com"), source.Token));
    }

    [Theory]
    [InlineData(PasswordResetOutcome.Succeeded, ResetPasswordStatus.Succeeded)]
    [InlineData(PasswordResetOutcome.Invalid, ResetPasswordStatus.Invalid)]
    public async Task ResetPasswordMapsSafeOutcome(
        PasswordResetOutcome identityOutcome,
        ResetPasswordStatus expectedStatus)
    {
        var recovery = new PasswordRecoveryStub { ResetOutcome = identityOutcome };
        var handler = new ResetPasswordCommandHandler(recovery);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(
            new ResetPasswordCommand(userId, "opaque", "fifteen-characters", "fifteen-characters"),
            CancellationToken.None);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(userId, recovery.ResetUserId);
        Assert.Equal("opaque", recovery.ResetToken);
    }

    [Fact]
    public void ValidatorsEnforceEmailPasswordPolicyAndConfirmation()
    {
        var forgotResult = new ForgotPasswordCommandValidator().Validate(
            new ForgotPasswordCommand("not-an-email"));
        var resetResult = new ResetPasswordCommandValidator().Validate(
            new ResetPasswordCommand(Guid.Empty, "", "short", "different"));

        Assert.False(forgotResult.IsValid);
        Assert.False(resetResult.IsValid);
        Assert.Contains(resetResult.Errors, error => error.PropertyName == "NewPassword");
        Assert.Contains(resetResult.Errors, error => error.PropertyName == "ConfirmPassword");
    }

    private sealed class PasswordRecoveryStub : IPasswordRecovery
    {
        public PasswordResetChallenge? Challenge { get; init; }

        public PasswordResetOutcome ResetOutcome { get; init; } = PasswordResetOutcome.Invalid;

        public string? RequestedEmail { get; private set; }

        public Guid? ResetUserId { get; private set; }

        public string? ResetToken { get; private set; }

        public Task<PasswordResetChallenge?> CreateChallengeAsync(
            string email,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedEmail = email;
            return Task.FromResult(Challenge);
        }

        public Task<PasswordResetOutcome> ResetPasswordAsync(
            Guid userId,
            string urlSafeToken,
            string newPassword,
            CancellationToken cancellationToken)
        {
            ResetUserId = userId;
            ResetToken = urlSafeToken;
            return Task.FromResult(ResetOutcome);
        }
    }

    private sealed class EmailSenderSpy : IPasswordRecoveryEmailSender
    {
        public string? Recipient { get; private set; }

        public string? ResetUrl { get; private set; }

        public Task SendAsync(
            string recipientAddress,
            string resetUrl,
            CancellationToken cancellationToken)
        {
            Recipient = recipientAddress;
            ResetUrl = resetUrl;
            return Task.CompletedTask;
        }
    }

    private sealed class LinkFactoryStub(string? link) : IPasswordResetLinkFactory
    {
        public string? Link { get; } = link;

        public string? Create(Guid userId, string urlSafeToken) => Link;
    }
}
