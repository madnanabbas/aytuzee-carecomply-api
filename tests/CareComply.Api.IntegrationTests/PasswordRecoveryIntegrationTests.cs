using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareComply.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CareComply.Api.IntegrationTests;

[Collection(PasswordRecoveryIntegrationGroup.Name)]
public sealed class PasswordRecoveryIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string OldPassword = "correct horse battery staple";
    private const string NewPassword = "new correct horse battery staple";
    private const string GenericMessage =
        "If an account exists for this email, a password reset link has been sent.";

    [Fact]
    public async Task ForgotPasswordReturnsIdenticalPublicOutcomeForAllAccountStates()
    {
        factory.PasswordRecoveryEmailSender.Clear();
        var eligible = await factory.CreateUserAsync(UniqueEmail(), OldPassword);
        var inactive = await factory.CreateUserAsync(UniqueEmail(), OldPassword, isActive: false);
        var unconfirmed = await factory.CreateUserAsync(
            UniqueEmail(),
            OldPassword,
            emailConfirmed: false);
        using var client = factory.CreateSecureClient();

        var responses = new[]
        {
            await ForgotAsync(client, eligible.Email!),
            await ForgotAsync(client, UniqueEmail()),
            await ForgotAsync(client, inactive.Email!),
            await ForgotAsync(client, unconfirmed.Email!),
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(GenericMessage, document.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("token", document.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        var message = Assert.Single(factory.PasswordRecoveryEmailSender.Messages);
        Assert.Equal(eligible.Email, message.RecipientAddress);
        var resetUri = new Uri(message.ResetUrl);
        Assert.Equal("https://carecomply.example.test/auth/reset-password", resetUri.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(resetUri.Query);
        Assert.Equal(eligible.Id.ToString(), query["userId"]);
        Assert.False(string.IsNullOrWhiteSpace(query["token"]));
        Assert.DoesNotContain(eligible.Email!, message.ResetUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidIdentityTokenResetsPasswordAndRevokesExistingSessionOnly()
    {
        factory.PasswordRecoveryEmailSender.Clear();
        var user = await factory.CreateUserAsync(UniqueEmail(), OldPassword);
        using var authenticatedClient = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await LoginAsync(authenticatedClient, user.Email!, OldPassword)).StatusCode);
        var before = await factory.GetUserAsync(user.Id);
        var mfaBefore = before.TwoFactorEnabled;
        var rolesBefore = await factory.GetUserRolesAsync(user.Id);
        var auditCountBefore = await factory.CountAllBusinessAuditLogsAsync();

        using var anonymousClient = factory.CreateSecureClient();
        var forgot = await ForgotAsync(anonymousClient, user.Email!);
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var resetUri = new Uri(Assert.Single(factory.PasswordRecoveryEmailSender.Messages).ResetUrl);
        var query = QueryHelpers.ParseQuery(resetUri.Query);

        var reset = await anonymousClient.PostAsJsonAsync(
            "/api/auth/password/reset",
            new
            {
                userId = query["userId"].ToString(),
                token = query["token"].ToString(),
                newPassword = NewPassword,
                confirmPassword = NewPassword,
            });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await authenticatedClient.GetAsync("/api/auth/me")).StatusCode);
        using var oldPasswordClient = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await LoginAsync(oldPasswordClient, user.Email!, OldPassword)).StatusCode);
        using var newPasswordClient = factory.CreateSecureClient();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await LoginAsync(newPasswordClient, user.Email!, NewPassword)).StatusCode);

        var after = await factory.GetUserAsync(user.Id);
        Assert.Equal(mfaBefore, after.TwoFactorEnabled);
        Assert.Equal(rolesBefore, await factory.GetUserRolesAsync(user.Id));
        Assert.Equal(auditCountBefore, await factory.CountAllBusinessAuditLogsAsync());
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
    }

    [Fact]
    public async Task InvalidResetTokenReturnsSanitizedProblemDetails()
    {
        var user = await factory.CreateUserAsync(UniqueEmail(), OldPassword);
        using var client = factory.CreateSecureClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/password/reset",
            new
            {
                userId = user.Id,
                token = "not-a-valid-token",
                newPassword = NewPassword,
                confirmPassword = NewPassword,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("urn:carecomply:problem:password-reset-invalid", body, StringComparison.Ordinal);
        Assert.DoesNotContain("not-a-valid-token", body, StringComparison.Ordinal);
        Assert.DoesNotContain(NewPassword, body, StringComparison.Ordinal);
        Assert.DoesNotContain("Identity", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PasswordRecoveryRateLimitIsEnforced()
    {
        using var limitedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:PasswordRecovery:ForgotRateLimitPermitLimit", "2");
            builder.UseSetting("Authentication:PasswordRecovery:ResetRateLimitPermitLimit", "2");
        });
        using var client = limitedFactory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
        });

        Assert.Equal(HttpStatusCode.Accepted, (await ForgotAsync(client, UniqueEmail())).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await ForgotAsync(client, UniqueEmail())).StatusCode);
        var rejected = await ForgotAsync(client, UniqueEmail());

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        var body = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("urn:carecomply:problem:password-recovery-rate-limit", body, StringComparison.Ordinal);
        Assert.DoesNotContain("PermitLimit", body, StringComparison.OrdinalIgnoreCase);

        var resetPayload = new
        {
            userId = Guid.NewGuid(),
            token = "invalid-token",
            newPassword = NewPassword,
            confirmPassword = NewPassword,
        };
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/auth/password/reset", resetPayload)).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/auth/password/reset", resetPayload)).StatusCode);
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync("/api/auth/password/reset", resetPayload)).StatusCode);
    }

    [Fact]
    public void PasswordResetTokenProviderHasFocusedSixtyMinuteLifetime()
    {
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<CareComplyPasswordResetTokenProviderOptions>>()
            .Value;

        Assert.Equal(TimeSpan.FromMinutes(60), options.TokenLifespan);
        Assert.Equal("CareComplyPasswordReset", options.Name);
    }

    private static Task<HttpResponseMessage> ForgotAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/password/forgot", new { email });

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private static string UniqueEmail() => $"recovery-{Guid.NewGuid():N}@example.test";
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PasswordRecoveryIntegrationGroup
{
    public const string Name = "Password recovery integration";
}
