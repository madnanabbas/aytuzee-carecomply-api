using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using CareComply.Domain.Companies;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class ClientContactWorkflowCharacterizationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string Password = "correct horse battery staple";

    public static TheoryData<string> ListRoutes => new()
    {
        "/api/clients/{0}/care-coordinators",
        "/api/clients/{0}/contacts",
        "/api/clients/{0}/responsible-parties",
        "/api/clients/{0}/consultation-providers",
        "/api/clients/{0}/physicians",
    };

    public static TheoryData<string, string, string> PermissionCases => new()
    {
        { "/api/clients/{0}/care-coordinators", "GET", Permissions.ClientCareCoordinator.View },
        { "/api/clients/{0}/care-coordinators", "POST", Permissions.ClientCareCoordinator.Create },
        { "/api/clients/{0}/care-coordinators/{1}", "PUT", Permissions.ClientCareCoordinator.Edit },
        { "/api/clients/{0}/contacts", "GET", Permissions.ClientContact.View },
        { "/api/clients/{0}/contacts", "POST", Permissions.ClientContact.Create },
        { "/api/clients/{0}/contacts/{1}", "PUT", Permissions.ClientContact.Edit },
        { "/api/clients/{0}/responsible-parties", "GET", Permissions.ClientResponsibleParty.View },
        { "/api/clients/{0}/responsible-parties", "POST", Permissions.ClientResponsibleParty.Create },
        { "/api/clients/{0}/responsible-parties/{1}", "PUT", Permissions.ClientResponsibleParty.Edit },
        { "/api/clients/{0}/consultation-providers", "GET", Permissions.ClientConsultationProvider.View },
        { "/api/clients/{0}/consultation-providers", "POST", Permissions.ClientConsultationProvider.Create },
        { "/api/clients/{0}/consultation-providers/{1}", "PUT", Permissions.ClientConsultationProvider.Edit },
        { "/api/clients/{0}/physicians", "GET", Permissions.ClientPhysician.View },
        { "/api/clients/{0}/physicians", "POST", Permissions.ClientPhysician.Create },
        { "/api/clients/{0}/physicians/{1}", "PUT", Permissions.ClientPhysician.Edit },
    };

    public static TheoryData<string, string, string, string> AuditCases => new()
    {
        { "/api/clients/{0}/care-coordinators", "ClientCareCoordinator.Created", "ClientCareCoordinator.Updated", "ClientCareCoordinator" },
        { "/api/clients/{0}/contacts", "ClientContact.Created", "ClientContact.Updated", "ClientContact" },
        { "/api/clients/{0}/responsible-parties", "ClientResponsibleParty.Created", "ClientResponsibleParty.Updated", "ClientResponsibleParty" },
        { "/api/clients/{0}/consultation-providers", "ClientConsultationProvider.Created", "ClientConsultationProvider.Updated", "ClientConsultationProvider" },
        { "/api/clients/{0}/physicians", "ClientPhysician.Created", "ClientPhysician.Updated", "ClientPhysician" },
    };

    [Theory]
    [MemberData(nameof(ListRoutes))]
    public async Task UnauthenticatedListRequestsAreRejected(string routeTemplate)
    {
        using var client = factory.CreateSecureClient();
        var response = await client.GetAsync(
            string.Format(CultureInfo.InvariantCulture, routeTemplate, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(PermissionCases))]
    public async Task EveryEndpointEnforcesItsDeclaredPermission(
        string routeTemplate,
        string method,
        string permission)
    {
        var actor = await CreateActorAsync();
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var route = string.Format(
            CultureInfo.InvariantCulture,
            routeTemplate,
            clientId,
            Guid.NewGuid());

        using var response = await SendAsync(client, method, route, ValidPayload(route));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await factory.GrantPermissionAsync(actor.Id, permission);
        using var authorizedClient = factory.CreateSecureClient();
        await SignInAsync(authorizedClient, actor.Email!);
        using var authorized = await SendAsync(
            authorizedClient,
            method,
            route,
            ValidPayload(route));
        Assert.NotEqual(HttpStatusCode.Forbidden, authorized.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ListRoutes))]
    public async Task MissingClientReturnsNotFound(string routeTemplate)
    {
        var permission = ViewPermission(routeTemplate);
        var actor = await CreateActorAsync(permission);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        var response = await client.GetAsync(
            string.Format(CultureInfo.InvariantCulture, routeTemplate, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MissingClientMutationsReturnNotFound()
    {
        var actor = await CreateActorAsync(AllContactPermissions);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        foreach (var routeTemplate in ContactRouteTemplates)
        {
            var route = string.Format(CultureInfo.InvariantCulture, routeTemplate, Guid.NewGuid());
            var create = await client.PostAsJsonAsync(route, ValidPayload(route));
            var update = await client.PutAsJsonAsync($"{route}/{Guid.NewGuid()}", ValidPayload(route));

            Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        }
    }

    [Theory]
    [MemberData(nameof(ListRoutes))]
    public async Task CrossCompanyClientIsNotExposed(string routeTemplate)
    {
        var actor = await CreateActorAsync(ViewPermission(routeTemplate));
        var actorLocation = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, actorLocation);
        var (companyId, locationId) = await CreateCompanyAsync();
        var clientId = await CreateClientAsync(companyId, locationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        var response = await client.GetAsync(
            string.Format(CultureInfo.InvariantCulture, routeTemplate, clientId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ListRoutes))]
    public async Task CrossLocationClientIsNotExposedAndTenantListIsFiltered(string routeTemplate)
    {
        var actor = await CreateActorAsync(ViewPermission(routeTemplate));
        var accessibleLocation = await MainLocationIdAsync();
        var inaccessibleLocation = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        await GiveLocationAccessAsync(actor.Id, accessibleLocation);
        var accessibleClient = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            accessibleLocation);
        var inaccessibleClient = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            inaccessibleLocation);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        var accessible = await client.GetAsync(
            string.Format(CultureInfo.InvariantCulture, routeTemplate, accessibleClient));
        var inaccessible = await client.GetAsync(
            string.Format(CultureInfo.InvariantCulture, routeTemplate, inaccessibleClient));

        Assert.Equal(HttpStatusCode.OK, accessible.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inaccessible.StatusCode);
    }

    [Fact]
    public async Task CrossCompanyAndCrossLocationMutationsAreRejected()
    {
        var actor = await CreateActorAsync(AllContactPermissions);
        var accessibleLocationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, accessibleLocationId);
        var inaccessibleLocationId = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        var inaccessibleLocationClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            inaccessibleLocationId);
        var otherTenant = await CreateCompanyAsync();
        var otherTenantClientId = await CreateClientAsync(otherTenant.CompanyId, otherTenant.LocationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        foreach (var routeTemplate in ContactRouteTemplates)
        {
            foreach (var clientId in new[] { inaccessibleLocationClientId, otherTenantClientId })
            {
                var route = string.Format(CultureInfo.InvariantCulture, routeTemplate, clientId);
                var create = await client.PostAsJsonAsync(route, ValidPayload(route));
                var update = await client.PutAsJsonAsync($"{route}/{Guid.NewGuid()}", ValidPayload(route));

                Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
            }
        }
    }

    [Fact]
    public async Task ValidationErrorsPreserveCurrentKeysAndMessages()
    {
        var actor = await CreateActorAsync(
            Permissions.ClientCareCoordinator.Create,
            Permissions.ClientContact.Create,
            Permissions.ClientResponsibleParty.Create,
            Permissions.ClientConsultationProvider.Create,
            Permissions.ClientPhysician.Create);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        await AssertValidationAsync(
            await client.PostAsJsonAsync($"/api/clients/{clientId}/care-coordinators", new { }),
            ("Name", "Name is required."),
            ("IsActive", "Select a status."));
        await AssertValidationAsync(
            await client.PostAsJsonAsync($"/api/clients/{clientId}/contacts", new { type = "Other" }),
            ("Type", "Select General or Emergency."),
            ("Name", "Name is required."),
            ("Relationship", "Relationship is required."),
            ("Phone", "Phone is required."),
            ("IsActive", "Select a status."));
        await AssertValidationAsync(
            await client.PostAsJsonAsync($"/api/clients/{clientId}/responsible-parties", new { }),
            ("Name", "Name is required."),
            ("Relationship", "Relationship is required."),
            ("Phone", "Phone is required."),
            ("LivesWithClient", "Select whether this person lives with the client."),
            ("HasSpenddown", "Select whether this person has spenddown responsibility."),
            ("IsActive", "Select a status."));
        await AssertValidationAsync(
            await client.PostAsJsonAsync($"/api/clients/{clientId}/consultation-providers", new { email = "invalid" }),
            ("Name", "Name is required."),
            ("Email", "Enter a valid email address."),
            ("IsActive", "Select a status."));
        await AssertValidationAsync(
            await client.PostAsJsonAsync($"/api/clients/{clientId}/physicians", new { npi = "abc" }),
            ("Name", "Name is required."),
            ("Npi", "NPI must contain exactly 10 digits."),
            ("IsActive", "Select a status."));
    }

    [Fact]
    public async Task CreateUpdateAndListContractsRoundTripForAllWorkflows()
    {
        var actor = await CreateActorAsync(AllContactPermissions);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        await AssertRoundTripAsync(
            client,
            $"/api/clients/{clientId}/care-coordinators",
            new { name = "Care Coordinator", companyName = "County", phone = "6125550100", phoneExtension = "42", fax = "6125550101", email = "coordinator@example.test", isActive = true },
            new { name = "Updated Coordinator", companyName = "County", phone = "6125550100", phoneExtension = "42", fax = "6125550101", email = "coordinator@example.test", isActive = false },
            "name",
            "Updated Coordinator");
        await AssertRoundTripAsync(
            client,
            $"/api/clients/{clientId}/contacts",
            new { type = "General", name = "Family Contact", relationship = "Sibling", phone = "6125550200", email = "contact@example.test", address = "10 Main St", isActive = true },
            new { type = "General", name = "Updated Contact", relationship = "Sibling", phone = "6125550200", email = "contact@example.test", address = "10 Main St", isActive = false },
            "name",
            "Updated Contact");
        await AssertRoundTripAsync(
            client,
            $"/api/clients/{clientId}/responsible-parties",
            new { name = "Responsible Party", relationship = "Parent", phone = "6125550300", email = "party@example.test", address = "20 Main St", livesWithClient = true, hasSpenddown = false, isActive = true },
            new { name = "Updated Party", relationship = "Parent", phone = "6125550300", email = "party@example.test", address = "20 Main St", livesWithClient = false, hasSpenddown = true, isActive = false },
            "name",
            "Updated Party");
        await AssertRoundTripAsync(
            client,
            $"/api/clients/{clientId}/consultation-providers",
            new { name = "Consultation Provider", phone = "6125550400", email = "provider@example.test", supportProviderName = "Support", supportProviderPhone = "6125550401", supportProviderAddress = "30 Main St", isActive = true },
            new { name = "Updated Provider", phone = "6125550400", email = "provider@example.test", supportProviderName = "Support", supportProviderPhone = "6125550401", supportProviderAddress = "30 Main St", isActive = false },
            "name",
            "Updated Provider");
        await AssertRoundTripAsync(
            client,
            $"/api/clients/{clientId}/physicians",
            new { name = "Physician", npi = "1234567890", phone = "6125550500", fax = "6125550501", address = "40 Main St", postalCode = "55101", city = "Saint Paul", state = "MN", isActive = true },
            new { name = "Updated Physician", npi = "1234567890", phone = "6125550500", fax = "6125550501", address = "40 Main St", postalCode = "55101", city = "Saint Paul", state = "MN", isActive = false },
            "name",
            "Updated Physician");
    }

    [Fact]
    public async Task ContactCreateBindsToRouteClientAndCrossClientUpdateIsRejected()
    {
        var actor = await CreateActorAsync(AllContactPermissions);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var firstClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            locationId);
        var secondClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            locationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = new
        {
            type = "General",
            name = "Bound Contact",
            relationship = "Sibling",
            phone = "6125550200",
            email = "contact@example.test",
            address = "10 Main St",
            isActive = true,
        };

        var created = await client.PostAsJsonAsync(
            $"/api/clients/{secondClientId}/contacts",
            request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var contactId = createdJson.RootElement.GetProperty("id").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            Assert.True(await db.ClientContacts.AsNoTracking().AnyAsync(contact =>
                contact.Id == contactId && contact.ClientId == secondClientId));
        }

        var rejected = await client.PutAsJsonAsync(
            $"/api/clients/{firstClientId}/contacts/{contactId}",
            new
            {
                type = "General",
                name = "Cross-client mutation",
                relationship = "Sibling",
                phone = "6125550200",
                email = "contact@example.test",
                address = "10 Main St",
                isActive = false,
            });

        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<CareComplyDbContext>();
        var persisted = await verificationDb.ClientContacts.AsNoTracking()
            .SingleAsync(contact => contact.Id == contactId);
        Assert.Equal(secondClientId, persisted.ClientId);
        Assert.Equal("Bound Contact", persisted.Name);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task CareCoordinatorCreateBindsToRouteClientAndCrossClientUpdateIsRejected()
    {
        var actor = await CreateActorAsync(AllContactPermissions);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var firstClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            locationId);
        var secondClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            locationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = new
        {
            name = "Bound Coordinator",
            companyName = "County Services",
            phone = "6125550200",
            phoneExtension = "42",
            fax = "6125550201",
            email = "coordinator@example.test",
            isActive = true,
        };

        var created = await client.PostAsJsonAsync(
            $"/api/clients/{secondClientId}/care-coordinators",
            request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var coordinatorId = createdJson.RootElement.GetProperty("id").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            Assert.True(await db.ClientContacts.AsNoTracking().AnyAsync(coordinator =>
                coordinator.Id == coordinatorId
                && coordinator.ClientId == secondClientId
                && coordinator.Type == ClientContactType.CareCoordinator));
        }

        var rejected = await client.PutAsJsonAsync(
            $"/api/clients/{firstClientId}/care-coordinators/{coordinatorId}",
            new
            {
                name = "Cross-client mutation",
                companyName = "County Services",
                phone = "6125550200",
                phoneExtension = "42",
                fax = "6125550201",
                email = "coordinator@example.test",
                isActive = false,
            });

        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<CareComplyDbContext>();
        var persisted = await verificationDb.ClientContacts.AsNoTracking()
            .SingleAsync(coordinator => coordinator.Id == coordinatorId);
        Assert.Equal(secondClientId, persisted.ClientId);
        Assert.Equal("Bound Coordinator", persisted.Name);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task ConsultationProviderCreateBindsToRouteClientAndCrossClientUpdateIsRejected()
    {
        var actor = await CreateActorAsync(AllContactPermissions);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var firstClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            locationId);
        var secondClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            locationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = new
        {
            name = "Bound Provider",
            phone = "6125550200",
            email = "provider@example.test",
            supportProviderName = "Support Person",
            supportProviderPhone = "6125550201",
            supportProviderAddress = "10 Main St",
            isActive = true,
        };

        var created = await client.PostAsJsonAsync(
            $"/api/clients/{secondClientId}/consultation-providers",
            request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var providerId = createdJson.RootElement.GetProperty("id").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            Assert.True(await db.ClientContacts.AsNoTracking().AnyAsync(provider =>
                provider.Id == providerId
                && provider.ClientId == secondClientId
                && provider.Type == ClientContactType.ConsultationProvider));
        }

        var rejected = await client.PutAsJsonAsync(
            $"/api/clients/{firstClientId}/consultation-providers/{providerId}",
            new
            {
                name = "Cross-client mutation",
                phone = "6125550200",
                email = "provider@example.test",
                supportProviderName = "Support Person",
                supportProviderPhone = "6125550201",
                supportProviderAddress = "10 Main St",
                isActive = false,
            });

        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<CareComplyDbContext>();
        var persisted = await verificationDb.ClientContacts.AsNoTracking()
            .SingleAsync(provider => provider.Id == providerId);
        Assert.Equal(secondClientId, persisted.ClientId);
        Assert.Equal("Bound Provider", persisted.Name);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task PhysicianCreateBindsToRouteClientAndCrossClientUpdateIsRejected()
    {
        var actor = await CreateActorAsync(AllContactPermissions);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var firstClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            locationId);
        var secondClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            locationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = new
        {
            name = "Bound Physician",
            npi = "1234567890",
            phone = "6125550200",
            fax = "6125550201",
            address = "10 Main St",
            postalCode = "55101",
            city = "Saint Paul",
            state = "MN",
            isActive = true,
        };

        var created = await client.PostAsJsonAsync(
            $"/api/clients/{secondClientId}/physicians",
            request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var physicianId = createdJson.RootElement.GetProperty("id").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            Assert.True(await db.ClientContacts.AsNoTracking().AnyAsync(physician =>
                physician.Id == physicianId
                && physician.ClientId == secondClientId
                && physician.Type == ClientContactType.Physician));
        }

        var rejected = await client.PutAsJsonAsync(
            $"/api/clients/{firstClientId}/physicians/{physicianId}",
            new
            {
                name = "Cross-client mutation",
                npi = "1234567890",
                phone = "6125550200",
                fax = "6125550201",
                address = "10 Main St",
                postalCode = "55101",
                city = "Saint Paul",
                state = "MN",
                isActive = false,
            });

        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<CareComplyDbContext>();
        var persisted = await verificationDb.ClientContacts.AsNoTracking()
            .SingleAsync(physician => physician.Id == physicianId);
        Assert.Equal(secondClientId, persisted.ClientId);
        Assert.Equal("Bound Physician", persisted.Name);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task ResponsiblePartyCreateBindsToRouteClientAndCrossClientUpdateIsRejected()
    {
        var actor = await CreateActorAsync(AllContactPermissions);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var firstClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            locationId);
        var secondClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            locationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var request = new
        {
            name = "Bound Responsible Party",
            relationship = "Guardian",
            phone = "6125550200",
            email = "party@example.test",
            address = "10 Main St",
            livesWithClient = true,
            hasSpenddown = false,
            isActive = true,
        };

        var created = await client.PostAsJsonAsync(
            $"/api/clients/{secondClientId}/responsible-parties",
            request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var partyId = createdJson.RootElement.GetProperty("id").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            Assert.True(await db.ClientContacts.AsNoTracking().AnyAsync(party =>
                party.Id == partyId
                && party.ClientId == secondClientId
                && party.Type == ClientContactType.ResponsibleParty));
        }

        var rejected = await client.PutAsJsonAsync(
            $"/api/clients/{firstClientId}/responsible-parties/{partyId}",
            new
            {
                name = "Cross-client mutation",
                relationship = "Guardian",
                phone = "6125550200",
                email = "party@example.test",
                address = "10 Main St",
                livesWithClient = false,
                hasSpenddown = true,
                isActive = false,
            });

        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<CareComplyDbContext>();
        var persisted = await verificationDb.ClientContacts.AsNoTracking()
            .SingleAsync(party => party.Id == partyId);
        Assert.Equal(secondClientId, persisted.ClientId);
        Assert.Equal("Bound Responsible Party", persisted.Name);
        Assert.True(persisted.LivesWithClient);
        Assert.False(persisted.HasSpenddown);
        Assert.True(persisted.IsActive);
    }

    [Theory]
    [MemberData(nameof(AuditCases))]
    public async Task SuccessfulCreateAndUpdateEmitSafeBusinessAudit(
        string routeTemplate,
        string createAction,
        string updateAction,
        string resourceType)
    {
        var actor = await CreateActorAsync(AllContactPermissions);
        var locationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, locationId);
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        var route = string.Format(CultureInfo.InvariantCulture, routeTemplate, clientId);

        var created = await client.PostAsJsonAsync(route, ValidPayload(route));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var resourceId = createdJson.RootElement.GetProperty("id").GetGuid();
        var createAudit = await ReadAuditAsync(actor.Id, createAction, resourceId);
        AssertSafeSuccessfulAudit(createAudit, resourceType, clientId, resourceId);

        var updated = await client.PutAsJsonAsync($"{route}/{resourceId}", ValidPayload(route));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updateAudit = await ReadAuditAsync(actor.Id, updateAction, resourceId);
        AssertSafeSuccessfulAudit(updateAudit, resourceType, clientId, resourceId);
    }

    [Theory]
    [MemberData(nameof(AuditCases))]
    public async Task PreHandlerFailuresEmitNoMutationAudit(
        string routeTemplate,
        string createAction,
        string updateAction,
        string resourceType)
    {
        _ = updateAction;
        _ = resourceType;
        var locationId = await MainLocationIdAsync();
        var clientId = await CreateClientAsync(SqlServerApiFactory.DefaultCompanyId, locationId);
        var route = string.Format(CultureInfo.InvariantCulture, routeTemplate, clientId);

        var authorized = await CreateActorAsync(AllContactPermissions);
        await GiveLocationAccessAsync(authorized.Id, locationId);
        using var authorizedClient = factory.CreateSecureClient();
        await SignInAsync(authorizedClient, authorized.Email!);
        var validationTraceId = Guid.NewGuid().ToString("N");
        var invalid = await PostWithTraceAsync(authorizedClient, route, new { }, validationTraceId);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, await CountAuditsByTraceAsync(validationTraceId));

        var forbidden = await CreateActorAsync();
        await GiveLocationAccessAsync(forbidden.Id, locationId);
        using var forbiddenClient = factory.CreateSecureClient();
        await SignInAsync(forbiddenClient, forbidden.Email!);
        var forbiddenTraceId = Guid.NewGuid().ToString("N");
        var denied = await PostWithTraceAsync(
            forbiddenClient,
            route,
            ValidPayload(route),
            forbiddenTraceId);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, await CountAuditsByTraceAsync(forbiddenTraceId));

        using var anonymousClient = factory.CreateSecureClient();
        var anonymousTraceId = Guid.NewGuid().ToString("N");
        var anonymous = await PostWithTraceAsync(
            anonymousClient,
            route,
            ValidPayload(route),
            anonymousTraceId);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(0, await CountAuditsByTraceAsync(anonymousTraceId));
        Assert.Equal(0, await CountSuccessfulAuditsAsync(authorized.Id, createAction));
        Assert.Equal(0, await CountSuccessfulAuditsAsync(forbidden.Id, createAction));
    }

    [Theory]
    [MemberData(nameof(AuditCases))]
    public async Task CrossCompanyAndLocationRejectionsEmitOnlyFailedAudit(
        string routeTemplate,
        string createAction,
        string updateAction,
        string resourceType)
    {
        _ = updateAction;
        var actor = await CreateActorAsync(AllContactPermissions);
        var accessibleLocationId = await MainLocationIdAsync();
        await GiveLocationAccessAsync(actor.Id, accessibleLocationId);
        var inaccessibleLocationId = await CreateLocationAsync(SqlServerApiFactory.DefaultCompanyId);
        var locationClientId = await CreateClientAsync(
            SqlServerApiFactory.DefaultCompanyId,
            inaccessibleLocationId);
        var otherTenant = await CreateCompanyAsync();
        var tenantClientId = await CreateClientAsync(otherTenant.CompanyId, otherTenant.LocationId);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);

        foreach (var rejectedClientId in new[] { locationClientId, tenantClientId })
        {
            var route = string.Format(
                CultureInfo.InvariantCulture,
                routeTemplate,
                rejectedClientId);
            var traceId = Guid.NewGuid().ToString("N");
            var response = await PostWithTraceAsync(client, route, ValidPayload(route), traceId);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var audit = await ReadAuditByTraceAsync(traceId);
            Assert.Equal(createAction, audit.Action);
            Assert.Equal(resourceType, audit.ResourceType);
            Assert.Equal(AuditOutcome.Failed, audit.Outcome);
            Assert.Equal("{\"reasonCode\":\"not-found\"}", audit.MetadataJson);
        }

        Assert.Equal(0, await CountSuccessfulAuditsAsync(actor.Id, createAction));
    }

    private static readonly string[] AllContactPermissions =
    [
        Permissions.ClientCareCoordinator.View,
        Permissions.ClientCareCoordinator.Create,
        Permissions.ClientCareCoordinator.Edit,
        Permissions.ClientContact.View,
        Permissions.ClientContact.Create,
        Permissions.ClientContact.Edit,
        Permissions.ClientResponsibleParty.View,
        Permissions.ClientResponsibleParty.Create,
        Permissions.ClientResponsibleParty.Edit,
        Permissions.ClientConsultationProvider.View,
        Permissions.ClientConsultationProvider.Create,
        Permissions.ClientConsultationProvider.Edit,
        Permissions.ClientPhysician.View,
        Permissions.ClientPhysician.Create,
        Permissions.ClientPhysician.Edit,
    ];

    private static readonly string[] ContactRouteTemplates =
    [
        "/api/clients/{0}/care-coordinators",
        "/api/clients/{0}/contacts",
        "/api/clients/{0}/responsible-parties",
        "/api/clients/{0}/consultation-providers",
        "/api/clients/{0}/physicians",
    ];

    private async Task<ApplicationUser> CreateActorAsync(params string[] permissions)
    {
        var actor = await factory.CreateUserAsync($"contact-workflow-{Guid.NewGuid():N}@example.test", Password);
        foreach (var permission in permissions)
        {
            await factory.GrantPermissionAsync(actor.Id, permission);
        }

        return actor;
    }

    private async Task<Guid> CreateClientAsync(Guid companyId, Guid locationId)
    {
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var client = Client.Create(id, companyId, "Characterization", null, "Client", null,
            new DateOnly(1990, 1, 1), ClientGender.Unknown, $"PMI-{Guid.NewGuid():N}",
            new DateOnly(2026, 1, 1), null, ClientStatus.Active, now);
        client.AssignLocation(locationId, "Test Location", now);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<(Guid CompanyId, Guid LocationId)> CreateCompanyAsync()
    {
        var companyId = Guid.CreateVersion7();
        var locationId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.Companies.Add(Company.Create(companyId, $"Characterization {companyId:N}", $"CHR-{companyId:N}", now));
        db.CompanyLocations.Add(CompanyLocation.Create(locationId, companyId, "Main", true, now));
        await db.SaveChangesAsync();
        return (companyId, locationId);
    }

    private async Task<Guid> CreateLocationAsync(Guid companyId)
    {
        var id = Guid.CreateVersion7();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        db.CompanyLocations.Add(CompanyLocation.Create(id, companyId, $"Location {id:N}", false, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return id;
    }

    private async Task GiveLocationAccessAsync(Guid userId, Guid locationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        if (!await db.UserLocations.AnyAsync(row => row.UserId == userId && row.LocationId == locationId))
        {
            db.UserLocations.Add(new ApplicationUserLocation { UserId = userId, LocationId = locationId });
            await db.SaveChangesAsync();
        }
    }

    private async Task<Guid> MainLocationIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await db.CompanyLocations
            .Where(location => location.CompanyId == SqlServerApiFactory.DefaultCompanyId)
            .OrderByDescending(location => location.IsMain)
            .Select(location => location.Id)
            .FirstAsync();
    }

    private async Task<AuditLog> ReadAuditAsync(
        Guid actorId,
        string action,
        Guid resourceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await db.Set<AuditLog>().AsNoTracking().SingleAsync(log =>
            log.ActorUserId == actorId
            && log.Action == action
            && log.ResourceId == resourceId.ToString("N"));
    }

    private async Task<AuditLog> ReadAuditByTraceAsync(string traceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await db.Set<AuditLog>().AsNoTracking().SingleAsync(log => log.TraceId == traceId);
    }

    private async Task<int> CountAuditsByTraceAsync(string traceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await db.Set<AuditLog>().CountAsync(log => log.TraceId == traceId);
    }

    private async Task<int> CountSuccessfulAuditsAsync(Guid actorId, string action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        return await db.Set<AuditLog>().CountAsync(log =>
            log.ActorUserId == actorId
            && log.Action == action
            && log.Outcome == AuditOutcome.Succeeded);
    }

    private static async Task SignInAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await client.GetAsync("/api/auth/csrf");
        csrf.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add(
            "X-XSRF-TOKEN",
            document.RootElement.GetProperty("requestToken").GetString());
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string method,
        string route,
        object payload) => method switch
        {
            "GET" => await client.GetAsync(route),
            "POST" => await client.PostAsJsonAsync(route, payload),
            "PUT" => await client.PutAsJsonAsync(route, payload),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };

    private static async Task<HttpResponseMessage> PostWithTraceAsync(
        HttpClient client,
        string route,
        object payload,
        string traceId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(payload),
        };
        var spanId = Guid.NewGuid().ToString("N")[..16];
        request.Headers.TryAddWithoutValidation(
            "traceparent",
            $"00-{traceId}-{spanId}-01");
        return await client.SendAsync(request);
    }

    private static void AssertSafeSuccessfulAudit(
        AuditLog audit,
        string resourceType,
        Guid clientId,
        Guid resourceId)
    {
        Assert.Equal(resourceType, audit.ResourceType);
        Assert.Equal(resourceId.ToString("N"), audit.ResourceId);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal($"{{\"clientId\":\"{clientId:N}\"}}", audit.MetadataJson);
        var persisted = string.Join('|', audit.Action, audit.ResourceType, audit.ResourceId,
            audit.MetadataJson ?? string.Empty);
        foreach (var excluded in new[]
        {
            "6125550100",
            "6125550101",
            "coordinator@example.test",
            "contact@example.test",
            "party@example.test",
            "provider@example.test",
            "1234567890",
            "55101",
            "Saint Paul",
            "Company",
            "Parent",
            "Sibling",
            "Main",
            "Relationship",
            "Address",
            "Support",
        })
        {
            Assert.DoesNotContain(excluded, persisted, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static object ValidPayload(string route) => route switch
    {
        var value when value.Contains("care-coordinators", StringComparison.Ordinal) =>
            new { name = "Coordinator", companyName = "Company", phone = "6125550100", phoneExtension = "1", fax = "6125550101", email = "coordinator@example.test", isActive = true },
        var value when value.Contains("responsible-parties", StringComparison.Ordinal) =>
            new { name = "Party", relationship = "Parent", phone = "6125550100", email = "party@example.test", address = "Main", livesWithClient = true, hasSpenddown = false, isActive = true },
        var value when value.Contains("consultation-providers", StringComparison.Ordinal) =>
            new { name = "Provider", phone = "6125550100", email = "provider@example.test", supportProviderName = "Support", supportProviderPhone = "6125550101", supportProviderAddress = "Main", isActive = true },
        var value when value.Contains("physicians", StringComparison.Ordinal) =>
            new { name = "Physician", npi = "1234567890", phone = "6125550100", fax = "6125550101", address = "Main", postalCode = "55101", city = "Saint Paul", state = "MN", isActive = true },
        _ => new { type = "General", name = "Contact", relationship = "Sibling", phone = "6125550100", email = "contact@example.test", address = "Main", isActive = true },
    };

    private static string ViewPermission(string route) => route switch
    {
        var value when value.Contains("care-coordinators", StringComparison.Ordinal) => Permissions.ClientCareCoordinator.View,
        var value when value.Contains("responsible-parties", StringComparison.Ordinal) => Permissions.ClientResponsibleParty.View,
        var value when value.Contains("consultation-providers", StringComparison.Ordinal) => Permissions.ClientConsultationProvider.View,
        var value when value.Contains("physicians", StringComparison.Ordinal) => Permissions.ClientPhysician.View,
        _ => Permissions.ClientContact.View,
    };

    private static async Task AssertValidationAsync(
        HttpResponseMessage response,
        params (string Key, string Message)[] expected)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        Assert.Equal(expected.Select(item => item.Key).Order(), errors.EnumerateObject().Select(item => item.Name).Order());
        foreach (var (key, message) in expected)
        {
            Assert.Equal(message, errors.GetProperty(key)[0].GetString());
        }
    }

    private static async Task AssertRoundTripAsync(
        HttpClient client,
        string route,
        object createRequest,
        object updateRequest,
        string assertedProperty,
        string expectedUpdatedValue)
    {
        var created = await client.PostAsJsonAsync(route, createRequest);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdJson.RootElement.GetProperty("id").GetGuid();

        var updated = await client.PutAsJsonAsync($"{route}/{id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedJson = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal(id, updatedJson.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(expectedUpdatedValue, updatedJson.RootElement.GetProperty(assertedProperty).GetString());

        var listed = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listedJson = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        Assert.Contains(
            listedJson.RootElement.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == id
                && item.GetProperty(assertedProperty).GetString() == expectedUpdatedValue);
    }
}
