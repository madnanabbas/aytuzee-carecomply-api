using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // EF migration-generated constant arrays

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenantFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Clients_PmiNumber",
                schema: "dbo",
                table: "Clients");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                schema: "dbo",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdministrator",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                schema: "dbo",
                table: "Clients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Companies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Companies",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "IsActive", "Name", "UpdatedAtUtc" },
                values: new object[] { new Guid("01990000-0000-7000-8000-000000000001"), "AYTUZEE-DEMO", new DateTimeOffset(new DateTime(2026, 9, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Aytuzee Demo Company", new DateTimeOffset(new DateTime(2026, 9, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.Sql("""
                UPDATE [dbo].[Users]
                SET [CompanyId] = '01990000-0000-7000-8000-000000000001';

                UPDATE [dbo].[Clients]
                SET [CompanyId] = '01990000-0000-7000-8000-000000000001';

                UPDATE [dbo].[Users]
                SET [IsPlatformAdministrator] = 1
                WHERE [NormalizedEmail] = 'ADMIN@CARECOMPLY.LOCAL';
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                schema: "dbo",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                schema: "dbo",
                table: "Clients",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId_IsActive",
                schema: "dbo",
                table: "Users",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UX_Clients_PmiNumber",
                schema: "dbo",
                table: "Clients",
                columns: new[] { "CompanyId", "PmiNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                schema: "dbo",
                table: "Companies",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "UX_Companies_Code",
                schema: "dbo",
                table: "Companies",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Companies_CompanyId",
                schema: "dbo",
                table: "Clients",
                column: "CompanyId",
                principalSchema: "dbo",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Companies_CompanyId",
                schema: "dbo",
                table: "Users",
                column: "CompanyId",
                principalSchema: "dbo",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Companies_CompanyId",
                schema: "dbo",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Companies_CompanyId",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Companies",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Users_CompanyId_IsActive",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UX_Clients_PmiNumber",
                schema: "dbo",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsPlatformAdministrator",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                schema: "dbo",
                table: "Clients");

            migrationBuilder.CreateIndex(
                name: "UX_Clients_PmiNumber",
                schema: "dbo",
                table: "Clients",
                column: "PmiNumber",
                unique: true);
        }
    }
}
