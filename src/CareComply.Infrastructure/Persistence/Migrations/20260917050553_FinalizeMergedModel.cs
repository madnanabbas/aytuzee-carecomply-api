using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeMergedModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_CompanyId",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UX_Clients_PmiNumber",
                schema: "dbo",
                table: "Clients");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPlatformAdministrator",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId_IsActive",
                schema: "dbo",
                table: "Users",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CompanyId_Name",
                schema: "dbo",
                table: "Roles",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "UX_Clients_PmiNumber",
                schema: "dbo",
                table: "Clients",
                columns: new[] { "CompanyId", "PmiNumber" },
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
                name: "FK_Roles_Companies_CompanyId",
                schema: "dbo",
                table: "Roles",
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
                name: "FK_Roles_Companies_CompanyId",
                schema: "dbo",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Users_CompanyId_IsActive",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Roles_CompanyId_Name",
                schema: "dbo",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "UX_Clients_PmiNumber",
                schema: "dbo",
                table: "Clients");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPlatformAdministrator",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId",
                schema: "dbo",
                table: "Users",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "UX_Clients_PmiNumber",
                schema: "dbo",
                table: "Clients",
                column: "PmiNumber",
                unique: true);
        }
    }
}
