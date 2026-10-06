using CareComply.Api.Authentication;
using CareComply.Api.Contracts.Authentication;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Features.Authentication.Commands.BeginMfaEnrollment;
using CareComply.Application.Features.Authentication.Commands.CancelMfaChallenge;
using CareComply.Application.Features.Authentication.Commands.ConfirmMfaEnrollment;
using CareComply.Application.Features.Authentication.Commands.VerifyMfa;
using CareComply.Application.Features.Authentication.Commands.VerifyMfaRecoveryCode;
using CareComply.Application.Features.Authentication.Commands.ResendMfaEmailCode;
using CareComply.Application.Features.Authentication.Queries.GetMfaContext;
using Mediator;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CareComply.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/auth/mfa")]
public sealed class MfaController(
    IAntiforgery antiforgery,
    MfaQrCodeRenderer qrCodeRenderer,
    ISender sender)
    : ControllerBase
{
    [HttpGet("context")]
    public async Task<ActionResult<MfaContextResponse>> Context(
        CancellationToken cancellationToken)
    {
        SetNoStore();
        var context = await sender.Send(new GetMfaContextQuery(), cancellationToken);
        if (context.Kind == MfaContextKind.None)
        {
            return ChallengeExpired();
        }

        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        var status = context.Kind == MfaContextKind.VerificationRequired
            ? "mfaRequired"
            : "mfaEnrollmentRequired";
        return Ok(new MfaContextResponse(
            status,
            context.Method.ToString(),
            tokens.RequestToken));
    }

    [HttpPost("enrollment/begin")]
    public async Task<ActionResult<MfaEnrollmentResponse>> BeginEnrollment(
        CancellationToken cancellationToken)
    {
        SetNoStore();
        var enrollment = await sender.Send(
            new BeginMfaEnrollmentCommand(),
            cancellationToken);
        if (enrollment is null)
        {
            return ChallengeExpired();
        }

        return Ok(new MfaEnrollmentResponse(
            enrollment.ManualSetupKey,
            qrCodeRenderer.RenderPngDataUrl(enrollment.AuthenticatorUri)));
    }

    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.MfaRateLimitPolicy)]
    [HttpPost("enrollment/confirm")]
    public async Task<ActionResult<MfaEnrollmentConfirmedResponse>> ConfirmEnrollment(
        MfaCodeRequest request,
        CancellationToken cancellationToken)
    {
        SetNoStore();
        var result = await sender.Send(
            new ConfirmMfaEnrollmentCommand(request.Code ?? string.Empty),
            cancellationToken);
        return result.Status switch
        {
            MfaSignInStatus.Succeeded => Ok(
                new MfaEnrollmentConfirmedResponse(result.RecoveryCodes)),
            MfaSignInStatus.Invalid => VerificationFailed(),
            MfaSignInStatus.Expired => ChallengeExpired(),
            MfaSignInStatus.Unavailable => MfaUnavailable(),
            _ => throw new InvalidOperationException("The MFA enrollment result is invalid."),
        };
    }

    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.MfaRateLimitPolicy)]
    [HttpPost("verify")]
    public async Task<IActionResult> Verify(
        MfaCodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new VerifyMfaCommand(request.Code ?? string.Empty),
            cancellationToken);
        return MapSignInResult(result, isRecoveryCode: false);
    }

    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.MfaRateLimitPolicy)]
    [HttpPost("recovery")]
    public async Task<IActionResult> Recovery(
        MfaRecoveryCodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new VerifyMfaRecoveryCodeCommand(request.RecoveryCode ?? string.Empty),
            cancellationToken);
        return MapSignInResult(result, isRecoveryCode: true);
    }

    [EnableRateLimiting(CareComplyAuthenticationServiceCollectionExtensions.MfaEmailResendRateLimitPolicy)]
    [HttpPost("email/resend")]
    public async Task<IActionResult> ResendEmailCode(CancellationToken cancellationToken)
    {
        SetNoStore();
        var result = await sender.Send(
            new ResendMfaEmailCodeCommand(),
            cancellationToken);
        return result switch
        {
            MfaEmailSendStatus.Succeeded => NoContent(),
            MfaEmailSendStatus.Expired => ChallengeExpired(),
            MfaEmailSendStatus.Unavailable => MfaUnavailable(),
            _ => throw new InvalidOperationException("The MFA email result is invalid."),
        };
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken)
    {
        await sender.Send(new CancelMfaChallengeCommand(), cancellationToken);
        return NoContent();
    }

    private IActionResult MapSignInResult(MfaSignInStatus status, bool isRecoveryCode) =>
        status switch
        {
            MfaSignInStatus.Succeeded => NoContent(),
            MfaSignInStatus.Invalid => isRecoveryCode
                ? RecoveryFailed()
                : VerificationFailed(),
            MfaSignInStatus.Expired => ChallengeExpired(),
            MfaSignInStatus.Unavailable => MfaUnavailable(),
            _ => throw new InvalidOperationException("The MFA sign-in result is invalid."),
        };

    private ObjectResult VerificationFailed() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "The verification code is invalid or has expired.",
        type: ProblemTypes.MfaVerificationFailed);

    private ObjectResult RecoveryFailed() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "The recovery code is invalid or has already been used.",
        type: ProblemTypes.MfaRecoveryFailed);

    private ObjectResult ChallengeExpired() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Your sign-in session has expired. Please sign in again.",
        type: ProblemTypes.MfaChallengeExpired);

    private ObjectResult MfaUnavailable() => Problem(
        statusCode: StatusCodes.Status500InternalServerError,
        title: "Verification could not be completed.",
        type: ProblemTypes.AuthenticationUnavailable);

    private void SetNoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }
}
