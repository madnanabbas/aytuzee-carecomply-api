using CareComply.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> entity)
    {
        entity.ToTable("Clients", table =>
        {
            table.HasCheckConstraint("CK_Clients_Status", "[Status] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Clients_Gender", "[Gender] BETWEEN 0 AND 5");
            table.HasCheckConstraint("CK_Clients_EvvAuthorizedHoursPerWeek",
                "[EvvAuthorizedHoursPerWeek] IS NULL OR ([EvvAuthorizedHoursPerWeek] >= 0 AND [EvvAuthorizedHoursPerWeek] <= 168)");
            table.HasCheckConstraint("CK_Clients_DischargeState",
                "([Status] <> 4 AND [DischargeDate] IS NULL) OR ([Status] = 4 AND [DischargeDate] IS NOT NULL)");
            table.HasCheckConstraint("CK_Clients_IntakeSocDates",
                "[IntakeDate] IS NULL OR [IntakeDate] <= [SocDate]");
            table.HasCheckConstraint("CK_Clients_DischargeSocDates",
                "[DischargeDate] IS NULL OR [DischargeDate] >= [SocDate]");
        });

        entity.HasKey(client => client.Id).HasName("PK_Clients");
        entity.Property(client => client.Id).ValueGeneratedNever();
        entity.Property(client => client.CompanyId).IsRequired();
        entity.Property(client => client.SupervisorUserId);
        entity.Property(client => client.FirstName).HasMaxLength(Client.MaximumNameLength).IsRequired();
        entity.Property(client => client.MiddleName).HasMaxLength(Client.MaximumNameLength).IsRequired();
        entity.Property(client => client.LastName).HasMaxLength(Client.MaximumNameLength).IsRequired();
        entity.Property(client => client.PreferredName).HasMaxLength(Client.MaximumPreferredNameLength).IsRequired();
        entity.Property(client => client.DateOfBirth).HasColumnType("date");
        entity.Property(client => client.Gender).HasConversion<int>();
        entity.Property(client => client.PmiNumber).HasMaxLength(Client.MaximumPmiLength).IsUnicode(false).IsRequired();
        entity.Property(client => client.ProtectedSsn).HasMaxLength(Client.MaximumProtectedSsnLength).IsRequired();
        entity.Property(client => client.InsuranceProvider).HasMaxLength(Client.MaximumInsuranceLength).IsRequired();
        entity.Property(client => client.Email).HasMaxLength(Client.MaximumEmailLength).IsUnicode(false).IsRequired();
        entity.Property(client => client.PrimaryPhone).HasMaxLength(Client.MaximumPhoneLength).IsUnicode(false).IsRequired();
        entity.Property(client => client.SecondaryPhone).HasMaxLength(Client.MaximumPhoneLength).IsUnicode(false).IsRequired();
        entity.Property(client => client.IntakeDate).HasColumnType("date");
        entity.Property(client => client.SocDate).HasColumnType("date");
        entity.Property(client => client.ReferralSource).HasMaxLength(Client.MaximumReferralSourceLength).IsRequired();
        entity.Property(client => client.Status).HasConversion<int>();
        entity.Property(client => client.Location).HasMaxLength(Client.MaximumLocationLength).IsRequired();
        entity.Property(client => client.DischargeDate).HasColumnType("date");
        entity.Property(client => client.DischargeReason).HasMaxLength(Client.MaximumTextLength).IsRequired();
        entity.Property(client => client.AddressLine1).HasMaxLength(Client.MaximumAddressLineLength).IsRequired();
        entity.Property(client => client.AddressLine2).HasMaxLength(Client.MaximumAddressLineLength).IsRequired();
        entity.Property(client => client.City).HasMaxLength(Client.MaximumCityLength).IsRequired();
        entity.Property(client => client.State).HasMaxLength(Client.MaximumStateLength).IsRequired();
        entity.Property(client => client.PostalCode).HasMaxLength(Client.MaximumPostalCodeLength).IsUnicode(false).IsRequired();
        entity.Property(client => client.County).HasMaxLength(Client.MaximumCountyLength).IsRequired();
        entity.Property(client => client.Country).HasMaxLength(Client.MaximumCountryLength).IsRequired();
        entity.Property(client => client.EvvAuthorizedHoursPerWeek).HasPrecision(6, 2);
        entity.Property(client => client.SafetyMeasures).HasMaxLength(Client.MaximumTextLength).IsRequired();
        entity.Property(client => client.SpecialEquipment).HasMaxLength(Client.MaximumTextLength).IsRequired();
        entity.Property(client => client.Allergies).HasMaxLength(Client.MaximumTextLength).IsRequired();
        entity.Property(client => client.OtherServices).HasMaxLength(Client.MaximumTextLength).IsRequired();
        entity.Property(client => client.CreatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(client => client.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(client => client.RowVersion).IsRowVersion().IsConcurrencyToken();

        entity.HasOne<CareComply.Domain.Companies.Company>()
            .WithMany()
            .HasForeignKey(client => client.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Clients_Companies_CompanyId");
        entity.HasIndex(client => new { client.CompanyId, client.PmiNumber })
            .HasDatabaseName("UX_Clients_PmiNumber")
            .IsUnique();
        entity.HasIndex(client => new { client.Status, client.LastName, client.FirstName })
            .HasDatabaseName("IX_Clients_Status_Name");
        entity.HasIndex(client => new { client.Location, client.Status })
            .HasDatabaseName("IX_Clients_Location_Status");
        entity.HasIndex(client => client.SocDate)
            .HasDatabaseName("IX_Clients_SocDate");
    }
}

internal sealed class ClientAdditionalProfileConfiguration : IEntityTypeConfiguration<ClientAdditionalProfile>
{
    public void Configure(EntityTypeBuilder<ClientAdditionalProfile> entity)
    {
        entity.ToTable("ClientAdditionalProfiles", table =>
            table.HasCheckConstraint("CK_ClientAdditionalProfiles_ProgramType", "[ProgramType] BETWEEN 0 AND 5"));
        entity.HasKey(profile => profile.ClientId).HasName("PK_ClientAdditionalProfiles");
        entity.Property(profile => profile.ProgramType).HasConversion<int>();
        entity.Property(profile => profile.ReferralDetails).HasMaxLength(ClientAdditionalProfile.MaximumLongTextLength).IsRequired();
        entity.Property(profile => profile.HomeCareRating).HasMaxLength(ClientAdditionalProfile.MaximumShortTextLength).IsRequired();
        entity.Property(profile => profile.MedicalEquipment).HasMaxLength(ClientAdditionalProfile.MaximumLongTextLength).IsRequired();
        entity.Property(profile => profile.PcaComplexity).HasMaxLength(ClientAdditionalProfile.MaximumShortTextLength).IsRequired();
        entity.Property(profile => profile.Demographics).HasMaxLength(ClientAdditionalProfile.MaximumLongTextLength).IsRequired();
        entity.Property(profile => profile.LivingArrangement).HasMaxLength(ClientAdditionalProfile.MaximumLongTextLength).IsRequired();
        entity.Property(profile => profile.PreferredClinic).HasMaxLength(ClientAdditionalProfile.MaximumShortTextLength).IsRequired();
        entity.Property(profile => profile.PreferredHospital).HasMaxLength(ClientAdditionalProfile.MaximumShortTextLength).IsRequired();
        entity.Property(profile => profile.PreferredAmbulance).HasMaxLength(ClientAdditionalProfile.MaximumShortTextLength).IsRequired();
        entity.Property(profile => profile.CaregiverPreference).HasMaxLength(ClientAdditionalProfile.MaximumLongTextLength).IsRequired();
        entity.Property(profile => profile.NutritionalRequirements).HasMaxLength(ClientAdditionalProfile.MaximumLongTextLength).IsRequired();
        entity.Property(profile => profile.Diet).HasMaxLength(ClientAdditionalProfile.MaximumLongTextLength).IsRequired();
        entity.Property(profile => profile.MealSchedule).HasMaxLength(ClientAdditionalProfile.MaximumLongTextLength).IsRequired();
        entity.Property(profile => profile.Notes).HasMaxLength(ClientAdditionalProfile.MaximumLongTextLength).IsRequired();
        entity.Property(profile => profile.AdditionalInformation).HasMaxLength(ClientAdditionalProfile.MaximumLongTextLength).IsRequired();
        entity.HasOne<Client>()
            .WithOne()
            .HasForeignKey<ClientAdditionalProfile>(profile => profile.ClientId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ClientAdditionalProfiles_Clients_ClientId");
    }
}

internal sealed class ClientServiceConfiguration : IEntityTypeConfiguration<ClientService>
{
    public void Configure(EntityTypeBuilder<ClientService> entity)
    {
        entity.ToTable("ClientServices", table =>
            table.HasCheckConstraint("CK_ClientServices_Dates", "[EndDate] IS NULL OR [EndDate] >= [StartDate]"));
        entity.HasKey(service => service.Id).HasName("PK_ClientServices");
        entity.Property(service => service.Id).ValueGeneratedNever();
        entity.Property(service => service.Code).HasMaxLength(ClientService.MaximumCodeLength).IsUnicode(false).IsRequired();
        entity.Property(service => service.Name).HasMaxLength(ClientService.MaximumNameLength).IsRequired();
        entity.Property(service => service.StartDate).HasColumnType("date");
        entity.Property(service => service.EndDate).HasColumnType("date");
        entity.HasOne<Client>()
            .WithMany()
            .HasForeignKey(service => service.ClientId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ClientServices_Clients_ClientId");
        entity.HasIndex(service => new { service.ClientId, service.Code, service.StartDate })
            .HasDatabaseName("UX_ClientServices_Client_Code_StartDate")
            .IsUnique();
        entity.HasIndex(service => new { service.ClientId, service.Code })
            .HasDatabaseName("UX_ClientServices_Client_Code_Active")
            .IsUnique()
            .HasFilter("[IsActive] = 1");
        entity.HasIndex(service => new { service.ClientId, service.IsActive })
            .HasDatabaseName("IX_ClientServices_Client_Active");
    }
}

internal sealed class ClientLanguagePreferenceConfiguration : IEntityTypeConfiguration<ClientLanguagePreference>
{
    public void Configure(EntityTypeBuilder<ClientLanguagePreference> entity)
    {
        entity.ToTable("ClientLanguagePreferences");
        entity.HasKey(language => new { language.ClientId, language.LanguageCode })
            .HasName("PK_ClientLanguagePreferences");
        entity.Property(language => language.LanguageCode)
            .HasMaxLength(ClientLanguagePreference.MaximumLanguageCodeLength).IsUnicode(false);
        entity.Property(language => language.LanguageName)
            .HasMaxLength(ClientLanguagePreference.MaximumLanguageNameLength).IsRequired();
        entity.HasOne<Client>()
            .WithMany()
            .HasForeignKey(language => language.ClientId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ClientLanguagePreferences_Clients_ClientId");
        entity.HasIndex(language => language.ClientId)
            .HasDatabaseName("UX_ClientLanguagePreferences_Primary")
            .IsUnique()
            .HasFilter("[IsPrimary] = 1");
    }
}

internal sealed class ClientContactConfiguration : IEntityTypeConfiguration<ClientContact>
{
    public void Configure(EntityTypeBuilder<ClientContact> entity)
    {
        entity.ToTable("ClientContacts", table =>
        {
            table.HasCheckConstraint("CK_ClientContacts_Type", "[Type] BETWEEN 1 AND 8");
            table.HasCheckConstraint("CK_ClientContacts_EffectiveDates",
                "[EffectiveFrom] IS NULL OR [EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
        });
        entity.HasKey(contact => contact.Id).HasName("PK_ClientContacts");
        entity.Property(contact => contact.Id).ValueGeneratedNever();
        entity.Property(contact => contact.Type).HasConversion<int>();
        entity.Property(contact => contact.Name).HasMaxLength(ClientContact.MaximumNameLength).IsRequired();
        entity.Property(contact => contact.Relationship).HasMaxLength(ClientContact.MaximumRelationshipLength).IsRequired();
        entity.Property(contact => contact.Phone).HasMaxLength(ClientContact.MaximumPhoneLength).IsUnicode(false).IsRequired();
        entity.Property(contact => contact.Email).HasMaxLength(ClientContact.MaximumEmailLength).IsUnicode(false).IsRequired();
        entity.Property(contact => contact.EffectiveFrom).HasColumnType("date");
        entity.Property(contact => contact.EffectiveTo).HasColumnType("date");
        entity.Property(contact => contact.Notes).HasMaxLength(ClientContact.MaximumNotesLength).IsRequired();
        entity.Property(contact => contact.Address).HasMaxLength(ClientContact.MaximumAddressLength).IsRequired();
        entity.Property(contact => contact.CompanyName).HasMaxLength(ClientContact.MaximumCompanyLength).IsRequired();
        entity.Property(contact => contact.PhoneExtension).HasMaxLength(ClientContact.MaximumExtensionLength).IsRequired();
        entity.Property(contact => contact.Fax).HasMaxLength(ClientContact.MaximumPhoneLength).IsRequired();
        entity.Property(contact => contact.SecondaryName).HasMaxLength(ClientContact.MaximumNameLength).IsRequired();
        entity.Property(contact => contact.SecondaryPhone).HasMaxLength(ClientContact.MaximumPhoneLength).IsRequired();
        entity.Property(contact => contact.SecondaryAddress).HasMaxLength(ClientContact.MaximumAddressLength).IsRequired();
        entity.Property(contact => contact.Npi).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(contact => contact.PostalCode).HasMaxLength(ClientContact.MaximumPostalCodeLength).IsRequired();
        entity.Property(contact => contact.City).HasMaxLength(ClientContact.MaximumCityLength).IsRequired();
        entity.Property(contact => contact.State).HasMaxLength(ClientContact.MaximumStateLength).IsRequired();
        entity.HasOne<Client>()
            .WithMany()
            .HasForeignKey(contact => contact.ClientId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ClientContacts_Clients_ClientId");
        entity.HasIndex(contact => new { contact.ClientId, contact.Type })
            .HasDatabaseName("IX_ClientContacts_Client_Type");
        entity.HasIndex(contact => new { contact.ClientId, contact.Type })
            .HasDatabaseName("UX_ClientContacts_Client_Type_Primary")
            .IsUnique()
            .HasFilter("[IsPrimary] = 1");
    }
}

internal sealed class ClientHospitalizationConfiguration : IEntityTypeConfiguration<ClientHospitalization>
{
    public void Configure(EntityTypeBuilder<ClientHospitalization> entity)
    {
        entity.ToTable("ClientHospitalizations", table =>
            table.HasCheckConstraint("CK_ClientHospitalizations_Dates",
                "[DischargedAtUtc] IS NULL OR [DischargedAtUtc] >= [AdmittedAtUtc]"));
        entity.HasKey(hospitalization => hospitalization.Id).HasName("PK_ClientHospitalizations");
        entity.Property(hospitalization => hospitalization.Id).ValueGeneratedNever();
        entity.Property(hospitalization => hospitalization.HospitalName)
            .HasMaxLength(ClientHospitalization.HospitalMaxLength).IsRequired();
        entity.Property(hospitalization => hospitalization.AdmittedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(hospitalization => hospitalization.DischargedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(hospitalization => hospitalization.Reason)
            .HasMaxLength(ClientHospitalization.ReasonMaxLength).IsRequired();
        entity.Property(hospitalization => hospitalization.Diagnosis)
            .HasMaxLength(ClientHospitalization.DetailsMaxLength).IsRequired();
        entity.Property(hospitalization => hospitalization.DischargeInstructions)
            .HasMaxLength(ClientHospitalization.DetailsMaxLength).IsRequired();
        entity.Property(hospitalization => hospitalization.RowVersion).IsRowVersion();
        entity.HasOne<CareComply.Infrastructure.Identity.ApplicationUser>()
            .WithMany().HasForeignKey(hospitalization => hospitalization.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Client>()
            .WithMany()
            .HasForeignKey(hospitalization => hospitalization.ClientId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ClientHospitalizations_Clients_ClientId");
        entity.HasIndex(hospitalization => new { hospitalization.ClientId, hospitalization.AdmittedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ClientHospitalizations_Client_AdmittedAtUtc");
    }
}
