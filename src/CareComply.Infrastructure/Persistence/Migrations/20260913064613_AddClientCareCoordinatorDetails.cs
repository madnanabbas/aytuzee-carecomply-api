using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // EF migration-generated constant arrays

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientCareCoordinatorDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                schema: "dbo",
                table: "ClientContacts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Fax",
                schema: "dbo",
                table: "ClientContacts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneExtension",
                schema: "dbo",
                table: "ClientContacts",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Permissions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 88, "ClientCareCoordinator.View" },
                    { 89, "ClientCareCoordinator.Create" },
                    { 90, "ClientCareCoordinator.Edit" }
                });

            migrationBuilder.Sql("""
                INSERT INTO [dbo].[RolePermissionFeatureLinks] ([FeatureId], [PermissionId])
                SELECT clientFeature.[FeatureId], coordinatorPermission.[Id]
                FROM [dbo].[RolePermissionFeatureLinks] AS clientFeature
                INNER JOIN [dbo].[Permissions] AS clientPermission
                    ON clientPermission.[Id] = clientFeature.[PermissionId]
                    AND clientPermission.[Name] = 'Client.View'
                CROSS JOIN [dbo].[Permissions] AS coordinatorPermission
                WHERE coordinatorPermission.[Name] IN ('ClientCareCoordinator.View', 'ClientCareCoordinator.Create', 'ClientCareCoordinator.Edit')
                  AND NOT EXISTS (
                    SELECT 1 FROM [dbo].[RolePermissionFeatureLinks] AS existing
                    WHERE existing.[PermissionId] = coordinatorPermission.[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE link
                FROM [dbo].[RolePermissionFeatureLinks] AS link
                INNER JOIN [dbo].[Permissions] AS permission ON permission.[Id] = link.[PermissionId]
                WHERE permission.[Name] IN ('ClientCareCoordinator.View', 'ClientCareCoordinator.Create', 'ClientCareCoordinator.Edit');
                """);
            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 90);

            migrationBuilder.DropColumn(
                name: "CompanyName",
                schema: "dbo",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "Fax",
                schema: "dbo",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "PhoneExtension",
                schema: "dbo",
                table: "ClientContacts");
        }
    }
}
