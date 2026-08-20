using CareComply.Application.Features.Authentication.Commands.Login;
using FluentValidation.TestHelper;

namespace CareComply.Application.Tests;

public sealed class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public async Task ValidCredentialsPassValidation()
    {
        var result = await _validator.TestValidateAsync(
            new LoginCommand("person@example.test", "correct horse battery staple"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "password", "Email")]
    [InlineData("not-an-email", "password", "Email")]
    [InlineData("person@example.test", "", "Password")]
    public async Task InvalidCredentialsFailValidation(
        string email,
        string password,
        string propertyName)
    {
        var result = await _validator.TestValidateAsync(new LoginCommand(email, password));

        result.ShouldHaveValidationErrorFor(propertyName);
    }

    [Fact]
    public async Task OversizedCredentialsFailValidation()
    {
        var result = await _validator.TestValidateAsync(
            new LoginCommand(new string('a', 257), new string('p', 129)));

        result.ShouldHaveValidationErrorFor(command => command.Email);
        result.ShouldHaveValidationErrorFor(command => command.Password);
    }
}
