using System.Diagnostics;
using CareComply.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Errors;

public sealed class CareComplyExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<CareComplyExceptionHandler> logger)
    : IExceptionHandler
{
    private static readonly Action<ILogger, string, string, Exception?> LogUnhandledException =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(1000, "UnhandledException"),
            "An unhandled {ExceptionType} occurred. Trace identifier: {TraceIdentifier}.");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception is ApplicationValidationException validationException
            ? CreateValidationProblemDetails(validationException)
            : CreateInternalServerError(httpContext, exception);

        problemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = problemDetails.Status
            ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
        });
    }

    private static ValidationProblemDetails CreateValidationProblemDetails(
        ApplicationValidationException exception) => new(
            exception.Errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Type = ProblemTypes.Validation,
        };

    private ProblemDetails CreateInternalServerError(
        HttpContext httpContext,
        Exception exception)
    {
        LogUnhandledException(
            logger,
            exception.GetType().Name,
            Activity.Current?.Id ?? httpContext.TraceIdentifier,
            null);

        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = ProblemTypes.InternalServerError,
        };
    }
}
