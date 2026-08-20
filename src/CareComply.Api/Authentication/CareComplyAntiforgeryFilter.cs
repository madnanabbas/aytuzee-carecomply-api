using CareComply.Api.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace CareComply.Api.Authentication;

public sealed class CareComplyAntiforgeryFilter(
    IAntiforgery antiforgery,
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
        if (SafeMethods.Contains(context.HttpContext.Request.Method)
            || context.ActionDescriptor.EndpointMetadata
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
            var problemDetails = problemDetailsFactory.CreateProblemDetails(
                context.HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The antiforgery token is invalid or missing.",
                type: ProblemTypes.AntiforgeryValidation);
            context.Result = new BadRequestObjectResult(problemDetails);
        }
    }
}
