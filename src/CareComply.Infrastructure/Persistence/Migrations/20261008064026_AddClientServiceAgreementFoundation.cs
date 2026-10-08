using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // EF-generated migration column arrays are single-use call arguments.

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientServiceAgreementFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientServiceAgreements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementNumber = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PayerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InsuredMemberId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    PriorAuthorizationNumber = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientServiceAgreements", x => x.Id);
                    table.CheckConstraint("CK_ClientServiceAgreements_Dates", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_ClientServiceAgreements_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "dbo",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientServiceAgreementLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientServiceAgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MinutesPerUnit = table.Column<int>(type: "int", nullable: true),
                    AuthorizedUnits = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    RatePerUnit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PlannedHoursPerVisit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PlannedVisitsPerWeek = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PrimaryDiagnosisText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IcdCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientServiceAgreementLines", x => x.Id);
                    table.CheckConstraint("CK_ClientServiceAgreementLines_AuthorizedUnits", "[AuthorizedUnits] IS NULL OR [AuthorizedUnits] >= 0");
                    table.CheckConstraint("CK_ClientServiceAgreementLines_MinutesPerUnit", "[MinutesPerUnit] IS NULL OR [MinutesPerUnit] > 0");
                    table.CheckConstraint("CK_ClientServiceAgreementLines_PlannedHoursPerVisit", "[PlannedHoursPerVisit] IS NULL OR [PlannedHoursPerVisit] >= 0");
                    table.CheckConstraint("CK_ClientServiceAgreementLines_PlannedVisitsPerWeek", "[PlannedVisitsPerWeek] IS NULL OR [PlannedVisitsPerWeek] >= 0");
                    table.CheckConstraint("CK_ClientServiceAgreementLines_RatePerUnit", "[RatePerUnit] IS NULL OR [RatePerUnit] >= 0");
                    table.ForeignKey(
                        name: "FK_ClientServiceAgreementLines_ClientServiceAgreements_ClientServiceAgreementId",
                        column: x => x.ClientServiceAgreementId,
                        principalSchema: "dbo",
                        principalTable: "ClientServiceAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientServiceAgreementLines_ClientServiceAgreementId",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                column: "ClientServiceAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientServiceAgreementLines_ClientServiceAgreementId_ServiceCode",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                columns: new[] { "ClientServiceAgreementId", "ServiceCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientServiceAgreements_ClientId",
                schema: "dbo",
                table: "ClientServiceAgreements",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientServiceAgreements_ClientId_StartDate",
                schema: "dbo",
                table: "ClientServiceAgreements",
                columns: new[] { "ClientId", "StartDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientServiceAgreementLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ClientServiceAgreements",
                schema: "dbo");
        }
    }
}
