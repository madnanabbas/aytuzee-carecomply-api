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

const string StagingCorsPolicy = "StagingFrontend";

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
        StagingCorsPolicy,
        policy =>
        {
            policy
                .WithOrigins(
                    "https://happy-grass-04c144b10.7.azurestaticapps.net")
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

if (app.Environment.IsStaging())
{
    app.UseCors(StagingCorsPolicy);
}

app.UseAuthentication();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;