using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // EF migration APIs conventionally use inline metadata arrays.

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyAdministrationAndTenantRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                schema: "dbo",
                table: "Roles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("UPDATE [dbo].[Roles] SET [CompanyId] = '01990000-0000-7000-8000-000000000001' WHERE [CompanyId] IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId", schema: "dbo", table: "Roles", type: "uniqueidentifier",
                nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NavigationFeatures",
                columns: new[] { "Id", "IconKey", "IsActive", "Key", "ModuleId", "Name", "ParentFeatureId", "Route", "SortOrder" },
                values: new object[] { 16, "companies", true, "companies", 5, "Companies", null, "/access-control/companies", 5 });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Permissions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 81, "Company.View" },
                    { 82, "Company.Create" },
                    { 83, "Company.Edit" },
                    { 84, "Company.Delete" }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NavigationFeaturePermissions",
                columns: new[] { "FeatureId", "PermissionId" },
                values: new object[] { 16, 81 });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "RolePermissionFeatureLinks",
                columns: new[] { "FeatureId", "PermissionId" },
                values: new object[,]
                {
                    { 16, 81 }, { 16, 82 }, { 16, 83 }, { 16, 84 }
                });

            migrationBuilder.Sql("""
                INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
                SELECT r.[Id], p.[Id]
                FROM [dbo].[Roles] r CROSS JOIN [dbo].[Permissions] p
                WHERE r.[NormalizedName] = 'SYSTEM ADMINISTRATOR' AND p.[Id] IN (81,82,83,84)
                  AND NOT EXISTS (SELECT 1 FROM [dbo].[RolePermissions] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CompanyId_Name",
                schema: "dbo",
                table: "Roles",
                columns: new[] { "CompanyId", "Name" });

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
            migrationBuilder.Sql("DELETE FROM [dbo].[RolePermissions] WHERE [PermissionId] IN (81,82,83,84);");
            migrationBuilder.Sql("DELETE FROM [dbo].[RolePermissionFeatureLinks] WHERE [PermissionId] IN (81,82,83,84);");
            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Companies_CompanyId",
                schema: "dbo",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_CompanyId_Name",
                schema: "dbo",
                table: "Roles");

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "NavigationFeaturePermissions",
                keyColumns: new[] { "FeatureId", "PermissionId" },
                keyValues: new object[] { 16, 81 });

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 84);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "NavigationFeatures",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 81);

            migrationBuilder.DropColumn(
                name: "CompanyId",
                schema: "dbo",
                table: "Roles");
        }
    }
}
