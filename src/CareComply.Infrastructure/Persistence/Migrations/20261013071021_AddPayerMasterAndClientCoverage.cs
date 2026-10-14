using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Generated migration array arguments are executed once.

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayerMasterAndClientCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientCoverageId",
                schema: "dbo",
                table: "ClientServiceAgreements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PayerId",
                schema: "dbo",
                table: "ClientServiceAgreements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Payers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientCoverages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManualPayerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MemberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MedicaidId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientCoverages", x => x.Id);
                    table.CheckConstraint("CK_ClientCoverages_Dates", "[EffectiveFrom] IS NULL OR [EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ClientCoverages_PayerSelection", "([PayerId] IS NOT NULL AND [ManualPayerName] IS NULL) OR ([PayerId] IS NULL AND [ManualPayerName] IS NOT NULL AND LEN(LTRIM(RTRIM([ManualPayerName]))) > 0)");
                    table.ForeignKey(
                        name: "FK_ClientCoverages_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "dbo",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientCoverages_Payers_PayerId",
                        column: x => x.PayerId,
                        principalSchema: "dbo",
                        principalTable: "Payers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NavigationFeatures",
                columns: new[] { "Id", "IconKey", "IsActive", "Key", "ModuleId", "Name", "ParentFeatureId", "Route", "SortOrder" },
                values: new object[] { 17, "payers", true, "payers", 5, "Payers", null, "/access-control/payers", 7 });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Permissions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 98, "Payer.View" },
                    { 99, "Payer.Create" },
                    { 100, "Payer.Edit" },
                    { 101, "ClientCoverage.View" },
                    { 102, "ClientCoverage.Create" },
                    { 103, "ClientCoverage.Edit" }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NavigationFeaturePermissions",
                columns: new[] { "FeatureId", "PermissionId" },
                values: new object[] { 17, 98 });

            migrationBuilder.Sql("""
                INSERT INTO [dbo].[RolePermissionFeatureLinks] ([FeatureId], [PermissionId])
                VALUES (17, 98), (17, 99), (17, 100), (2, 101), (2, 102), (2, 103);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ClientServiceAgreements_ClientCoverageId",
                schema: "dbo",
                table: "ClientServiceAgreements",
                column: "ClientCoverageId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientServiceAgreements_PayerId",
                schema: "dbo",
                table: "ClientServiceAgreements",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientCoverages_PayerId",
                schema: "dbo",
                table: "ClientCoverages",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "UX_ClientCoverages_OnePrimaryPerClient",
                schema: "dbo",
                table: "ClientCoverages",
                column: "ClientId",
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Payers_CompanyId_Active_Name",
                schema: "dbo",
                table: "Payers",
                columns: new[] { "CompanyId", "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "UX_Payers_CompanyId_Name",
                schema: "dbo",
                table: "Payers",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientServiceAgreements_ClientCoverages_ClientCoverageId",
                schema: "dbo",
                table: "ClientServiceAgreements",
                column: "ClientCoverageId",
                principalSchema: "dbo",
                principalTable: "ClientCoverages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientServiceAgreements_Payers_PayerId",
                schema: "dbo",
                table: "ClientServiceAgreements",
                column: "PayerId",
                principalSchema: "dbo",
                principalTable: "Payers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [dbo].[RolePermissionFeatureLinks]
                WHERE [PermissionId] BETWEEN 98 AND 103;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ClientServiceAgreements_ClientCoverages_ClientCoverageId",
                schema: "dbo",
                table: "ClientServiceAgreements");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientServiceAgreements_Payers_PayerId",
                schema: "dbo",
                table: "ClientServiceAgreements");

            migrationBuilder.DropTable(
                name: "ClientCoverages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Payers",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_ClientServiceAgreements_ClientCoverageId",
                schema: "dbo",
                table: "ClientServiceAgreements");

            migrationBuilder.DropIndex(
                name: "IX_ClientServiceAgreements_PayerId",
                schema: "dbo",
                table: "ClientServiceAgreements");

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "NavigationFeaturePermissions",
                keyColumns: new[] { "FeatureId", "PermissionId" },
                keyValues: new object[] { 17, 98 });

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 99);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 100);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "NavigationFeatures",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 98);

            migrationBuilder.DropColumn(
                name: "ClientCoverageId",
                schema: "dbo",
                table: "ClientServiceAgreements");

            migrationBuilder.DropColumn(
                name: "PayerId",
                schema: "dbo",
                table: "ClientServiceAgreements");
        }
    }
}
