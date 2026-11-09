using CareComply.Api.Authentication;
using CareComply.Api.Auditing;
using CareComply.Api.Authorization;
using CareComply.Api.Development;
using CareComply.Api.Errors;
using CareComply.Api.Staging;
using CareComply.Application;
using CareComply.Application.Behaviors;
using CareComply.Application.Features.Authentication.Commands.Login;
using CareComply.Infrastructure;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "Frontend";

string[] frontendOrigins = builder.Environment.IsProduction()
    ? ["https://aytuzee.com", "https://www.aytuzee.com"]
    : ["https://happy-grass-04c144b10.7.azurestaticapps.net"];

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] =
            System.Diagnostics.Activity.Current?.Id
            ?? context.HttpContext.TraceIdentifier);

builder.Services.AddExceptionHandler<CareComplyExceptionHandler>();

builder.Services.AddControllers(options =>
    options.Filters.Add<CareComplyAntiforgeryFilter>());

builder.Services.AddOpenApi();

builder.Services.AddApplication();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCareComplyAuthentication(builder.Configuration);

builder.Services.AddSingleton<MfaQrCodeRenderer>();

builder.Services.AddCareComplyAuthorization();

builder.Services.AddCareComplyAuditing();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        FrontendCorsPolicy,
        policy =>
        {
            policy
                .WithOrigins(frontendOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

builder.Services.AddMediator(options =>
{
    options.Assemblies =
    [
        typeof(LoginCommand).Assembly,
    ];

    options.ServiceLifetime = ServiceLifetime.Scoped;

    options.PipelineBehaviors =
    [
        typeof(ValidationBehavior<,>),
        typeof(AuthorizationBehavior<,>),
        typeof(PlatformAdministratorAuthorizationBehavior<,>),
        typeof(AuditBehavior<,>),
    ];
});

var app = builder.Build();

if (await DevelopmentIdentityCommand.TryRunAsync(
        app.Services,
        app.Environment,
        app.Configuration,
        args))
{
    return;
}

if (await StagingIdentityCommand.TryRunAsync(
        app.Services,
        app.Environment,
        app.Configuration,
        args))
{
    return;
}

if (await DevelopmentFormTemplateCommand.TryRunAsync(
        app.Services,
        app.Environment,
        app.Configuration,
        args))
{
    return;
}

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider
        .GetRequiredService<CareComply.Infrastructure.Identity.Development.DevelopmentWorkforceInitializer>()
        .InitializeAsync();

    app.MapOpenApi();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();


app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (context.Context.Request.Path.Equals(
                "/config/app-config.json",
                StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "no-store";
            context.Context.Response.Headers.Pragma = "no-cache";
            context.Context.Response.Headers.Expires = "0";
        }
    },
});

if (app.Environment.IsStaging() || app.Environment.IsProduction())
{
    app.UseCors(FrontendCorsPolicy);
}

app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/public/client-intake")
        && context.Request.Path != "/api/public/client-intake/redeem"
        && context.Request.Cookies.TryGetValue(
            CareComply.Api.Controllers.PublicClientIntakeController.SessionCookieName,
            out var guestSessionToken))
    {
        context.Request.Headers[
            CareComply.Api.Controllers.PublicClientIntakeController.TokenHeaderName] = guestSessionToken;
    }
    await next(context);
});

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.MapFallback(async context =>
{
    if ((!HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method))
        || context.Request.Path.StartsWithSegments(
            "/api",
            StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var indexFile = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
    if (!indexFile.Exists)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.Headers.CacheControl = "no-cache";
    await context.Response.SendFileAsync(indexFile, context.RequestAborted);
});

app.Run();

public partial class Program;
