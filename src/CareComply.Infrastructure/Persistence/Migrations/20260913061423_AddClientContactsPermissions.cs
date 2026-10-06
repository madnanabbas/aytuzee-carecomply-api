using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // EF migration-generated constant arrays

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientContactsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Permissions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 85, "ClientContact.View" },
                    { 86, "ClientContact.Create" },
                    { 87, "ClientContact.Edit" }
                });

            migrationBuilder.Sql("""
                INSERT INTO [dbo].[RolePermissionFeatureLinks] ([FeatureId], [PermissionId])
                SELECT clientFeature.[FeatureId], contactPermission.[Id]
                FROM [dbo].[RolePermissionFeatureLinks] AS clientFeature
                INNER JOIN [dbo].[Permissions] AS clientPermission
                    ON clientPermission.[Id] = clientFeature.[PermissionId]
                    AND clientPermission.[Name] = 'Client.View'
                CROSS JOIN [dbo].[Permissions] AS contactPermission
                WHERE contactPermission.[Name] IN ('ClientContact.View', 'ClientContact.Create', 'ClientContact.Edit')
                  AND NOT EXISTS (
                    SELECT 1 FROM [dbo].[RolePermissionFeatureLinks] AS existing
                    WHERE existing.[PermissionId] = contactPermission.[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE link
                FROM [dbo].[RolePermissionFeatureLinks] AS link
                INNER JOIN [dbo].[Permissions] AS permission ON permission.[Id] = link.[PermissionId]
                WHERE permission.[Name] IN ('ClientContact.View', 'ClientContact.Create', 'ClientContact.Edit');
                """);
            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 87);
        }
    }
}
