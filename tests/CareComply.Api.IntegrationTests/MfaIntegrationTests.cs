using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareComply.Application.Authorization;
using CareComply.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;

namespace CareComply.Api.IntegrationTests;

public sealed class MfaIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string ValidPassword = "correct horse battery staple";

    [Fact]
    public async Task RequiredUserEnrollsWithRealIdentityProviderBeforeApplicationSessionExists()
    {
        var user = await CreateUserAsync();
        await factory.GrantPermissionAsync(user.Id, Permissions.User.Manage);
        using var client = factory.CreateSecureClient();

        var login = await LoginAsync(client, user.Email!);
        Assert.Equal(HttpStatusCode.Accepted, login.StatusCode);
        Assert.DoesNotContain(
            login.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-CareComply.Auth=", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        var context = await GetMfaContextAsync(client);
        Assert.Equal("mfaEnrollmentRequired", context.Status);
        var unprotectedBegin = await client.PostAsync("/api/auth/mfa/enrollment/begin", null);
        Assert.Equal(HttpStatusCode.BadRequest, unprotectedBegin.StatusCode);

        var begin = await PostWithCsrfAsync(
            client,
            "/api/auth/mfa/enrollment/begin",
            payload: null,
            context.RequestToken);
        Assert.Equal(HttpStatusCode.OK, begin.StatusCode);
        var beginBody = await begin.Content.ReadFromJsonAsync<MfaEnrollmentBody>();
        Assert.NotNull(beginBody);
        Assert.False(string.IsNullOrWhiteSpace(beginBody.ManualSetupKey));
        Assert.StartsWith("data:image/png;base64,", beginBody.QrCodeDataUrl, StringComparison.Ordinal);
        Assert.False((await factory.GetUserAsync(user.Id)).TwoFactorEnabled);

        var verificationCode = await factory.GenerateAuthenticatorCodeAsync(user.Id);
        var confirmation = await PostWithCsrfAsync(
            client,
            "/api/auth/mfa/enrollment/confirm",
            new { code = verificationCode },
            context.RequestToken);
        Assert.True(
            confirmation.StatusCode == HttpStatusCode.OK,
            await confirmation.Content.ReadAsStringAsync());
        var confirmationBody = await confirmation.Content
            .ReadFromJsonAsync<MfaEnrollmentConfirmationBody>();
        Assert.NotNull(confirmationBody);
        Assert.Equal(10, confirmationBody.RecoveryCodes.Length);
        Assert.True((await factory.GetUserAsync(user.Id)).TwoFactorEnabled);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(0, await factory.CountBusinessAuditLogsAsync(user.Id));
    }

    [Fact]
    public async Task EnabledUserReceivesIntermediateChallengeAndAuthenticatorCompletesLogin()
    {
        var user = await CreateUserAsync();
        await factory.EnableAuthenticatorAsync(user.Id);
        using var client = factory.CreateSecureClient();

        var login = await LoginAsync(client, user.Email!);
        Assert.Equal(HttpStatusCode.Accepted, login.StatusCode);
        var cookies = login.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, value => value.StartsWith("__Host-CareComply.Mfa=", StringComparison.Ordinal));
        Assert.Contains(cookies, value => value.StartsWith("__Host-CareComply.MfaContext=", StringComparison.Ordinal));
        Assert.DoesNotContain(cookies, value => value.StartsWith("__Host-CareComply.Auth=", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        var context = await GetMfaContextAsync(client);
        Assert.Equal("mfaRequired", context.Status);
        var invalid = await PostWithCsrfAsync(
            client,
            "/api/auth/mfa/verify",
            new { code = "000000" },
            context.RequestToken);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        await AssertProblemTypeAsync(invalid, "urn:carecomply:problem:mfa-verification-failed");

        var code = await factory.GenerateAuthenticatorCodeAsync(user.Id);
        var verified = await PostWithCsrfAsync(
            client,
            "/api/auth/mfa/verify",
            new { code },
            context.RequestToken);
        Assert.True(
            verified.StatusCode == HttpStatusCode.NoContent,
            await verified.Content.ReadAsStringAsync());
        Assert.Contains(
            verified.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-CareComply.Auth=", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task NewlyRequiredPermissionInvalidatesPasswordOnlyApplicationSession()
    {
        var user = await CreateUserAsync();
        using var client = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(client, user.Email!)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        await factory.GrantPermissionAsync(user.Id, Permissions.User.Manage);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task RecoveryCodeCompletesLoginOnceAndCannotBeReused()
    {
        var user = await CreateUserAsync();
        var recoveryCode = Assert.Single(
            (await factory.EnableAuthenticatorAsync(user.Id)).Take(1));

        using (var firstClient = factory.CreateSecureClient())
        {
            Assert.Equal(HttpStatusCode.Accepted, (await LoginAsync(firstClient, user.Email!)).StatusCode);
            var context = await GetMfaContextAsync(firstClient);
            var recovered = await PostWithCsrfAsync(
                firstClient,
                "/api/auth/mfa/recovery",
                new { recoveryCode },
                context.RequestToken);
            Assert.Equal(HttpStatusCode.NoContent, recovered.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await firstClient.GetAsync("/api/auth/me")).StatusCode);
        }

        using var secondClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Accepted, (await LoginAsync(secondClient, user.Email!)).StatusCode);
        var secondContext = await GetMfaContextAsync(secondClient);
        var reused = await PostWithCsrfAsync(
            secondClient,
            "/api/auth/mfa/recovery",
            new { recoveryCode },
            secondContext.RequestToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        await AssertProblemTypeAsync(reused, "urn:carecomply:problem:mfa-recovery-failed");
    }

    [Fact]
    public async Task SecurityStampAndInactiveChangesInvalidatePendingChallenge()
    {
        var stampUser = await CreateUserAsync();
        await factory.EnableAuthenticatorAsync(stampUser.Id);
        using var stampClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Accepted, (await LoginAsync(stampClient, stampUser.Email!)).StatusCode);
        await factory.UpdateSecurityStampAsync(stampUser.Id);
        var expired = await stampClient.GetAsync("/api/auth/mfa/context");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        await AssertProblemTypeAsync(expired, "urn:carecomply:problem:mfa-challenge-expired");

        var inactiveUser = await CreateUserAsync();
        await factory.EnableAuthenticatorAsync(inactiveUser.Id);
        using var inactiveClient = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Accepted, (await LoginAsync(inactiveClient, inactiveUser.Email!)).StatusCode);
        await factory.DisableUserAsync(inactiveUser.Id);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await inactiveClient.GetAsync("/api/auth/mfa/context")).StatusCode);
    }

    [Fact]
    public async Task VerificationRequiresAntiforgeryAndUsesSeparateRateLimit()
    {
        var user = await CreateUserAsync();
        await factory.EnableAuthenticatorAsync(user.Id);
        using (var antiforgeryClient = factory.CreateSecureClient())
        {
            Assert.Equal(
                HttpStatusCode.Accepted,
                (await LoginAsync(antiforgeryClient, user.Email!)).StatusCode);
            _ = await GetMfaContextAsync(antiforgeryClient);
            var missingCsrf = await antiforgeryClient.PostAsJsonAsync(
                "/api/auth/mfa/verify",
                new { code = "000000" });
            Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
            await AssertProblemTypeAsync(
                missingCsrf,
                "urn:carecomply:problem:antiforgery-validation");
        }

        using var limitedFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Authentication:Mfa:VerificationRateLimitPermitLimit", "2"));
        using var client = limitedFactory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        Assert.Equal(HttpStatusCode.Accepted, (await LoginAsync(client, user.Email!)).StatusCode);
        var context = await GetMfaContextAsync(client);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var permitted = await PostWithCsrfAsync(
                client,
                "/api/auth/mfa/verify",
                new { code = "000000" },
                context.RequestToken);
            Assert.Equal(HttpStatusCode.Unauthorized, permitted.StatusCode);
        }

        var rejected = await PostWithCsrfAsync(
            client,
            "/api/auth/mfa/verify",
            new { code = "000000" },
            context.RequestToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        await AssertProblemTypeAsync(rejected, "urn:carecomply:problem:mfa-rate-limit");
    }

    private async Task<ApplicationUser> CreateUserAsync() => await factory.CreateUserAsync(
        $"mfa-{Guid.NewGuid():N}@example.test",
        ValidPassword);

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password = ValidPassword });

    private static async Task<MfaContextBody> GetMfaContextAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/mfa/context");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MfaContextBody>()
            ?? throw new InvalidOperationException("The MFA context response was empty.");
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(
        HttpClient client,
        string path,
        object? payload,
        string requestToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = payload is null ? null : JsonContent.Create(payload),
        };
        request.Headers.Add("X-XSRF-TOKEN", requestToken);
        return await client.SendAsync(request);
    }

    private static async Task AssertProblemTypeAsync(
        HttpResponseMessage response,
        string expectedType)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(expectedType, root.GetProperty("type").GetString());
        Assert.False(root.TryGetProperty("detail", out _));
        Assert.True(root.TryGetProperty("traceId", out _));
    }

    private sealed record MfaContextBody(string Status, string RequestToken);
    private sealed record MfaEnrollmentBody(string ManualSetupKey, string QrCodeDataUrl);
    private sealed record MfaEnrollmentConfirmationBody(string[] RecoveryCodes);
}
