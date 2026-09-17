using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // EF migration-generated constant arrays

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientClinicalContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "dbo",
                table: "ClientContacts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Npi",
                schema: "dbo",
                table: "ClientContacts",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                schema: "dbo",
                table: "ClientContacts",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecondaryAddress",
                schema: "dbo",
                table: "ClientContacts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecondaryName",
                schema: "dbo",
                table: "ClientContacts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecondaryPhone",
                schema: "dbo",
                table: "ClientContacts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "dbo",
                table: "ClientContacts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Permissions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 91, "ClientConsultationProvider.View" },
                    { 92, "ClientConsultationProvider.Create" },
                    { 93, "ClientConsultationProvider.Edit" },
                    { 94, "ClientPhysician.View" },
                    { 95, "ClientPhysician.Create" },
                    { 96, "ClientPhysician.Edit" }
                });

            migrationBuilder.Sql("""
                INSERT INTO [dbo].[RolePermissionFeatureLinks] ([FeatureId], [PermissionId])
                SELECT clientFeature.[FeatureId], clinicalPermission.[Id]
                FROM [dbo].[RolePermissionFeatureLinks] AS clientFeature
                INNER JOIN [dbo].[Permissions] AS clientPermission
                    ON clientPermission.[Id] = clientFeature.[PermissionId]
                    AND clientPermission.[Name] = 'Client.View'
                CROSS JOIN [dbo].[Permissions] AS clinicalPermission
                WHERE clinicalPermission.[Name] IN (
                    'ClientConsultationProvider.View', 'ClientConsultationProvider.Create', 'ClientConsultationProvider.Edit',
                    'ClientPhysician.View', 'ClientPhysician.Create', 'ClientPhysician.Edit')
                  AND NOT EXISTS (
                    SELECT 1 FROM [dbo].[RolePermissionFeatureLinks] AS existing
                    WHERE existing.[PermissionId] = clinicalPermission.[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE link
                FROM [dbo].[RolePermissionFeatureLinks] AS link
                INNER JOIN [dbo].[Permissions] AS permission ON permission.[Id] = link.[PermissionId]
                WHERE permission.[Name] IN (
                    'ClientConsultationProvider.View', 'ClientConsultationProvider.Create', 'ClientConsultationProvider.Edit',
                    'ClientPhysician.View', 'ClientPhysician.Create', 'ClientPhysician.Edit');
                """);
            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 96);

            migrationBuilder.DropColumn(
                name: "City",
                schema: "dbo",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "Npi",
                schema: "dbo",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                schema: "dbo",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "SecondaryAddress",
                schema: "dbo",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "SecondaryName",
                schema: "dbo",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "SecondaryPhone",
                schema: "dbo",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "dbo",
                table: "ClientContacts");
        }
    }
}
