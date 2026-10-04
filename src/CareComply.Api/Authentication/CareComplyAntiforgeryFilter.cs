using CareComply.Api.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using CareComply.Application.Abstractions.Clients;
using CareComply.Api.Controllers;

namespace CareComply.Api.Authentication;

public sealed class CareComplyAntiforgeryFilter(
    IAntiforgery antiforgery,
    IClientGuestSessionService guestSessions,
    ProblemDetailsFactory problemDetailsFactory)
    : IAsyncAuthorizationFilter
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase)
        {
            HttpMethods.Get,
            HttpMethods.Head,
            HttpMethods.Options,
            HttpMethods.Trace,
        };

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (SafeMethods.Contains(context.HttpContext.Request.Method)) return;

        if (context.HttpContext.Request.Path.StartsWithSegments("/api/public/client-intake")
            && context.HttpContext.Request.Path != "/api/public/client-intake/redeem")
        {
            var request = context.HttpContext.Request;
            var valid = request.Cookies.TryGetValue(
                    PublicClientIntakeController.SessionCookieName, out var sessionToken)
                && request.Cookies.TryGetValue(
                    PublicClientIntakeController.CsrfCookieName, out var csrfCookie)
                && request.Headers.TryGetValue(
                    PublicClientIntakeController.CsrfHeaderName, out var csrfHeader)
                && string.Equals(csrfCookie, csrfHeader.ToString(), StringComparison.Ordinal)
                && await guestSessions.ValidateCsrfAsync(
                    sessionToken, csrfHeader.ToString(), context.HttpContext.RequestAborted);
            if (!valid) SetFailure(context);
            return;
        }

        if (context.ActionDescriptor.EndpointMetadata
                .OfType<IgnoreAntiforgeryTokenAttribute>()
                .Any())
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            SetFailure(context);
        }
    }

    private void SetFailure(AuthorizationFilterContext context)
    {
        var problemDetails = problemDetailsFactory.CreateProblemDetails(
            context.HttpContext,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The antiforgery token is invalid or missing.",
            type: ProblemTypes.AntiforgeryValidation);
        context.Result = new BadRequestObjectResult(problemDetails);
    }
}
