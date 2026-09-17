using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core scaffolds migration index columns as inline arrays.

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyLocationsAndLocationScopedAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                schema: "dbo",
                table: "Clients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyLocations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsMain = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyLocations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [dbo].[CompanyLocations] ([Id], [CompanyId], [Name], [IsMain], [IsActive], [CreatedAtUtc])
                SELECT NEWID(), company.[Id], N'Main', 1, 1, SYSUTCDATETIME()
                FROM [dbo].[Companies] AS company
                WHERE NOT EXISTS (
                    SELECT 1 FROM [dbo].[CompanyLocations] AS location
                    WHERE location.[CompanyId] = company.[Id] AND location.[IsMain] = 1);

                INSERT INTO [dbo].[CompanyLocations] ([Id], [CompanyId], [Name], [IsMain], [IsActive], [CreatedAtUtc])
                SELECT NEWID(), demo.[CompanyId], names.[Name], 0, 1, SYSUTCDATETIME()
                FROM (VALUES (CAST('01990000-0000-7000-8000-000000000001' AS uniqueidentifier))) AS demo([CompanyId])
                CROSS JOIN (VALUES (N'Minneapolis'), (N'Minnesota'), (N'St. Paul'), (N'Bloomington'), (N'Brooklyn Park')) AS names([Name])
                WHERE EXISTS (SELECT 1 FROM [dbo].[Companies] WHERE [Id] = demo.[CompanyId])
                  AND NOT EXISTS (
                    SELECT 1 FROM [dbo].[CompanyLocations] AS location
                    WHERE location.[CompanyId] = demo.[CompanyId] AND location.[Name] = names.[Name]);

                UPDATE client
                SET client.[LocationId] = location.[Id], client.[Location] = N'Main'
                FROM [dbo].[Clients] AS client
                INNER JOIN [dbo].[CompanyLocations] AS location
                    ON location.[CompanyId] = client.[CompanyId] AND location.[IsMain] = 1;
                """);

            migrationBuilder.CreateTable(
                name: "UserLocations",
                schema: "dbo",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLocations", x => new { x.UserId, x.LocationId });
                    table.ForeignKey(
                        name: "FK_UserLocations_CompanyLocations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "dbo",
                        principalTable: "CompanyLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserLocations_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [dbo].[UserLocations] ([UserId], [LocationId])
                SELECT appUser.[Id], location.[Id]
                FROM [dbo].[Users] AS appUser
                INNER JOIN [dbo].[CompanyLocations] AS location
                    ON location.[CompanyId] = appUser.[CompanyId] AND location.[IsMain] = 1
                WHERE appUser.[CompanyId] IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM [dbo].[UserLocations] AS assignment
                    WHERE assignment.[UserId] = appUser.[Id] AND assignment.[LocationId] = location.[Id]);
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "LocationId",
                schema: "dbo",
                table: "Clients",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CompanyId_LocationId_Status",
                schema: "dbo",
                table: "Clients",
                columns: new[] { "CompanyId", "LocationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_LocationId",
                schema: "dbo",
                table: "Clients",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLocations_CompanyId_IsMain",
                schema: "dbo",
                table: "CompanyLocations",
                columns: new[] { "CompanyId", "IsMain" },
                unique: true,
                filter: "[IsMain] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLocations_CompanyId_Name",
                schema: "dbo",
                table: "CompanyLocations",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLocations_LocationId",
                schema: "dbo",
                table: "UserLocations",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_CompanyLocations_LocationId",
                schema: "dbo",
                table: "Clients",
                column: "LocationId",
                principalSchema: "dbo",
                principalTable: "CompanyLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_CompanyLocations_LocationId",
                schema: "dbo",
                table: "Clients");

            migrationBuilder.DropTable(
                name: "UserLocations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CompanyLocations",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Clients_CompanyId_LocationId_Status",
                schema: "dbo",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_LocationId",
                schema: "dbo",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LocationId",
                schema: "dbo",
                table: "Clients");
        }
    }
}
#pragma warning restore CA1861
