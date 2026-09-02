using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // EF-generated migration column arrays are single-use call arguments.

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260902090000_AddClientDomain")]
public sealed class AddClientDomain : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Clients",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PreferredName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                Gender = table.Column<int>(type: "int", nullable: false),
                PmiNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                InsuranceProvider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "varchar(320)", unicode: false, maxLength: 320, nullable: false),
                PrimaryPhone = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                SecondaryPhone = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                IntakeDate = table.Column<DateOnly>(type: "date", nullable: true),
                SocDate = table.Column<DateOnly>(type: "date", nullable: false),
                ReferralSource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                Location = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                IsOnboarding = table.Column<bool>(type: "bit", nullable: false),
                DischargeDate = table.Column<DateOnly>(type: "date", nullable: true),
                DischargeReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PostalCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                County = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EvvAuthorizedHoursPerWeek = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                SafetyMeasures = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                SpecialEquipment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                Allergies = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                OtherServices = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Clients", value => value.Id);
                table.CheckConstraint("CK_Clients_Status", "[Status] BETWEEN 1 AND 5");
                table.CheckConstraint("CK_Clients_Gender", "[Gender] BETWEEN 0 AND 5");
                table.CheckConstraint("CK_Clients_EvvAuthorizedHoursPerWeek", "[EvvAuthorizedHoursPerWeek] IS NULL OR ([EvvAuthorizedHoursPerWeek] >= 0 AND [EvvAuthorizedHoursPerWeek] <= 168)");
                table.CheckConstraint("CK_Clients_DischargeState", "([Status] <> 4 AND [DischargeDate] IS NULL) OR ([Status] = 4 AND [DischargeDate] IS NOT NULL)");
                table.CheckConstraint("CK_Clients_IntakeSocDates", "[IntakeDate] IS NULL OR [IntakeDate] <= [SocDate]");
                table.CheckConstraint("CK_Clients_DischargeSocDates", "[DischargeDate] IS NULL OR [DischargeDate] >= [SocDate]");
            });

        migrationBuilder.CreateTable(
            name: "ClientAdditionalProfiles",
            schema: "dbo",
            columns: table => new
            {
                ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProgramType = table.Column<int>(type: "int", nullable: false),
                ReferralDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                HomeCareRating = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                MedicalEquipment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                PcaComplexity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Demographics = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                Smokes = table.Column<bool>(type: "bit", nullable: true),
                IsVeteran = table.Column<bool>(type: "bit", nullable: true),
                HasPets = table.Column<bool>(type: "bit", nullable: true),
                LivingArrangement = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                PreferredClinic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PreferredHospital = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PreferredAmbulance = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CaregiverPreference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                NutritionalRequirements = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                Diet = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                MealSchedule = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientAdditionalProfiles", value => value.ClientId);
                table.CheckConstraint("CK_ClientAdditionalProfiles_ProgramType", "[ProgramType] BETWEEN 0 AND 5");
                table.ForeignKey(
                    name: "FK_ClientAdditionalProfiles_Clients_ClientId",
                    column: value => value.ClientId,
                    principalSchema: "dbo",
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ClientContacts",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Relationship = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Phone = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                Email = table.Column<string>(type: "varchar(320)", unicode: false, maxLength: 320, nullable: false),
                IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                IsEmergencyContact = table.Column<bool>(type: "bit", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientContacts", value => value.Id);
                table.CheckConstraint("CK_ClientContacts_Type", "[Type] BETWEEN 1 AND 8");
                table.CheckConstraint("CK_ClientContacts_EffectiveDates", "[EffectiveFrom] IS NULL OR [EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.ForeignKey(
                    name: "FK_ClientContacts_Clients_ClientId",
                    column: value => value.ClientId,
                    principalSchema: "dbo",
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ClientHospitalizations",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                HospitalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                AdmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                DischargedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientHospitalizations", value => value.Id);
                table.CheckConstraint("CK_ClientHospitalizations_Dates", "[DischargedAtUtc] IS NULL OR [DischargedAtUtc] >= [AdmittedAtUtc]");
                table.ForeignKey(
                    name: "FK_ClientHospitalizations_Clients_ClientId",
                    column: value => value.ClientId,
                    principalSchema: "dbo",
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ClientLanguagePreferences",
            schema: "dbo",
            columns: table => new
            {
                ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LanguageCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                LanguageName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IsPrimary = table.Column<bool>(type: "bit", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientLanguagePreferences", value => new { value.ClientId, value.LanguageCode });
                table.ForeignKey(
                    name: "FK_ClientLanguagePreferences_Clients_ClientId",
                    column: value => value.ClientId,
                    principalSchema: "dbo",
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ClientServices",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientServices", value => value.Id);
                table.CheckConstraint("CK_ClientServices_Dates", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                table.ForeignKey(
                    name: "FK_ClientServices_Clients_ClientId",
                    column: value => value.ClientId,
                    principalSchema: "dbo",
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "UX_Clients_PmiNumber", schema: "dbo", table: "Clients", column: "PmiNumber", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Clients_Status_Name", schema: "dbo", table: "Clients", columns: new[] { "Status", "LastName", "FirstName" });
        migrationBuilder.CreateIndex(name: "IX_Clients_Location_Status", schema: "dbo", table: "Clients", columns: new[] { "Location", "Status" });
        migrationBuilder.CreateIndex(name: "IX_Clients_SocDate", schema: "dbo", table: "Clients", column: "SocDate");
        migrationBuilder.CreateIndex(name: "IX_ClientContacts_Client_Type", schema: "dbo", table: "ClientContacts", columns: new[] { "ClientId", "Type" });
        migrationBuilder.CreateIndex(name: "UX_ClientContacts_Client_Type_Primary", schema: "dbo", table: "ClientContacts", columns: new[] { "ClientId", "Type" }, unique: true, filter: "[IsPrimary] = 1");
        migrationBuilder.CreateIndex(name: "IX_ClientHospitalizations_Client_AdmittedAtUtc", schema: "dbo", table: "ClientHospitalizations", columns: new[] { "ClientId", "AdmittedAtUtc" }, descending: new[] { false, true });
        migrationBuilder.CreateIndex(name: "UX_ClientLanguagePreferences_Primary", schema: "dbo", table: "ClientLanguagePreferences", column: "ClientId", unique: true, filter: "[IsPrimary] = 1");
        migrationBuilder.CreateIndex(name: "UX_ClientServices_Client_Code_StartDate", schema: "dbo", table: "ClientServices", columns: new[] { "ClientId", "Code", "StartDate" }, unique: true);
        migrationBuilder.CreateIndex(name: "UX_ClientServices_Client_Code_Active", schema: "dbo", table: "ClientServices", columns: new[] { "ClientId", "Code" }, unique: true, filter: "[IsActive] = 1");
        migrationBuilder.CreateIndex(name: "IX_ClientServices_Client_Active", schema: "dbo", table: "ClientServices", columns: new[] { "ClientId", "IsActive" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ClientAdditionalProfiles", schema: "dbo");
        migrationBuilder.DropTable(name: "ClientContacts", schema: "dbo");
        migrationBuilder.DropTable(name: "ClientHospitalizations", schema: "dbo");
        migrationBuilder.DropTable(name: "ClientLanguagePreferences", schema: "dbo");
        migrationBuilder.DropTable(name: "ClientServices", schema: "dbo");
        migrationBuilder.DropTable(name: "Clients", schema: "dbo");
    }
}
