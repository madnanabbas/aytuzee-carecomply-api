using CareComply.Api.Authentication;
using CareComply.Api.Contracts.Authentication;
using CareComply.Api.Errors;
using CareComply.Application.Features.Authentication.Commands.Login;
using CareComply.Application.Features.Authentication.Commands.ForgotPassword;
using CareComply.Application.Features.Authentication.Commands.ResetPassword;
using CareComply.Application.Features.Authentication.Queries.GetCurrentUser;
using Mediator;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAntiforgery _antiforgery;
    private readonly ISender _sender;

    public AuthController(
        IAntiforgery antiforgery,
        ISender sender)
    {
        _antiforgery = antiforgery;
        _sender = sender;
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.LoginRateLimitPolicy)]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new LoginCommand(request.Email ?? string.Empty, request.Password ?? string.Empty),
            cancellationToken);

        return result.Status switch
        {
            LoginStatus.Succeeded => NoContent(),
            LoginStatus.MfaRequired => Accepted(new MfaRequiredResponse("mfaRequired")),
            LoginStatus.MfaEnrollmentRequired =>
                Accepted(new MfaRequiredResponse("mfaEnrollmentRequired")),
            LoginStatus.InvalidCredentials => AuthenticationFailed(),
            LoginStatus.Unavailable => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Authentication could not be completed.",
                type: ProblemTypes.AuthenticationUnavailable),
            _ => throw new InvalidOperationException("The login result status is invalid."),
        };
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.ForgotPasswordRateLimitPolicy)]
    [HttpPost("password/forgot")]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _sender.Send(
            new ForgotPasswordCommand(request.Email ?? string.Empty),
            cancellationToken);

        return Accepted(new ForgotPasswordResponse(
            "If an account exists for this email, a password reset link has been sent."));
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.ResetPasswordRateLimitPolicy)]
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ResetPasswordCommand(
                request.UserId,
                request.Token ?? string.Empty,
                request.NewPassword ?? string.Empty,
                request.ConfirmPassword ?? string.Empty),
            cancellationToken);

        return result.Status == ResetPasswordStatus.Succeeded
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The password reset link is invalid or has expired.",
                type: ProblemTypes.PasswordResetInvalid);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me(
        CancellationToken cancellationToken)
    {
        var user = await _sender.Send(new GetCurrentUserQuery(), cancellationToken);

        if (user is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Authentication is required.",
                type: ProblemTypes.AuthenticationRequired);
        }

        return Ok(new CurrentUserResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.CompanyId,
            user.CompanyName,
            user.IsPlatformAdministrator,
            user.Permissions));
    }

    [Authorize]
    [HttpGet("csrf")]
    public ActionResult<object> Csrf()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

        return Ok(new { requestToken = tokens.RequestToken });
    }

    private ObjectResult AuthenticationFailed() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Authentication failed.",
        type: ProblemTypes.AuthenticationFailed);
}
