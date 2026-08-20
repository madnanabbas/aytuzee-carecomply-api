using CareComply.Application.Behaviors;
using CareComply.Application.Exceptions;
using CareComply.Application.Features.Authentication.Commands.Login;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Tests;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task InvalidCommandAggregatesErrorsAndDoesNotInvokeHandler()
    {
        var handlerWasCalled = false;
        var behavior = new ValidationBehavior<LoginCommand, LoginResult>(
            [new LoginCommandValidator()]);
        var command = new LoginCommand(string.Empty, string.Empty);
        MessageHandlerDelegate<LoginCommand, LoginResult> next = (_, _) =>
        {
            handlerWasCalled = true;
            return ValueTask.FromResult(new LoginResult(LoginStatus.Succeeded));
        };

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => behavior.Handle(command, next, CancellationToken.None).AsTask());

        Assert.False(handlerWasCalled);
        Assert.Contains(nameof(LoginCommand.Email), exception.Errors.Keys);
        Assert.Contains(nameof(LoginCommand.Password), exception.Errors.Keys);
    }

    [Fact]
    public async Task ValidCommandInvokesHandler()
    {
        var handlerWasCalled = false;
        var behavior = new ValidationBehavior<LoginCommand, LoginResult>(
            [new LoginCommandValidator()]);
        var command = new LoginCommand("person@example.test", "password");
        MessageHandlerDelegate<LoginCommand, LoginResult> next = (_, _) =>
        {
            handlerWasCalled = true;
            return ValueTask.FromResult(new LoginResult(LoginStatus.Succeeded));
        };

        var result = await behavior.Handle(command, next, CancellationToken.None);

        Assert.True(handlerWasCalled);
        Assert.Equal(LoginStatus.Succeeded, result.Status);
    }
}
