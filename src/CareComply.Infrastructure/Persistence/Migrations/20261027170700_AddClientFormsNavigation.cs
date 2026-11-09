using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // EF-generated migration column arrays are single-use call arguments.

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientFormsNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NavigationFeatures",
                columns: new[] { "Id", "IconKey", "IsActive", "Key", "ModuleId", "Name", "ParentFeatureId", "Route", "SortOrder" },
                values: new object[] { 18, "documents", true, "client-forms", 2, "Forms", 2, "/clients/forms", 10 });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NavigationFeaturePermissions",
                columns: new[] { "FeatureId", "PermissionId" },
                values: new object[] { 18, 74 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "NavigationFeaturePermissions",
                keyColumns: new[] { "FeatureId", "PermissionId" },
                keyValues: new object[] { 18, 74 });

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "NavigationFeatures",
                keyColumn: "Id",
                keyValue: 18);
        }
    }
}
