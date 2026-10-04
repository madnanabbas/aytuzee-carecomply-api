using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Collections.Concurrent;
using CareComply.Api.Authentication;
using CareComply.Api.Contracts.PublicClientIntake;
using CareComply.Api.Controllers;
using CareComply.Domain.Clients;
using CareComply.Domain.Companies;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class PublicClientIntakeIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string HeaderName = "X-Client-Intake-Token";
    private const string Password = "correct horse battery staple";
    private static readonly ConcurrentDictionary<string, string> CsrfTokens = new();

    [Fact]
    public async Task ValidTokenResolvesAnonymouslyWithMinimalNoStoreResponseAndSetsOpenedOnce()
    {
        var invitation = await InsertInvitationAsync("fresh");
        using var client = factory.CreateSecureClient();
        using var request = Request(invitation.Token);
        var auditBefore = await factory.CountAllBusinessAuditLogsAsync();

        var response = await client.SendAsync(request);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
        var result = await response.Content.ReadFromJsonAsync<PublicClientInvitationResponse>();
        Assert.NotNull(result);
        Assert.Equal("Ready", result.State);
        Assert.Equal("Public Intake Client", result.ClientDisplayName);
        Assert.False(result.HasCreatedClient);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("invitationId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("createdClientId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokenHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mobile", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(invitation.Token, json, StringComparison.Ordinal);

        var firstOpenedAt = await OpenedAtAsync(invitation.Id);
        Assert.NotNull(firstOpenedAt);
        using var repeatedRequest = Request(invitation.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(repeatedRequest)).StatusCode);
        Assert.Equal(firstOpenedAt, await OpenedAtAsync(invitation.Id));
        Assert.Equal(auditBefore, await factory.CountAllBusinessAuditLogsAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64url***")]
    [InlineData("YWJj")]
    public async Task MissingMalformedAndWrongLengthTokensAreRejectedSafely(string? token)
    {
        using var client = factory.CreateSecureClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/client-intake/invitation");
        if (token is not null) request.Headers.TryAddWithoutValidation(HeaderName, token);

        var response = await client.SendAsync(request);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest });
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid", json, StringComparison.Ordinal);
        Assert.DoesNotContain("exception", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownTokenIsGenericAndDoesNotOpenAnotherInvitation()
    {
        var invitation = await InsertInvitationAsync("fresh");
        var unknown = Encode(RandomNumberGenerator.GetBytes(32));
        using var client = factory.CreateSecureClient();
        using var request = Request(unknown);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Invalid", (await response.Content.ReadFromJsonAsync<PublicClientInvitationResponse>())?.State);
        Assert.NotNull(await OpenedAtAsync(invitation.Id));
    }

    [Fact]
    public async Task TokenInUrlIsNotAccepted()
    {
        var invitation = await InsertInvitationAsync("fresh");
        using var client = factory.CreateSecureClient();

        var response = await client.GetAsync(
            $"/api/public/client-intake/invitation?token={Uri.EscapeDataString(invitation.Token)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(await OpenedAtAsync(invitation.Id));
    }

    [Theory]
    [InlineData("expired", "Expired")]
    [InlineData("revoked", "Unavailable")]
    [InlineData("completed", "Completed")]
    public async Task UnusableLifecycleStatesAreSafeForAnExistingSession(
        string persistedState,
        string expectedState)
    {
        var invitation = await InsertInvitationAsync(persistedState);
        using var client = factory.CreateSecureClient();
        using var request = Request(invitation.Token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedState,
            (await response.Content.ReadFromJsonAsync<PublicClientInvitationResponse>())?.State);
        Assert.NotNull(await OpenedAtAsync(invitation.Id));
    }

    [Theory]
    [InlineData("sent")]
    [InlineData("opened")]
    [InlineData("client-created")]
    public async Task SentOpenedAndCreatedClientStatesRemainUsable(string persistedState)
    {
        var invitation = await InsertInvitationAsync(persistedState);
        var originalOpenedAt = await OpenedAtAsync(invitation.Id);
        using var client = factory.CreateSecureClient();
        using var request = Request(invitation.Token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PublicClientInvitationResponse>();
        Assert.NotNull(result);
        Assert.Equal("Ready", result.State);
        Assert.Equal(persistedState == "client-created", result.HasCreatedClient);
        if (persistedState == "opened")
        {
            Assert.Equal(originalOpenedAt, await OpenedAtAsync(invitation.Id));
        }
    }

    [Fact]
    public async Task PublicEndpointDoesNotMakeExistingClientApiAnonymousOrTrustTenantHeaders()
    {
        var invitation = await InsertInvitationAsync("fresh");
        using var client = factory.CreateSecureClient();
        using var publicRequest = Request(invitation.Token);
        publicRequest.Headers.TryAddWithoutValidation("X-Company-Id", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(publicRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/clients")).StatusCode);
    }

    [Fact]
    public void EndpointDeclaresFocusedRateLimitAndNoStorePolicy()
    {
        var method = typeof(PublicClientIntakeController).GetMethod(
            nameof(PublicClientIntakeController.ResolveInvitation),
            BindingFlags.Instance | BindingFlags.Public)!;

        var rateLimit = Assert.Single(method.GetCustomAttributes<EnableRateLimitingAttribute>());
        Assert.Equal(
            CareComplyAuthenticationServiceCollectionExtensions.PublicClientIntakeRateLimitPolicy,
            rateLimit.PolicyName);
        var cache = Assert.Single(method.GetCustomAttributes<ResponseCacheAttribute>());
        Assert.True(cache.NoStore);
        Assert.Equal(ResponseCacheLocation.None, cache.Location);
    }

    [Fact]
    public async Task TokenScopedLocationsReturnMinimalActiveCompanyLocations()
    {
        var invitation = await InsertInvitationAsync("fresh");
        using var client = factory.CreateSecureClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/client-intake/locations");
        request.Headers.Add(HeaderName, invitation.Token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.Equal("Ready", document.RootElement.GetProperty("state").GetString());
        Assert.NotEmpty(document.RootElement.GetProperty("locations").EnumerateArray());
        Assert.DoesNotContain("companyId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(invitation.Token, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidStepOneAtomicallyCreatesPendingClientAndBindsInvitation()
    {
        var invitation = await InsertInvitationAsync("fresh");
        var locationId = await MainLocationIdAsync();
        var pmi = Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        using var client = factory.CreateSecureClient();
        using var request = CreateBasicDetailsRequest(invitation.Token, pmi, locationId);

        var response = await client.SendAsync(request);

        var json = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("InProgress", document.RootElement.GetProperty("state").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("nextStep").GetInt32());
        Assert.Equal(64, document.RootElement.GetProperty("editorVersion").GetString()!.Length);
        Assert.DoesNotContain("clientId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ssn", json, StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persistedInvitation = await db.ClientGuestInvitations.AsNoTracking()
            .SingleAsync(value => value.Id == invitation.Id);
        Assert.NotNull(persistedInvitation.CreatedClientId);
        Assert.NotNull(persistedInvitation.ClientCreatedAtUtc);
        Assert.Null(persistedInvitation.CompletedAtUtc);
        var persistedClient = await db.Clients.AsNoTracking()
            .SingleAsync(value => value.Id == persistedInvitation.CreatedClientId);
        Assert.Equal(SqlServerApiFactory.DefaultCompanyId, persistedClient.CompanyId);
        Assert.Equal(locationId, persistedClient.LocationId);
        Assert.Equal(ClientStatus.Pending, persistedClient.Status);
        Assert.Equal(2, persistedClient.CreationProgressStep);
        Assert.True(persistedClient.IsOnboarding);
        Assert.NotEqual("123-45-6789", persistedClient.ProtectedSsn);
        Assert.True(string.IsNullOrEmpty(persistedClient.Email));
        Assert.True(string.IsNullOrEmpty(persistedClient.PrimaryPhone));
        Assert.False(await db.ClientAdditionalProfiles.AnyAsync(value => value.ClientId == persistedClient.Id));
        Assert.False(await db.ClientContacts.AnyAsync(value => value.ClientId == persistedClient.Id));
        Assert.Equal(2, await db.ClientServices.CountAsync(value => value.ClientId == persistedClient.Id));
    }

    [Fact]
    public async Task RepeatedStepOneReturnsContinuationWithoutCreatingAnotherClient()
    {
        var invitation = await InsertInvitationAsync("fresh");
        var locationId = await MainLocationIdAsync();
        var pmi = Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        using var client = factory.CreateSecureClient();
        using var first = CreateBasicDetailsRequest(invitation.Token, pmi, locationId);
        var firstResponse = await client.SendAsync(first);
        Assert.True(
            firstResponse.StatusCode == HttpStatusCode.OK,
            await firstResponse.Content.ReadAsStringAsync());
        using var second = CreateBasicDetailsRequest(invitation.Token, pmi, locationId);

        var response = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "AlreadyStarted",
            (await response.Content.ReadFromJsonAsync<PublicClientIntakeContinuationResponse>())?.State);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal(1, await db.Clients.CountAsync(value => value.PmiNumber == pmi));
    }

    [Fact]
    public async Task InvalidLocationCreatesNoClientAndLeavesInvitationUnbound()
    {
        var invitation = await InsertInvitationAsync("fresh");
        var pmi = Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        using var client = factory.CreateSecureClient();
        using var request = CreateBasicDetailsRequest(invitation.Token, pmi, Guid.NewGuid());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.False(await db.Clients.AnyAsync(value => value.PmiNumber == pmi));
        var persistedInvitation = await db.ClientGuestInvitations.AsNoTracking()
            .SingleAsync(value => value.Id == invitation.Id);
        Assert.Null(persistedInvitation.CreatedClientId);
        Assert.Null(persistedInvitation.ClientCreatedAtUtc);
    }

    [Theory]
    [InlineData("expired", HttpStatusCode.Gone, "Expired")]
    [InlineData("revoked", HttpStatusCode.Conflict, "Unavailable")]
    [InlineData("completed", HttpStatusCode.Conflict, "Completed")]
    public async Task StepOneRevalidatesInvitationLifecycle(
        string state,
        HttpStatusCode expectedStatus,
        string expectedState)
    {
        var invitation = await InsertInvitationAsync(state);
        var locationId = await MainLocationIdAsync();
        var pmi = Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        using var client = factory.CreateSecureClient();
        using var request = CreateBasicDetailsRequest(invitation.Token, pmi, locationId);

        var response = await client.SendAsync(request);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(
            expectedState,
            (await response.Content.ReadFromJsonAsync<PublicClientIntakeStateResponse>())?.State);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.False(await db.Clients.AnyAsync(value => value.PmiNumber == pmi));
    }

    [Fact]
    public async Task ConcurrentStepOneRequestsCannotCreateTwoClients()
    {
        var invitation = await InsertInvitationAsync("fresh");
        var locationId = await MainLocationIdAsync();
        var pmi = Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        using var firstClient = factory.CreateSecureClient();
        using var secondClient = factory.CreateSecureClient();
        using var first = CreateBasicDetailsRequest(invitation.Token, pmi, locationId);
        using var second = CreateBasicDetailsRequest(invitation.Token, pmi, locationId);

        var responses = await Task.WhenAll(firstClient.SendAsync(first), secondClient.SendAsync(second));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal(1, await db.Clients.CountAsync(value => value.PmiNumber == pmi));
        var persistedInvitation = await db.ClientGuestInvitations.AsNoTracking()
            .SingleAsync(value => value.Id == invitation.Id);
        Assert.NotNull(persistedInvitation.CreatedClientId);
    }

    [Fact]
    public async Task StepTwoHydratesCreatesUpdatesAndEnforcesOptimisticConcurrency()
    {
        var invitation = await InsertInvitationAsync("fresh");
        var locationId = await MainLocationIdAsync();
        var pmi = Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        using var http = factory.CreateSecureClient();
        using var stepOne = CreateBasicDetailsRequest(invitation.Token, pmi, locationId);
        var stepOneResponse = await http.SendAsync(stepOne);
        var continuation = await stepOneResponse.Content.ReadFromJsonAsync<PublicClientIntakeContinuationResponse>();
        Assert.NotNull(continuation);

        using var get = AdditionalInformationRequest(HttpMethod.Get, invitation.Token);
        var getResponse = await http.SendAsync(get);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getJson = await getResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("clientId", getJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId", getJson, StringComparison.OrdinalIgnoreCase);

        using var update = AdditionalInformationRequest(
            HttpMethod.Put,
            invitation.Token,
            continuation.EditorVersion,
            "First guest note");
        var updateResponse = await http.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<PublicClientIntakeStepContinuationResponse>();
        Assert.NotNull(updated);
        Assert.Equal(3, updated.NextStep);
        Assert.NotEqual(continuation.EditorVersion, updated.EditorVersion);

        using var stale = AdditionalInformationRequest(
            HttpMethod.Put,
            invitation.Token,
            continuation.EditorVersion,
            "Must not persist");
        Assert.Equal(HttpStatusCode.Conflict, (await http.SendAsync(stale)).StatusCode);

        using var repeat = AdditionalInformationRequest(
            HttpMethod.Put,
            invitation.Token,
            updated.EditorVersion,
            "Updated guest note");
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(repeat)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persistedInvitation = await db.ClientGuestInvitations.AsNoTracking()
            .SingleAsync(value => value.Id == invitation.Id);
        var client = await db.Clients.AsNoTracking()
            .SingleAsync(value => value.Id == persistedInvitation.CreatedClientId);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.Equal(SqlServerApiFactory.DefaultCompanyId, client.CompanyId);
        Assert.Equal(locationId, client.LocationId);
        Assert.Equal(3, client.CreationProgressStep);
        var profiles = await db.ClientAdditionalProfiles.AsNoTracking()
            .Where(value => value.ClientId == client.Id)
            .ToArrayAsync();
        Assert.Single(profiles);
        Assert.Equal("Updated guest note", profiles[0].Notes);
        Assert.NotEqual("Must not persist", profiles[0].Notes);
        Assert.Equal(2, await db.ClientServices.CountAsync(value => value.ClientId == client.Id));
        Assert.False(await db.ClientLanguagePreferences.AnyAsync(value => value.ClientId == client.Id));
    }

    [Fact]
    public async Task StepThreePrefillsWithoutPersistingThenAtomicallyUpdatesContactLanguagesAndProgress()
    {
        var invitation = await InsertInvitationAsync("fresh");
        var locationId = await MainLocationIdAsync();
        var pmi = Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        using var http = factory.CreateSecureClient();

        using var stepOne = CreateBasicDetailsRequest(invitation.Token, pmi, locationId);
        var stepOneResponse = await http.SendAsync(stepOne);
        var firstContinuation = await stepOneResponse.Content
            .ReadFromJsonAsync<PublicClientIntakeContinuationResponse>();
        Assert.NotNull(firstContinuation);
        using var stepTwo = AdditionalInformationRequest(
            HttpMethod.Put, invitation.Token, firstContinuation.EditorVersion, "Step three setup");
        var stepTwoResponse = await http.SendAsync(stepTwo);
        var secondContinuation = await stepTwoResponse.Content
            .ReadFromJsonAsync<PublicClientIntakeStepContinuationResponse>();
        Assert.NotNull(secondContinuation);

        using var get = ContactInformationRequest(HttpMethod.Get, invitation.Token);
        var getResponse = await http.SendAsync(get);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getJson = await getResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("clientId", getJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId", getJson, StringComparison.OrdinalIgnoreCase);
        var hydrated = await getResponse.Content
            .ReadFromJsonAsync<PublicClientIntakeContactInformationResponse>();
        Assert.NotNull(hydrated);
        Assert.StartsWith("invite-", hydrated.ContactInformation.Email, StringComparison.Ordinal);
        Assert.Equal("+16125550100", hydrated.ContactInformation.PrimaryPhone);
        Assert.Empty(hydrated.ContactInformation.PreferredLanguages);

        await using (var beforeScope = factory.Services.CreateAsyncScope())
        {
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var boundId = await beforeDb.ClientGuestInvitations.AsNoTracking()
                .Where(value => value.Id == invitation.Id)
                .Select(value => value.CreatedClientId)
                .SingleAsync();
            var before = await beforeDb.Clients.AsNoTracking().SingleAsync(value => value.Id == boundId);
            Assert.True(string.IsNullOrEmpty(before.Email));
            Assert.True(string.IsNullOrEmpty(before.PrimaryPhone));
        }

        using var update = ContactInformationRequest(
            HttpMethod.Put, invitation.Token, secondContinuation.EditorVersion, "(612) 555-0111");
        var updateResponse = await http.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content
            .ReadFromJsonAsync<PublicClientIntakeStepContinuationResponse>();
        Assert.NotNull(updated);
        Assert.Equal(4, updated.NextStep);
        Assert.NotEqual(secondContinuation.EditorVersion, updated.EditorVersion);

        using var stale = ContactInformationRequest(
            HttpMethod.Put, invitation.Token, secondContinuation.EditorVersion, "(612) 555-0999");
        Assert.Equal(HttpStatusCode.Conflict, (await http.SendAsync(stale)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persistedInvitation = await db.ClientGuestInvitations.AsNoTracking()
            .SingleAsync(value => value.Id == invitation.Id);
        var client = await db.Clients.AsNoTracking()
            .SingleAsync(value => value.Id == persistedInvitation.CreatedClientId);
        Assert.Equal("guest.contact@example.test", client.Email);
        Assert.Equal("(612) 555-0111", client.PrimaryPhone);
        Assert.Equal("100 Guest Street", client.AddressLine1);
        Assert.Equal(4, client.CreationProgressStep);
        Assert.Equal("+16125550100", persistedInvitation.MobileNumber);
        Assert.NotEqual(client.Email, persistedInvitation.Email);
        var languages = await db.ClientLanguagePreferences.AsNoTracking()
            .Where(value => value.ClientId == client.Id)
            .OrderBy(value => value.LanguageCode)
            .ToArrayAsync();
        Assert.Equal(2, languages.Length);
        Assert.Single(languages, value => value.IsPrimary && value.LanguageCode == "en");
        Assert.Single(languages, value => !value.IsPrimary && value.LanguageCode == "es");
        Assert.False(await db.Set<AuditLog>().AnyAsync(value =>
            value.Action == "Client.Updated" && value.ResourceId == client.Id.ToString("N")));
    }

    [Fact]
    public async Task StepThreeGetUsesPersistedValuesAndLeavesMissingInvitationMobileBlank()
    {
        var blankInvitation = await InsertInvitationAsync("fresh", null);
        using var http = factory.CreateSecureClient();
        var blankContinuation = await AdvanceToStepThreeAsync(http, blankInvitation.Token);
        using var blankGet = ContactInformationRequest(HttpMethod.Get, blankInvitation.Token);
        var blankResponse = await http.SendAsync(blankGet);
        var blankState = await blankResponse.Content
            .ReadFromJsonAsync<PublicClientIntakeContactInformationResponse>();
        Assert.NotNull(blankState);
        Assert.True(string.IsNullOrEmpty(blankState.ContactInformation.PrimaryPhone));

        var persistedInvitation = await InsertInvitationAsync("fresh");
        await AdvanceToStepThreeAsync(http, persistedInvitation.Token);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var invitation = await db.ClientGuestInvitations
                .SingleAsync(value => value.Id == persistedInvitation.Id);
            var client = await db.Clients.SingleAsync(value => value.Id == invitation.CreatedClientId);
            client.ChangeContact(
                "persisted@example.test", "(612) 555-0222", null, "Persisted Street", null,
                "Saint Paul", "MN", "55101", "Ramsey", "United States", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        using var persistedGet = ContactInformationRequest(HttpMethod.Get, persistedInvitation.Token);
        var persistedResponse = await http.SendAsync(persistedGet);
        var persistedState = await persistedResponse.Content
            .ReadFromJsonAsync<PublicClientIntakeContactInformationResponse>();
        Assert.NotNull(persistedState);
        Assert.Equal("persisted@example.test", persistedState.ContactInformation.Email);
        Assert.Equal("(612) 555-0222", persistedState.ContactInformation.PrimaryPhone);
        Assert.NotEqual(blankContinuation.EditorVersion, persistedState.EditorVersion);
    }

    [Fact]
    public async Task StepThreeRepeatedSaveReplacesLanguagesAndDoesNotReduceProgress()
    {
        var invitation = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        var continuation = await AdvanceToStepThreeAsync(http, invitation.Token);
        using var first = ContactInformationRequest(
            HttpMethod.Put, invitation.Token, continuation.EditorVersion, "(612) 555-0300");
        var firstResponse = await http.SendAsync(first);
        var firstResult = await firstResponse.Content
            .ReadFromJsonAsync<PublicClientIntakeStepContinuationResponse>();
        Assert.NotNull(firstResult);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var clientId = await db.ClientGuestInvitations.AsNoTracking()
                .Where(value => value.Id == invitation.Id)
                .Select(value => value.CreatedClientId)
                .SingleAsync();
            var client = await db.Clients.SingleAsync(value => value.Id == clientId);
            client.AdvanceCreationProgress(5, DateTimeOffset.UtcNow);
            client.AdvanceCreationProgress(6, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        using var refresh = ContactInformationRequest(HttpMethod.Get, invitation.Token);
        var refreshedResponse = await http.SendAsync(refresh);
        var refreshed = await refreshedResponse.Content
            .ReadFromJsonAsync<PublicClientIntakeContactInformationResponse>();
        Assert.NotNull(refreshed);
        Assert.NotEqual(firstResult.EditorVersion, refreshed.EditorVersion);

        using var second = ContactInformationRequest(
            HttpMethod.Put,
            invitation.Token,
            refreshed.EditorVersion,
            "(612) 555-0333",
            [("es", "Spanish", true)]);
        var secondResponse = await http.SendAsync(second);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var boundClientId = await verificationDb.ClientGuestInvitations.AsNoTracking()
            .Where(value => value.Id == invitation.Id)
            .Select(value => value.CreatedClientId)
            .SingleAsync();
        var persisted = await verificationDb.Clients.AsNoTracking()
            .SingleAsync(value => value.Id == boundClientId);
        Assert.Equal(6, persisted.CreationProgressStep);
        Assert.Equal(ClientStatus.Pending, persisted.Status);
        Assert.Equal("(612) 555-0333", persisted.PrimaryPhone);
        var language = Assert.Single(await verificationDb.ClientLanguagePreferences.AsNoTracking()
            .Where(value => value.ClientId == boundClientId)
            .ToArrayAsync());
        Assert.Equal("es", language.LanguageCode);
        Assert.True(language.IsPrimary);
        Assert.Equal(1, await verificationDb.ClientAdditionalProfiles.CountAsync(value => value.ClientId == boundClientId));
        Assert.False(await verificationDb.ClientCaregivers.AnyAsync(value => value.ClientId == boundClientId));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StepThreeRejectsInvalidLanguageSetsWithoutMutation(bool duplicateCodes)
    {
        var invitation = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        var continuation = await AdvanceToStepThreeAsync(http, invitation.Token);
        IReadOnlyList<(string Code, string Name, bool Primary)> languages = duplicateCodes
            ? [("en", "English", true), ("EN", "English duplicate", false)]
            : [("en", "English", false), ("es", "Spanish", false)];
        using var invalid = ContactInformationRequest(
            HttpMethod.Put, invitation.Token, continuation.EditorVersion, "(612) 555-0444", languages);
        var response = await http.SendAsync(invalid);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(problem.Errors.Keys, key => key.Contains("Languages", StringComparison.Ordinal));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var clientId = await db.ClientGuestInvitations.AsNoTracking()
            .Where(value => value.Id == invitation.Id)
            .Select(value => value.CreatedClientId)
            .SingleAsync();
        var client = await db.Clients.AsNoTracking().SingleAsync(value => value.Id == clientId);
        Assert.Equal(3, client.CreationProgressStep);
        Assert.True(string.IsNullOrEmpty(client.PrimaryPhone));
        Assert.False(await db.ClientLanguagePreferences.AnyAsync(value => value.ClientId == clientId));
    }

    [Fact]
    public async Task StepThreeRejectsInvalidUnboundAndIncompleteIntakesWithoutAdvancingProgress()
    {
        using var http = factory.CreateSecureClient();
        using var invalid = ContactInformationRequest(HttpMethod.Get, "not-a-valid-token");
        Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(invalid)).StatusCode);

        var unbound = await InsertInvitationAsync("fresh");
        using var unboundRequest = ContactInformationRequest(HttpMethod.Get, unbound.Token);
        Assert.Equal(HttpStatusCode.Conflict, (await http.SendAsync(unboundRequest)).StatusCode);

        var incomplete = await InsertInvitationAsync("fresh");
        var locationId = await MainLocationIdAsync();
        var pmi = Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        using var stepOne = CreateBasicDetailsRequest(incomplete.Token, pmi, locationId);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(stepOne)).StatusCode);
        using var premature = ContactInformationRequest(HttpMethod.Get, incomplete.Token);
        Assert.Equal(HttpStatusCode.Conflict, (await http.SendAsync(premature)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var clientId = await db.ClientGuestInvitations.AsNoTracking()
            .Where(value => value.Id == incomplete.Id)
            .Select(value => value.CreatedClientId)
            .SingleAsync();
        Assert.Equal(2, await db.Clients.AsNoTracking()
            .Where(value => value.Id == clientId)
            .Select(value => value.CreationProgressStep)
            .SingleAsync());
    }

    private async Task<(Guid Id, string Token, string InvitationToken)> InsertInvitationAsync(
        string state,
        string? mobileNumber = "+16125550100")
    {
        var creator = await factory.CreateUserAsync(
            $"public-intake-{Guid.NewGuid():N}@example.test", Password);
        var now = DateTimeOffset.UtcNow;
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var sessionTokenBytes = RandomNumberGenerator.GetBytes(32);
        var csrfTokenBytes = RandomNumberGenerator.GetBytes(32);
        var createdAt = state == "expired" ? now.AddDays(-2) : now;
        var expiresAt = state == "expired" ? now.AddDays(-1) : now.AddDays(7);
        var invitation = ClientGuestInvitation.Create(
            Guid.CreateVersion7(),
            SqlServerApiFactory.DefaultCompanyId,
            "Public Intake Client",
            $"invite-{Guid.NewGuid():N}@example.test",
            mobileNumber,
            SHA256.HashData(tokenBytes),
            expiresAt,
            createdAt,
            creator.Id);
        if (state == "sent") invitation.MarkSent(now);
        if (state == "opened") invitation.MarkOpened(now.AddMinutes(-5));
        if (state == "revoked") invitation.Revoke(now);
        if (state == "completed") invitation.Complete(now);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        if (state == "client-created")
        {
            var location = await context.CompanyLocations.AsNoTracking()
                .FirstAsync(value => value.CompanyId == SqlServerApiFactory.DefaultCompanyId);
            var client = Client.Create(
                Guid.CreateVersion7(), SqlServerApiFactory.DefaultCompanyId, "Guest", null, "Pending",
                null, new DateOnly(1990, 1, 1), ClientGender.Unknown, "12345678",
                DateOnly.FromDateTime(DateTime.UtcNow), null, ClientStatus.Pending, now);
            client.AssignLocation(location.Id, location.Name, now);
            context.Clients.Add(client);
            invitation.BindCreatedClient(client.Id, now);
        }
        context.ClientGuestInvitations.Add(invitation);
        invitation.Redeem(now);
        context.ClientGuestSessions.Add(ClientGuestSession.Create(
            Guid.CreateVersion7(), invitation.Id, SHA256.HashData(sessionTokenBytes),
            SHA256.HashData(csrfTokenBytes), now, now.AddHours(8)));
        await context.SaveChangesAsync();
        var sessionToken = Encode(sessionTokenBytes);
        CsrfTokens[sessionToken] = Encode(csrfTokenBytes);
        return (invitation.Id, sessionToken, Encode(tokenBytes));
    }

    [Fact]
    public async Task UnusedInvitationRedeemsOnceIntoHashedHttpOnlySession()
    {
        var seeded = await InsertUnredeemedInvitationAsync();
        using var http = factory.CreateSecureClient();
        using var redeem = new HttpRequestMessage(HttpMethod.Post, "/api/public/client-intake/redeem");
        redeem.Headers.Add(HeaderName, seeded.Token);

        var response = await http.SendAsync(redeem);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Ready", body);
        Assert.DoesNotContain(seeded.Token, body, StringComparison.Ordinal);
        Assert.DoesNotContain("sessionToken", body, StringComparison.OrdinalIgnoreCase);
        var setCookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(setCookies, value => value.StartsWith(
            PublicClientIntakeController.SessionCookieName, StringComparison.Ordinal)
            && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var invitation = await db.ClientGuestInvitations.AsNoTracking()
            .SingleAsync(value => value.Id == seeded.Id);
        Assert.NotNull(invitation.RedeemedAtUtc);
        var session = await db.ClientGuestSessions.AsNoTracking()
            .SingleAsync(value => value.ClientGuestInvitationId == seeded.Id);
        Assert.Equal(32, session.SessionTokenHash.Length);
        Assert.Equal(32, session.CsrfTokenHash.Length);

        using var repeated = new HttpRequestMessage(HttpMethod.Post, "/api/public/client-intake/redeem");
        repeated.Headers.Add(HeaderName, seeded.Token);
        var repeatedResponse = await http.SendAsync(repeated);
        Assert.Equal("Used", (await repeatedResponse.Content
            .ReadFromJsonAsync<PublicClientGuestAccessResponse>())?.State);
        Assert.Equal(invitation.RedeemedAtUtc, await db.ClientGuestInvitations.AsNoTracking()
            .Where(value => value.Id == seeded.Id).Select(value => value.RedeemedAtUtc).SingleAsync());
    }

    [Fact]
    public async Task ConcurrentRedemptionIssuesExactlyOneGuestSession()
    {
        var seeded = await InsertUnredeemedInvitationAsync();
        using var firstClient = factory.CreateSecureClient();
        using var secondClient = factory.CreateSecureClient();
        using var first = new HttpRequestMessage(HttpMethod.Post, "/api/public/client-intake/redeem");
        using var second = new HttpRequestMessage(HttpMethod.Post, "/api/public/client-intake/redeem");
        first.Headers.Add(HeaderName, seeded.Token);
        second.Headers.Add(HeaderName, seeded.Token);

        var responses = await Task.WhenAll(firstClient.SendAsync(first), secondClient.SendAsync(second));
        var states = await Task.WhenAll(responses.Select(async response =>
            (await response.Content.ReadFromJsonAsync<PublicClientGuestAccessResponse>())!.State));

        Assert.Single(states, value => value == "Ready");
        Assert.Single(states, value => value == "Used");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal(1, await db.ClientGuestSessions.CountAsync(
            value => value.ClientGuestInvitationId == seeded.Id));
    }

    [Fact]
    public async Task SessionRefreshResumesAndExpiredSessionIsRejected()
    {
        var seeded = await InsertUnredeemedInvitationAsync();
        using var http = factory.CreateSecureClient();
        using var redeem = new HttpRequestMessage(HttpMethod.Post, "/api/public/client-intake/redeem");
        redeem.Headers.Add(HeaderName, seeded.Token);
        var redeemed = await http.SendAsync(redeem);
        var sessionCookie = Assert.Single(redeemed.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(PublicClientIntakeController.SessionCookieName, StringComparison.Ordinal))
            .Split(';')[0];

        using var refresh = new HttpRequestMessage(HttpMethod.Get, "/api/public/client-intake/session");
        refresh.Headers.Add("Cookie", sessionCookie);
        var ready = await (await http.SendAsync(refresh)).Content
            .ReadFromJsonAsync<PublicClientGuestAccessResponse>();
        Assert.NotNull(ready);
        Assert.Equal("Ready", ready.State);
        Assert.False(ready.HasCreatedClient);
        Assert.Equal(1, ready.CreationProgressStep);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            await db.ClientGuestSessions.Where(value => value.ClientGuestInvitationId == seeded.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    value => value.LastActivityAtUtc, DateTimeOffset.UtcNow.AddHours(-2)));
        }

        using var expiredRefresh = new HttpRequestMessage(HttpMethod.Get, "/api/public/client-intake/session");
        expiredRefresh.Headers.Add("Cookie", sessionCookie);
        var expired = await (await http.SendAsync(expiredRefresh)).Content
            .ReadFromJsonAsync<PublicClientGuestAccessResponse>();
        Assert.Equal("SessionExpired", expired?.State);
    }

    [Fact]
    public async Task OriginalTokenCannotAuthorizeStepsAndCookieMutationRequiresCsrf()
    {
        var seeded = await InsertUnredeemedInvitationAsync();
        using var http = factory.CreateSecureClient();
        using var redeem = new HttpRequestMessage(HttpMethod.Post, "/api/public/client-intake/redeem");
        redeem.Headers.Add(HeaderName, seeded.Token);
        var redeemed = await http.SendAsync(redeem);
        var cookies = redeemed.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';')[0]).ToArray();
        var sessionCookie = Assert.Single(cookies, value => value.StartsWith(
            PublicClientIntakeController.SessionCookieName, StringComparison.Ordinal));
        var csrfCookie = Assert.Single(cookies, value => value.StartsWith(
            PublicClientIntakeController.CsrfCookieName, StringComparison.Ordinal));
        var csrf = csrfCookie[(csrfCookie.IndexOf('=') + 1)..];

        using var replayClient = factory.CreateSecureClient();
        using var replay = new HttpRequestMessage(HttpMethod.Get, "/api/public/client-intake/locations");
        replay.Headers.Add(HeaderName, seeded.Token);
        var replayResponse = await replayClient.SendAsync(replay);
        Assert.Equal("Invalid", (await replayResponse.Content
            .ReadFromJsonAsync<PublicClientIntakeLocationsResponse>())?.State);

        var locationId = await MainLocationIdAsync();
        using var missingCsrf = CreateBasicDetailsRequest("unused", RandomPmi(), locationId);
        missingCsrf.Headers.Remove(HeaderName);
        missingCsrf.Headers.Add("Cookie", $"{sessionCookie}; {csrfCookie}");
        Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(missingCsrf)).StatusCode);

        using var legitimate = CreateBasicDetailsRequest("unused", RandomPmi(), locationId);
        legitimate.Headers.Remove(HeaderName);
        legitimate.Headers.Remove("Cookie");
        legitimate.Headers.Add("Cookie", $"{sessionCookie}; {csrfCookie}");
        legitimate.Headers.Add(PublicClientIntakeController.CsrfHeaderName, csrf);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(legitimate)).StatusCode);
    }

    private async Task<(Guid Id, string Token)> InsertUnredeemedInvitationAsync()
    {
        var creator = await factory.CreateUserAsync(
            $"redeem-{Guid.NewGuid():N}@example.test", Password);
        var bytes = RandomNumberGenerator.GetBytes(32);
        var now = DateTimeOffset.UtcNow;
        var invitation = ClientGuestInvitation.Create(
            Guid.CreateVersion7(), SqlServerApiFactory.DefaultCompanyId, "One Time Guest",
            $"redeem-{Guid.NewGuid():N}@example.test", null, SHA256.HashData(bytes),
            now.AddDays(7), now, creator.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.ClientGuestInvitations.Add(invitation);
        await db.SaveChangesAsync();
        return (invitation.Id, Encode(bytes));
    }

    private static string RandomPmi() =>
        Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);

    private async Task<DateTimeOffset?> OpenedAtAsync(Guid invitationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await context.ClientGuestInvitations.AsNoTracking()
            .Where(value => value.Id == invitationId)
            .Select(value => value.OpenedAtUtc)
            .SingleAsync();
    }

    private async Task<Guid> MainLocationIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await context.CompanyLocations.AsNoTracking()
            .Where(value => value.CompanyId == SqlServerApiFactory.DefaultCompanyId && value.IsActive)
            .Select(value => value.Id)
            .FirstAsync();
    }

    [Fact]
    public async Task StepFourCandidatesAreEligibleAndExposeOnlyMinimalIdentity()
    {
        var invitation = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepFourAsync(http, invitation.Token);
        var locationId = await MainLocationIdAsync();
        var eligibleId = await CreateEmployeeAsync(locationId, "Eligible", "Caregiver", "RN");

        using var request = CaregiverRequest(HttpMethod.Get, invitation.Token, "caregiver-candidates");
        var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candidates = await response.Content
            .ReadFromJsonAsync<IReadOnlyList<PublicClientCaregiverCandidateResponse>>();
        var eligible = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<PublicClientCaregiverCandidateResponse>>(candidates),
            value => value.Id == eligibleId);
        Assert.Equal("Eligible Caregiver", eligible.DisplayName);
        Assert.Equal("RN", eligible.JobTitle);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("locationId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StepFourEligibilityExcludesCrossTenantCrossLocationInactiveAndNonEmployees()
    {
        var invitation = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepFourAsync(http, invitation.Token);
        var mainLocation = await MainLocationIdAsync();
        var otherLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        var (otherCompany, otherCompanyLocation) = await CreateCompanyAndLocationAsync();
        var eligible = await CreateEmployeeAsync(mainLocation, "Only", "Eligible", "PCA");
        var crossLocation = await CreateEmployeeAsync(otherLocation, "Wrong", "Location", "PCA");
        var crossCompany = await CreateEmployeeAsync(otherCompanyLocation, "Wrong", "Company", "PCA", otherCompany);
        var inactive = await CreateEmployeeAsync(mainLocation, "Inactive", "Employee", "PCA", isActive: false);
        var nonEmployee = await CreateEmployeeAsync(mainLocation, "Not", "Employee", "PCA", isEmployee: false);

        using var candidatesRequest = CaregiverRequest(HttpMethod.Get, invitation.Token, "caregiver-candidates");
        var candidates = await (await http.SendAsync(candidatesRequest)).Content
            .ReadFromJsonAsync<IReadOnlyList<PublicClientCaregiverCandidateResponse>>();
        Assert.NotNull(candidates);
        Assert.Contains(candidates, value => value.Id == eligible);
        Assert.DoesNotContain(candidates, value => value.Id == crossLocation);
        Assert.DoesNotContain(candidates, value => value.Id == crossCompany);
        Assert.DoesNotContain(candidates, value => value.Id == inactive);
        Assert.DoesNotContain(candidates, value => value.Id == nonEmployee);

        foreach (var rejected in new[] { crossLocation, crossCompany, inactive, nonEmployee })
        {
            using var create = SaveCaregiverRequest(HttpMethod.Post, invitation.Token, null, rejected);
            Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(create)).StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var clientId = await db.ClientGuestInvitations.AsNoTracking()
            .Where(value => value.Id == invitation.Id)
            .Select(value => value.CreatedClientId!.Value)
            .SingleAsync();
        Assert.False(await db.ClientCaregivers.AnyAsync(value => value.ClientId == clientId));
    }

    [Fact]
    public async Task StepFourCreateUpdateEndRoundTripsWithoutAdvancingOrStaffAuditAttribution()
    {
        var invitation = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepFourAsync(http, invitation.Token);
        var caregiverId = await CreateEmployeeAsync(await MainLocationIdAsync(), "Guest", "Caregiver", "PCA");
        var auditBefore = await factory.CountAllBusinessAuditLogsAsync();

        using var create = SaveCaregiverRequest(HttpMethod.Post, invitation.Token, null, caregiverId);
        var createResponse = await http.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PublicClientCaregiverResponse>();
        Assert.NotNull(created);
        Assert.Equal("Guest Caregiver", created.CaregiverDisplayName);
        Assert.Equal("Primary", created.AssignmentType);
        Assert.Equal("Daughter", created.Relationship);
        Assert.True(created.LivesWithClient);

        using var list = CaregiverRequest(HttpMethod.Get, invitation.Token, "caregivers");
        var listResponse = await http.SendAsync(list);
        var state = await listResponse.Content.ReadFromJsonAsync<PublicClientCaregiverListResponse>();
        Assert.NotNull(state);
        Assert.Equal(4, state.CreationProgressStep);
        Assert.Contains("PCA", state.AvailableServices);
        Assert.Single(state.Caregivers);

        using var duplicate = SaveCaregiverRequest(HttpMethod.Post, invitation.Token, null, caregiverId);
        Assert.Equal(HttpStatusCode.Conflict, (await http.SendAsync(duplicate)).StatusCode);

        using var update = SaveCaregiverRequest(
            HttpMethod.Put, invitation.Token, created.EditorVersion, caregiverId, created.AssignmentId, "Backup");
        var updateResponse = await http.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<PublicClientCaregiverResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Backup", updated.AssignmentType);
        Assert.NotEqual(created.EditorVersion, updated.EditorVersion);

        using var end = CaregiverRequest(
            HttpMethod.Post, invitation.Token, $"caregivers/{created.AssignmentId}/end",
            new { endDate = "2026-09-30", editorVersion = updated.EditorVersion });
        var endResponse = await http.SendAsync(end);
        Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);
        var ended = await endResponse.Content.ReadFromJsonAsync<PublicClientCaregiverResponse>();
        Assert.NotNull(ended);
        Assert.Equal("Ended", ended.Status);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await db.ClientCaregivers.AsNoTracking()
            .SingleAsync(value => value.Id == created.AssignmentId);
        var client = await db.Clients.AsNoTracking().SingleAsync(value => value.Id == persisted.ClientId);
        Assert.Null(persisted.CreatedByUserId);
        Assert.Equal(invitation.Id, persisted.CreatedByGuestInvitationId);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.Equal(4, client.CreationProgressStep);
        Assert.Equal(auditBefore, await factory.CountAllBusinessAuditLogsAsync());
    }

    [Fact]
    public async Task StepFourAssignmentsCannotBeMutatedThroughAnotherInvitation()
    {
        var owner = await InsertInvitationAsync("fresh");
        var other = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepFourAsync(http, owner.Token);
        await AdvanceToStepFourAsync(http, other.Token);
        var caregiverId = await CreateEmployeeAsync(await MainLocationIdAsync(), "Scoped", "Caregiver", "PCA");
        using var create = SaveCaregiverRequest(HttpMethod.Post, owner.Token, null, caregiverId);
        var created = await (await http.SendAsync(create)).Content
            .ReadFromJsonAsync<PublicClientCaregiverResponse>();
        Assert.NotNull(created);

        using var update = SaveCaregiverRequest(
            HttpMethod.Put, other.Token, created.EditorVersion, caregiverId, created.AssignmentId, "Backup");
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(update)).StatusCode);
        using var end = CaregiverRequest(HttpMethod.Post, other.Token,
            $"caregivers/{created.AssignmentId}/end",
            new { endDate = "2026-09-30", editorVersion = created.EditorVersion });
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(end)).StatusCode);
    }

    [Fact]
    public async Task StepFourContinueIsExplicitWorksWithoutAssignmentsAndRejectsPrematureAccess()
    {
        using var http = factory.CreateSecureClient();
        var invitation = await InsertInvitationAsync("fresh");
        await AdvanceToStepFourAsync(http, invitation.Token);

        using var continuation = CaregiverRequest(HttpMethod.Post, invitation.Token, "caregivers/continue");
        var response = await http.SendAsync(continuation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PublicClientIntakeStepContinuationResponse>();
        Assert.NotNull(result);
        Assert.Equal(5, result.NextStep);

        var premature = await InsertInvitationAsync("fresh");
        await AdvanceToStepThreeAsync(http, premature.Token);
        using var prematureList = CaregiverRequest(HttpMethod.Get, premature.Token, "caregivers");
        Assert.Equal(HttpStatusCode.Conflict, (await http.SendAsync(prematureList)).StatusCode);
        using var malformed = CaregiverRequest(HttpMethod.Get, "invalid", "caregivers");
        Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(malformed)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var clientId = await db.ClientGuestInvitations.AsNoTracking()
            .Where(value => value.Id == invitation.Id)
            .Select(value => value.CreatedClientId!.Value)
            .SingleAsync();
        var client = await db.Clients.AsNoTracking().SingleAsync(value => value.Id == clientId);
        Assert.Equal(5, client.CreationProgressStep);
        Assert.Equal(ClientStatus.Pending, client.Status);
    }

    [Fact]
    public async Task StepFiveCreateListAndUpdateAreTokenBoundMinimalAndDoNotAdvanceProgress()
    {
        var invitation = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepFiveAsync(http, invitation.Token);
        var auditBefore = await factory.CountAllBusinessAuditLogsAsync();

        using var create = ContactRecordRequest(HttpMethod.Post, invitation.Token, "contacts", new
        {
            type = "Emergency", name = " Guest Contact ", relationship = " Parent ",
            phone = "(612) 555-0100", email = "guest.contact@example.test",
            address = "100 Main Street", isActive = true,
            clientId = Guid.NewGuid(), companyId = Guid.NewGuid(),
        });
        var createResponse = await http.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PublicClientContactResponse>();
        Assert.NotNull(created);
        Assert.Equal("Guest Contact", created.Name);
        Assert.Equal("Emergency", created.Type);

        using var list = ContactRecordRequest(HttpMethod.Get, invitation.Token, "contacts");
        var listResponse = await http.SendAsync(list);
        var state = await listResponse.Content.ReadFromJsonAsync<PublicClientContactListResponse>();
        Assert.NotNull(state);
        Assert.Equal(5, state.CreationProgressStep);
        Assert.Single(state.Contacts);
        var json = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("clientId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invitationId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("createdBy", json, StringComparison.OrdinalIgnoreCase);

        using var update = ContactRecordRequest(HttpMethod.Put, invitation.Token,
            $"contacts/{created.Id}", new
            {
                type = "Emergency", name = "Updated Contact", relationship = "Guardian",
                phone = "(612) 555-0111", email = "updated@example.test",
                address = "200 Main Street", isActive = false,
            });
        var updateResponse = await http.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Updated Contact",
            (await updateResponse.Content.ReadFromJsonAsync<PublicClientContactResponse>())?.Name);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await db.ClientContacts.AsNoTracking().SingleAsync(value => value.Id == created.Id);
        var client = await db.Clients.AsNoTracking().SingleAsync(value => value.Id == persisted.ClientId);
        Assert.Equal(5, client.CreationProgressStep);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.Equal(invitation.Id, await db.ClientGuestInvitations.AsNoTracking()
            .Where(value => value.CreatedClientId == persisted.ClientId).Select(value => value.Id).SingleAsync());
        Assert.Equal(auditBefore, await factory.CountAllBusinessAuditLogsAsync());
    }

    [Fact]
    public async Task StepFiveRejectsInvalidAndCrossClientUpdatesAndHasNoDeleteEndpoint()
    {
        var owner = await InsertInvitationAsync("fresh");
        var other = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepFiveAsync(http, owner.Token);
        await AdvanceToStepFiveAsync(http, other.Token);
        using var create = ContactRecordRequest(HttpMethod.Post, owner.Token, "contacts", new
        {
            type = "General", name = "Owner Contact", relationship = "Friend",
            phone = "(612) 555-0100", email = "owner@example.test", address = "", isActive = true,
        });
        var created = await (await http.SendAsync(create)).Content.ReadFromJsonAsync<PublicClientContactResponse>();
        Assert.NotNull(created);

        using var invalid = ContactRecordRequest(HttpMethod.Post, owner.Token, "contacts", new
        {
            type = "Other", name = "", relationship = "", phone = "", email = "invalid", isActive = true,
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(invalid)).StatusCode);
        using var crossUpdate = ContactRecordRequest(HttpMethod.Put, other.Token,
            $"contacts/{created.Id}", new
            {
                type = "General", name = "Stolen", relationship = "Friend",
                phone = "(612) 555-0100", email = "owner@example.test", address = "", isActive = true,
            });
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(crossUpdate)).StatusCode);
        using var delete = ContactRecordRequest(HttpMethod.Delete, owner.Token, $"contacts/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(delete)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await db.ClientContacts.AsNoTracking().SingleAsync(value => value.Id == created.Id);
        Assert.Equal("Owner Contact", persisted.Name);
    }

    [Fact]
    public async Task StepFiveContinueIsExplicitOptionalAndAdvancesOnlyToSix()
    {
        var invitation = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepFiveAsync(http, invitation.Token);
        using var request = ContactRecordRequest(HttpMethod.Post, invitation.Token, "contacts/continue");
        var response = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(6, (await response.Content
            .ReadFromJsonAsync<PublicClientIntakeStepContinuationResponse>())?.NextStep);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var clientId = await db.ClientGuestInvitations.AsNoTracking()
            .Where(value => value.Id == invitation.Id).Select(value => value.CreatedClientId!.Value).SingleAsync();
        var client = await db.Clients.AsNoTracking().SingleAsync(value => value.Id == clientId);
        Assert.Equal(6, client.CreationProgressStep);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.False(await db.ClientContacts.AnyAsync(value => value.ClientId == clientId));
    }

    [Fact]
    public async Task StepSixCoordinatorCrudIsTokenBoundMinimalOptionalAndHasNoDelete()
    {
        var owner = await InsertInvitationAsync("fresh");
        var other = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepSixAsync(http, owner.Token);
        await AdvanceToStepSixAsync(http, other.Token);
        var auditBefore = await factory.CountAllBusinessAuditLogsAsync();
        using var create = CareCoordinatorRequest(HttpMethod.Post, owner.Token, "care-coordinators", new
        {
            name = " County Coordinator ", companyName = "Agency", phone = "(612) 555-0100",
            phoneExtension = "12", fax = "(612) 555-0101", email = "case@example.test",
            isActive = true, clientId = Guid.NewGuid(), companyId = Guid.NewGuid(),
        });
        var response = await http.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PublicClientCareCoordinatorResponse>();
        Assert.NotNull(created);
        Assert.Equal("County Coordinator", created.Name);
        using var list = CareCoordinatorRequest(HttpMethod.Get, owner.Token, "care-coordinators");
        var listResponse = await http.SendAsync(list);
        var json = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("clientId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(6, (await listResponse.Content.ReadFromJsonAsync<PublicClientCareCoordinatorListResponse>())?.CreationProgressStep);
        using var cross = CareCoordinatorRequest(HttpMethod.Put, other.Token, $"care-coordinators/{created.Id}", new
        {
            name = "Stolen", companyName = "Agency", phone = "", phoneExtension = "", fax = "",
            email = "case@example.test", isActive = true,
        });
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(cross)).StatusCode);
        using var update = CareCoordinatorRequest(HttpMethod.Put, owner.Token, $"care-coordinators/{created.Id}", new
        {
            name = "Updated Coordinator", companyName = "Updated Agency", phone = "(612) 555-0111",
            phoneExtension = "14", fax = "", email = "updated@example.test", isActive = false,
        });
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(update)).StatusCode);
        using var delete = CareCoordinatorRequest(HttpMethod.Delete, owner.Token, $"care-coordinators/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(delete)).StatusCode);
        using var continuation = CareCoordinatorRequest(HttpMethod.Post, owner.Token, "care-coordinators/continue");
        Assert.Equal(7, (await (await http.SendAsync(continuation)).Content.ReadFromJsonAsync<PublicClientIntakeStepContinuationResponse>())?.NextStep);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await db.ClientContacts.AsNoTracking().SingleAsync(value => value.Id == created.Id);
        var client = await db.Clients.AsNoTracking().SingleAsync(value => value.Id == persisted.ClientId);
        Assert.Equal("Updated Coordinator", persisted.Name);
        Assert.Equal(7, client.CreationProgressStep);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.Equal(auditBefore, await factory.CountAllBusinessAuditLogsAsync());
    }

    [Fact]
    public async Task StepSevenConsultationProvidersAreTokenBoundKindSafeOptionalAndUnaudited()
    {
        var owner = await InsertInvitationAsync("fresh");
        var other = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepSevenAsync(http, owner.Token);
        await AdvanceToStepSevenAsync(http, other.Token);
        var auditBefore = await factory.CountAllBusinessAuditLogsAsync();
        using var create = ClinicalContactRequest(HttpMethod.Post, owner.Token, "consultation-providers", new
        {
            name = " Guest Consultant ", phone = "(612) 555-0100", email = "consult@example.test",
            supportProviderName = "Support Person", supportProviderPhone = "(612) 555-0101",
            supportProviderAddress = "100 Support Street", isActive = true,
            clientId = Guid.NewGuid(), companyId = Guid.NewGuid(), type = "Physician",
        });
        var response = await http.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = Assert.IsType<PublicClientConsultationProviderResponse>(
            await response.Content.ReadFromJsonAsync<PublicClientConsultationProviderResponse>());
        Assert.Equal("Guest Consultant", created.Name);

        Guid physicianId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var clientId = await db.ClientGuestInvitations.AsNoTracking()
                .Where(value => value.Id == owner.Id).Select(value => value.CreatedClientId!.Value).SingleAsync();
            var physician = new ClientContact(Guid.CreateVersion7(), clientId, ClientContactType.Physician,
                "Private Physician", null, null, null, false, false, null, null, null);
            physician.UpdatePhysician("Private Physician", "1234567890", "(612) 555-0199", null,
                null, null, null, null, true);
            db.ClientContacts.Add(physician);
            await db.SaveChangesAsync();
            physicianId = physician.Id;
        }

        using var list = ClinicalContactRequest(HttpMethod.Get, owner.Token, "consultation-providers");
        var listResponse = await http.SendAsync(list);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var json = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("clientId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invitationId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Private Physician", json, StringComparison.Ordinal);
        var listed = await listResponse.Content.ReadFromJsonAsync<PublicClientConsultationProviderListResponse>();
        Assert.NotNull(listed);
        Assert.Single(listed.Providers);
        Assert.Equal(7, listed.CreationProgressStep);

        var updateBody = new { name = "Updated Consultant", phone = "", email = "updated@example.test",
            supportProviderName = "Updated Support", supportProviderPhone = "", supportProviderAddress = "",
            isActive = false, type = "Physician" };
        using var cross = ClinicalContactRequest(HttpMethod.Put, other.Token,
            $"consultation-providers/{created.Id}", updateBody);
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(cross)).StatusCode);
        using var wrongKind = ClinicalContactRequest(HttpMethod.Put, owner.Token,
            $"consultation-providers/{physicianId}", updateBody);
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(wrongKind)).StatusCode);
        using var update = ClinicalContactRequest(HttpMethod.Put, owner.Token,
            $"consultation-providers/{created.Id}", updateBody);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(update)).StatusCode);
        using var delete = ClinicalContactRequest(HttpMethod.Delete, owner.Token,
            $"consultation-providers/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(delete)).StatusCode);
        using var continuation = ClinicalContactRequest(
            HttpMethod.Post, owner.Token, "consultation-providers/continue");
        Assert.Equal(8, (await (await http.SendAsync(continuation)).Content
            .ReadFromJsonAsync<PublicClientIntakeStepContinuationResponse>())?.NextStep);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await verificationDb.ClientContacts.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        var client = await verificationDb.Clients.AsNoTracking().SingleAsync(x => x.Id == persisted.ClientId);
        Assert.Equal(ClientContactType.ConsultationProvider, persisted.Type);
        Assert.Equal("Updated Consultant", persisted.Name);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.Equal(8, client.CreationProgressStep);
        Assert.Equal(auditBefore, await factory.CountAllBusinessAuditLogsAsync());
    }

    [Fact]
    public async Task StepEightPhysiciansAreTokenBoundKindSafeOptionalAndUnaudited()
    {
        var owner = await InsertInvitationAsync("fresh");
        var other = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepEightAsync(http, owner.Token);
        await AdvanceToStepEightAsync(http, other.Token);
        var auditBefore = await factory.CountAllBusinessAuditLogsAsync();
        using var create = ClinicalContactRequest(HttpMethod.Post, owner.Token, "physicians", new
        {
            name = " Guest Physician ", npi = "1234567890", phone = "(612) 555-0100",
            fax = "(612) 555-0101", address = "100 Clinic Street", zipCode = "55401",
            city = "Minneapolis", state = "MN", isActive = true,
            clientId = Guid.NewGuid(), companyId = Guid.NewGuid(), type = "ConsultationProvider",
        });
        var response = await http.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = Assert.IsType<PublicClientPhysicianResponse>(
            await response.Content.ReadFromJsonAsync<PublicClientPhysicianResponse>());
        Assert.Equal("Guest Physician", created.Name);

        Guid providerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var clientId = await db.ClientGuestInvitations.AsNoTracking()
                .Where(x => x.Id == owner.Id).Select(x => x.CreatedClientId!.Value).SingleAsync();
            var provider = new ClientContact(Guid.CreateVersion7(), clientId,
                ClientContactType.ConsultationProvider, "Private Consultant", null, null, null,
                false, false, null, null, null);
            provider.UpdateConsultationProvider("Private Consultant", null, null, null, null, null, true);
            db.ClientContacts.Add(provider);
            await db.SaveChangesAsync();
            providerId = provider.Id;
        }

        using var list = ClinicalContactRequest(HttpMethod.Get, owner.Token, "physicians");
        var listResponse = await http.SendAsync(list);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var json = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("clientId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invitationId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Private Consultant", json, StringComparison.Ordinal);
        var listed = await listResponse.Content.ReadFromJsonAsync<PublicClientPhysicianListResponse>();
        Assert.NotNull(listed);
        Assert.Single(listed.Physicians);
        Assert.Equal(8, listed.CreationProgressStep);

        using var invalidNpi = ClinicalContactRequest(HttpMethod.Post, owner.Token, "physicians", new
        {
            name = "Invalid NPI", npi = "123", isActive = true,
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(invalidNpi)).StatusCode);
        var updateBody = new { name = "Updated Physician", npi = "0987654321", phone = "",
            fax = "", address = "Updated Clinic", zipCode = "55101", city = "Saint Paul",
            state = "MN", isActive = false, type = "ConsultationProvider" };
        using var cross = ClinicalContactRequest(HttpMethod.Put, other.Token,
            $"physicians/{created.Id}", updateBody);
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(cross)).StatusCode);
        using var wrongKind = ClinicalContactRequest(HttpMethod.Put, owner.Token,
            $"physicians/{providerId}", updateBody);
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(wrongKind)).StatusCode);
        using var update = ClinicalContactRequest(HttpMethod.Put, owner.Token,
            $"physicians/{created.Id}", updateBody);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(update)).StatusCode);
        using var delete = ClinicalContactRequest(HttpMethod.Delete, owner.Token,
            $"physicians/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(delete)).StatusCode);
        using var continuation = ClinicalContactRequest(HttpMethod.Post, owner.Token, "physicians/continue");
        Assert.Equal(9, (await (await http.SendAsync(continuation)).Content
            .ReadFromJsonAsync<PublicClientIntakeStepContinuationResponse>())?.NextStep);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await verificationDb.ClientContacts.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        var client = await verificationDb.Clients.AsNoTracking().SingleAsync(x => x.Id == persisted.ClientId);
        Assert.Equal(ClientContactType.Physician, persisted.Type);
        Assert.Equal("Updated Physician", persisted.Name);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.Equal(9, client.CreationProgressStep);
        Assert.Equal(auditBefore, await factory.CountAllBusinessAuditLogsAsync());
    }

    [Fact]
    public async Task StepNineResponsiblePartiesAreTokenBoundOptionalAndUnaudited()
    {
        var owner = await InsertInvitationAsync("fresh");
        var other = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepNineAsync(http, owner.Token);
        await AdvanceToStepNineAsync(http, other.Token);
        var auditBefore = await factory.CountAllBusinessAuditLogsAsync();
        using var create = ClinicalContactRequest(HttpMethod.Post, owner.Token, "responsible-parties", new
        {
            name = " Guest Guardian ", relationship = "Parent", phone = "(612) 555-0100",
            email = "guardian@example.test", address = "100 Family Street",
            livesWithClient = true, hasSpenddown = false, isActive = true,
            clientId = Guid.NewGuid(), companyId = Guid.NewGuid(),
        });
        var response = await http.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = Assert.IsType<PublicClientResponsiblePartyResponse>(
            await response.Content.ReadFromJsonAsync<PublicClientResponsiblePartyResponse>());
        Assert.Equal("Guest Guardian", created.Name);
        Assert.False(created.HasSpenddown);

        using var list = ClinicalContactRequest(HttpMethod.Get, owner.Token, "responsible-parties");
        var listResponse = await http.SendAsync(list);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var json = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("clientId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invitationId", json, StringComparison.OrdinalIgnoreCase);
        var listed = await listResponse.Content.ReadFromJsonAsync<PublicClientResponsiblePartyListResponse>();
        Assert.NotNull(listed);
        Assert.Single(listed.ResponsibleParties);
        Assert.Equal(9, listed.CreationProgressStep);

        using var invalid = ClinicalContactRequest(HttpMethod.Post, owner.Token, "responsible-parties", new
        {
            name = "", relationship = "", phone = "", email = "invalid",
            livesWithClient = (bool?)null, hasSpenddown = (bool?)null, isActive = (bool?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(invalid)).StatusCode);
        var updateBody = new { name = "Updated Guardian", relationship = "Legal guardian",
            phone = "(612) 555-0111", email = "updated@example.test", address = "200 Family Street",
            livesWithClient = false, hasSpenddown = true, isActive = false };
        using var cross = ClinicalContactRequest(HttpMethod.Put, other.Token,
            $"responsible-parties/{created.Id}", updateBody);
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(cross)).StatusCode);
        using var update = ClinicalContactRequest(HttpMethod.Put, owner.Token,
            $"responsible-parties/{created.Id}", updateBody);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(update)).StatusCode);
        using var delete = ClinicalContactRequest(HttpMethod.Delete, owner.Token,
            $"responsible-parties/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(delete)).StatusCode);
        using var continuation = ClinicalContactRequest(
            HttpMethod.Post, owner.Token, "responsible-parties/continue");
        Assert.Equal(10, (await (await http.SendAsync(continuation)).Content
            .ReadFromJsonAsync<PublicClientIntakeStepContinuationResponse>())?.NextStep);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await db.ClientContacts.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        var client = await db.Clients.AsNoTracking().SingleAsync(x => x.Id == persisted.ClientId);
        Assert.Equal(ClientContactType.ResponsibleParty, persisted.Type);
        Assert.Equal("Updated Guardian", persisted.Name);
        Assert.True(persisted.HasSpenddown);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.Equal(10, client.CreationProgressStep);
        Assert.Equal(auditBefore, await factory.CountAllBusinessAuditLogsAsync());
    }

    [Fact]
    public async Task StepTenHospitalizationsAreTokenBoundValidatedAndDoNotCompleteIntake()
    {
        var owner = await InsertInvitationAsync("fresh");
        var other = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepTenAsync(http, owner.Token);
        await AdvanceToStepTenAsync(http, other.Token);
        var auditBefore = await factory.CountAllBusinessAuditLogsAsync();
        var admission = DateTimeOffset.UtcNow.AddDays(2);
        using var create = ClinicalContactRequest(HttpMethod.Post, owner.Token, "hospitalizations", new
        {
            hospitalName = " Guest Medical Center ", admittedAtUtc = admission,
            dischargedAtUtc = (DateTimeOffset?)null, reason = "Observation",
            diagnosis = new string('D', 2000), dischargeInstructions = new string('I', 2000),
            clientId = Guid.NewGuid(), companyId = Guid.NewGuid(),
        });
        var createResponse = await http.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = Assert.IsType<PublicClientHospitalizationResponse>(
            await createResponse.Content.ReadFromJsonAsync<PublicClientHospitalizationResponse>());
        Assert.Equal("Guest Medical Center", created.HospitalName);
        Assert.Null(created.DischargedAtUtc);

        using var list = ClinicalContactRequest(HttpMethod.Get, owner.Token, "hospitalizations");
        var listResponse = await http.SendAsync(list);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var json = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("clientId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invitationId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("createdBy", json, StringComparison.OrdinalIgnoreCase);
        var listed = await listResponse.Content.ReadFromJsonAsync<PublicClientHospitalizationListResponse>();
        Assert.NotNull(listed);
        Assert.Single(listed.Hospitalizations);
        Assert.Equal(10, listed.CreationProgressStep);

        using var invalid = ClinicalContactRequest(HttpMethod.Post, owner.Token, "hospitalizations", new
        {
            hospitalName = "", admittedAtUtc = admission,
            dischargedAtUtc = admission.AddMinutes(-1), reason = "",
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(invalid)).StatusCode);
        var updateBody = new
        {
            hospitalName = "Updated Hospital", admittedAtUtc = admission,
            dischargedAtUtc = admission.AddDays(1), reason = "Updated reason",
            diagnosis = new string('X', 2000), dischargeInstructions = new string('Y', 2000),
            editorVersion = created.EditorVersion,
        };
        using var cross = ClinicalContactRequest(HttpMethod.Put, other.Token,
            $"hospitalizations/{created.Id}", updateBody);
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(cross)).StatusCode);
        using var update = ClinicalContactRequest(HttpMethod.Put, owner.Token,
            $"hospitalizations/{created.Id}", updateBody);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(update)).StatusCode);
        using var delete = ClinicalContactRequest(HttpMethod.Delete, owner.Token,
            $"hospitalizations/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(delete)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persisted = await db.ClientHospitalizations.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        var client = await db.Clients.AsNoTracking().SingleAsync(x => x.Id == persisted.ClientId);
        var invitation = await db.ClientGuestInvitations.AsNoTracking().SingleAsync(x => x.Id == owner.Id);
        Assert.Equal("Updated Hospital", persisted.HospitalName);
        Assert.Null(persisted.CreatedByUserId);
        Assert.Equal(owner.Id, persisted.CreatedByGuestInvitationId);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.Equal(10, client.CreationProgressStep);
        Assert.Null(invitation.CompletedAtUtc);
        Assert.Null(invitation.RevokedAtUtc);
        Assert.Equal(auditBefore, await factory.CountAllBusinessAuditLogsAsync());
    }

    [Fact]
    public async Task FinalCompletionIsAtomicIdempotentAndLocksGuestMutations()
    {
        var owner = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepTenAsync(http, owner.Token);
        var auditBefore = await factory.CountAllBusinessAuditLogsAsync();

        DateTimeOffset createdAt;
        DateTimeOffset expiresAt;
        DateTimeOffset? openedAt;
        DateTimeOffset? clientCreatedAt;
        Guid clientId;
        await using (var beforeScope = factory.Services.CreateAsyncScope())
        {
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var invitation = await beforeDb.ClientGuestInvitations.AsNoTracking()
                .SingleAsync(value => value.Id == owner.Id);
            clientId = Assert.IsType<Guid>(invitation.CreatedClientId);
            createdAt = invitation.CreatedAtUtc;
            expiresAt = invitation.ExpiresAtUtc;
            openedAt = invitation.OpenedAtUtc;
            clientCreatedAt = invitation.ClientCreatedAtUtc;
        }

        using var complete = ClinicalContactRequest(HttpMethod.Post, owner.Token, "complete");
        var response = await http.SendAsync(complete);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseJson = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("clientId", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId", responseJson, StringComparison.OrdinalIgnoreCase);
        var completion = Assert.IsType<PublicClientIntakeCompletionResponse>(
            await response.Content.ReadFromJsonAsync<PublicClientIntakeCompletionResponse>());
        Assert.Equal("Completed", completion.State);

        using var retry = ClinicalContactRequest(HttpMethod.Post, owner.Token, "complete");
        var retryResponse = await http.SendAsync(retry);
        Assert.Equal(HttpStatusCode.BadRequest, retryResponse.StatusCode);

        using var blockedMutation = ClinicalContactRequest(HttpMethod.Post, owner.Token,
            "hospitalizations", new
            {
                hospitalName = "Blocked", admittedAtUtc = DateTimeOffset.UtcNow,
                dischargedAtUtc = (DateTimeOffset?)null, reason = "Must not persist",
            });
        var blockedResponse = await http.SendAsync(blockedMutation);
        Assert.Equal(HttpStatusCode.BadRequest, blockedResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var persistedInvitation = await db.ClientGuestInvitations.AsNoTracking()
            .SingleAsync(value => value.Id == owner.Id);
        var client = await db.Clients.AsNoTracking().SingleAsync(value => value.Id == clientId);
        Assert.Equal(ClientStatus.Active, client.Status);
        Assert.Equal(10, client.CreationProgressStep);
        Assert.Equal(completion.CompletedAtUtc, persistedInvitation.CompletedAtUtc);
        Assert.Null(persistedInvitation.RevokedAtUtc);
        Assert.Equal(clientId, persistedInvitation.CreatedClientId);
        Assert.Equal(createdAt, persistedInvitation.CreatedAtUtc);
        Assert.Equal(expiresAt, persistedInvitation.ExpiresAtUtc);
        Assert.Equal(openedAt, persistedInvitation.OpenedAtUtc);
        Assert.Equal(clientCreatedAt, persistedInvitation.ClientCreatedAtUtc);
        Assert.DoesNotContain(await db.ClientHospitalizations.AsNoTracking().ToListAsync(),
            value => value.HospitalName == "Blocked");
        Assert.Equal(auditBefore, await factory.CountAllBusinessAuditLogsAsync());
    }

    [Theory]
    [InlineData("expired", HttpStatusCode.Gone, "Expired")]
    [InlineData("revoked", HttpStatusCode.Conflict, "Unavailable")]
    [InlineData("fresh", HttpStatusCode.Conflict, "Unavailable")]
    public async Task FinalCompletionRejectsInvalidLifecycleOrUnboundInvitation(
        string invitationState, HttpStatusCode expectedStatus, string expectedState)
    {
        var invitation = await InsertInvitationAsync(invitationState);
        using var http = factory.CreateSecureClient();
        using var request = ClinicalContactRequest(HttpMethod.Post, invitation.Token, "complete");
        var response = await http.SendAsync(request);
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Contains(expectedState, await response.Content.ReadAsStringAsync());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Null((await db.ClientGuestInvitations.AsNoTracking()
            .SingleAsync(value => value.Id == invitation.Id)).CompletedAtUtc);
    }

    [Fact]
    public async Task FinalCompletionRequiresStepTenAndPendingClient()
    {
        var incomplete = await InsertInvitationAsync("fresh");
        using var http = factory.CreateSecureClient();
        await AdvanceToStepNineAsync(http, incomplete.Token);
        using var premature = ClinicalContactRequest(HttpMethod.Post, incomplete.Token, "complete");
        Assert.Equal(HttpStatusCode.Conflict, (await http.SendAsync(premature)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var invitation = await db.ClientGuestInvitations
            .SingleAsync(value => value.Id == incomplete.Id);
        var client = await db.Clients.SingleAsync(value => value.Id == invitation.CreatedClientId);
        Assert.Equal(ClientStatus.Pending, client.Status);
        Assert.Equal(9, client.CreationProgressStep);
        Assert.Null(invitation.CompletedAtUtc);

        client.ChangeStatus(ClientStatus.Active, null, null, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        using var inconsistent = ClinicalContactRequest(HttpMethod.Post, incomplete.Token, "complete");
        Assert.Equal(HttpStatusCode.Conflict, (await http.SendAsync(inconsistent)).StatusCode);
        await db.Entry(invitation).ReloadAsync();
        Assert.Null(invitation.CompletedAtUtc);
    }

    private async Task<PublicClientIntakeStepContinuationResponse> AdvanceToStepThreeAsync(
        HttpClient http,
        string token)
    {
        var locationId = await MainLocationIdAsync();
        var pmi = Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        using var stepOne = CreateBasicDetailsRequest(token, pmi, locationId);
        var stepOneResponse = await http.SendAsync(stepOne);
        var first = await stepOneResponse.Content.ReadFromJsonAsync<PublicClientIntakeContinuationResponse>();
        Assert.NotNull(first);
        using var stepTwo = AdditionalInformationRequest(HttpMethod.Put, token, first.EditorVersion, "Setup");
        var stepTwoResponse = await http.SendAsync(stepTwo);
        var second = await stepTwoResponse.Content
            .ReadFromJsonAsync<PublicClientIntakeStepContinuationResponse>();
        return Assert.IsType<PublicClientIntakeStepContinuationResponse>(second);
    }

    private async Task<PublicClientIntakeStepContinuationResponse> AdvanceToStepFourAsync(
        HttpClient http,
        string token)
    {
        var stepThree = await AdvanceToStepThreeAsync(http, token);
        using var contact = ContactInformationRequest(HttpMethod.Put, token, stepThree.EditorVersion);
        var response = await http.SendAsync(contact);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<PublicClientIntakeStepContinuationResponse>(
            await response.Content.ReadFromJsonAsync<PublicClientIntakeStepContinuationResponse>());
    }

    private async Task AdvanceToStepFiveAsync(HttpClient http, string token)
    {
        await AdvanceToStepFourAsync(http, token);
        using var request = CaregiverRequest(HttpMethod.Post, token, "caregivers/continue");
        var response = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task AdvanceToStepSixAsync(HttpClient http, string token)
    {
        await AdvanceToStepFiveAsync(http, token);
        using var request = ContactRecordRequest(HttpMethod.Post, token, "contacts/continue");
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(request)).StatusCode);
    }

    private async Task AdvanceToStepSevenAsync(HttpClient http, string token)
    {
        await AdvanceToStepSixAsync(http, token);
        using var request = CareCoordinatorRequest(HttpMethod.Post, token, "care-coordinators/continue");
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(request)).StatusCode);
    }

    private async Task AdvanceToStepEightAsync(HttpClient http, string token)
    {
        await AdvanceToStepSevenAsync(http, token);
        using var request = ClinicalContactRequest(
            HttpMethod.Post, token, "consultation-providers/continue");
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(request)).StatusCode);
    }

    private async Task AdvanceToStepNineAsync(HttpClient http, string token)
    {
        await AdvanceToStepEightAsync(http, token);
        using var request = ClinicalContactRequest(HttpMethod.Post, token, "physicians/continue");
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(request)).StatusCode);
    }

    private async Task AdvanceToStepTenAsync(HttpClient http, string token)
    {
        await AdvanceToStepNineAsync(http, token);
        using var request = ClinicalContactRequest(
            HttpMethod.Post, token, "responsible-parties/continue");
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(request)).StatusCode);
    }

    private static HttpRequestMessage ClinicalContactRequest(
        HttpMethod method, string token, string path, object? content = null) =>
        CaregiverRequest(method, token, path, content);

    private static HttpRequestMessage CareCoordinatorRequest(
        HttpMethod method, string token, string path, object? content = null) =>
        CaregiverRequest(method, token, path, content);

    private static HttpRequestMessage ContactRecordRequest(
        HttpMethod method, string token, string path, object? content = null) =>
        CaregiverRequest(method, token, path, content);

    private async Task<Guid> CreateEmployeeAsync(
        Guid locationId,
        string firstName,
        string lastName,
        string jobTitle,
        Guid? companyId = null,
        bool isActive = true,
        bool isEmployee = true)
    {
        var user = await factory.CreateUserAsync($"guest-caregiver-{Guid.NewGuid():N}@example.test", Password);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        var employee = await db.Users.SingleAsync(value => value.Id == user.Id);
        employee.CompanyId = companyId ?? SqlServerApiFactory.DefaultCompanyId;
        employee.FirstName = firstName;
        employee.LastName = lastName;
        employee.PhoneNumber = "(612) 555-0199";
        employee.IsActive = isActive;
        employee.IsEmployee = isEmployee;
        employee.JobTitle = jobTitle;
        db.UserLocations.Add(new ApplicationUserLocation { UserId = employee.Id, LocationId = locationId });
        await db.SaveChangesAsync();
        return employee.Id;
    }

    private async Task<Guid> CreateLocationAsync(Guid companyId)
    {
        var id = Guid.CreateVersion7();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.CompanyLocations.Add(CompanyLocation.Create(
            id, companyId, $"Guest {id:N}", false, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<(Guid CompanyId, Guid LocationId)> CreateCompanyAndLocationAsync()
    {
        var companyId = Guid.CreateVersion7();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.Companies.Add(Company.Create(
            companyId, $"Guest {companyId:N}", $"GL-{companyId:N}", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return (companyId, await CreateLocationAsync(companyId));
    }

    private static HttpRequestMessage SaveCaregiverRequest(
        HttpMethod method,
        string token,
        string? editorVersion,
        Guid caregiverId,
        Guid? assignmentId = null,
        string assignmentType = "Primary") =>
        CaregiverRequest(method, token,
            assignmentId.HasValue ? $"caregivers/{assignmentId}" : "caregivers",
            new
            {
                caregiverUserId = caregiverId,
                assignmentType,
                clientPreference = true,
                assignedServices = "PCA",
                startDate = "2026-09-01",
                endDate = (string?)null,
                relationship = "Daughter",
                livesWithClient = true,
                notes = "Guest-selected caregiver",
                editorVersion,
            });

    private static HttpRequestMessage CaregiverRequest(
        HttpMethod method,
        string token,
        string path,
        object? content = null)
    {
        var request = new HttpRequestMessage(method, $"/api/public/client-intake/{path}");
        AddSession(request, token);
        if (content is not null) request.Content = JsonContent.Create(content);
        return request;
    }

    private static HttpRequestMessage CreateBasicDetailsRequest(string token, string pmi, Guid locationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/public/client-intake/basic-details");
        AddSession(request, token);
        request.Content = JsonContent.Create(new
        {
            identity = new
            {
                firstName = "Guest",
                middleName = "Phase",
                lastName = "Five",
                preferredName = "Guest",
                dateOfBirth = "1990-01-01",
                gender = "Female",
                pmiNumber = pmi,
                ssn = "123-45-6789",
            },
            careSetup = new
            {
                intakeDate = "2026-09-30",
                socDate = "2026-10-01",
                referralSource = "G-Link",
                locationId,
                supervisorUserId = (Guid?)null,
                evvAuthorizedHoursPerWeek = 12.5m,
            },
            insuranceProvider = "Guest Insurance",
            services = new[]
            {
                new { code = "PCA", name = "PCA" },
                new { code = "CFSS", name = "CFSS" },
            },
        });
        return request;
    }

    private static HttpRequestMessage AdditionalInformationRequest(
        HttpMethod method,
        string token,
        string? editorVersion = null,
        string notes = "")
    {
        var request = new HttpRequestMessage(method, "/api/public/client-intake/additional-information");
        AddSession(request, token);
        if (method == HttpMethod.Put)
        {
            request.Content = JsonContent.Create(new
            {
                additionalInformation = new
                {
                    programType = "PcaChoice",
                    referralDetails = "referringClient=Yes\nreferringClientName=Referral Person",
                    homeCareRating = "Standard",
                    medicalEquipment = "Wheelchair",
                    pcaComplexity = "Enhanced",
                    demographics = "maritalStatus=Single\nethnicity=Guest\nrace=Guest",
                    smokes = false,
                    isVeteran = true,
                    hasPets = true,
                    livingArrangement = "type=With family\ndetails=Supportive home",
                    preferredClinic = "Guest Clinic",
                    preferredHospital = "Guest Hospital",
                    preferredAmbulance = "Guest Ambulance",
                    caregiverPreference = "Language match",
                    nutritionalRequirements = "Low salt",
                    diet = "Low Salt, Regular",
                    mealSchedule = "breakfast=08:00\nlunch=12:00\ndinner=18:00",
                    notes,
                    additionalInformation = "Guest supplied information",
                },
                editorVersion,
            });
        }

        return request;
    }

    private static HttpRequestMessage ContactInformationRequest(
        HttpMethod method,
        string token,
        string? editorVersion = null,
        string primaryPhone = "(612) 555-0111",
        IReadOnlyList<(string Code, string Name, bool Primary)>? languages = null)
    {
        var request = new HttpRequestMessage(method, "/api/public/client-intake/contact-information");
        AddSession(request, token);
        if (method == HttpMethod.Put)
        {
            request.Content = JsonContent.Create(new
            {
                contactInformation = new
                {
                    email = "guest.contact@example.test",
                    primaryPhone,
                    secondaryPhone = "(612) 555-0112",
                    addressLine1 = "100 Guest Street",
                    addressLine2 = "Apt 2",
                    city = "Minneapolis",
                    state = "MN",
                    postalCode = "55401",
                    county = "Hennepin",
                    country = "United States",
                    preferredLanguages = (languages ??
                        [("en", "English", true), ("es", "Spanish", false)])
                        .Select(value => new
                        {
                            languageCode = value.Code,
                            languageName = value.Name,
                            isPrimary = value.Primary,
                        }),
                },
                editorVersion,
            });
        }

        return request;
    }

    private static HttpRequestMessage Request(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/client-intake/invitation");
        AddSession(request, token);
        return request;
    }

    private static void AddSession(HttpRequestMessage request, string token)
    {
        request.Headers.Add(HeaderName, token);
        if (CsrfTokens.TryGetValue(token, out var csrf))
        {
            request.Headers.Add("Cookie",
                $"{PublicClientIntakeController.SessionCookieName}={token}; {PublicClientIntakeController.CsrfCookieName}={csrf}");
            request.Headers.Add(PublicClientIntakeController.CsrfHeaderName, csrf);
        }
        else
        {
            request.Headers.Add("Cookie", $"{PublicClientIntakeController.SessionCookieName}={token}");
        }
    }

    private static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
