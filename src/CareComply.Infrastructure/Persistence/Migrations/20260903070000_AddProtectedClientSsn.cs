using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260903070000_AddProtectedClientSsn")]
public sealed class AddProtectedClientSsn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ProtectedSsn",
            schema: "dbo",
            table: "Clients",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("""
            IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [Id] = 49)
                INSERT INTO [dbo].[Permissions] ([Id], [Name]) VALUES (49, N'Client.RevealSsn');

            IF NOT EXISTS (
                SELECT 1 FROM [dbo].[RolePermissionFeatureLinks]
                WHERE [FeatureId] = 2 AND [PermissionId] = 49)
                INSERT INTO [dbo].[RolePermissionFeatureLinks] ([FeatureId], [PermissionId]) VALUES (2, 49);

            INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
            SELECT [Id], 49
            FROM [dbo].[Roles] AS [role]
            WHERE [role].[NormalizedName] = N'SYSTEM ADMINISTRATOR'
              AND NOT EXISTS (
                  SELECT 1 FROM [dbo].[RolePermissions] AS [existing]
                  WHERE [existing].[RoleId] = [role].[Id] AND [existing].[PermissionId] = 49);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM [dbo].[RolePermissions] WHERE [PermissionId] = 49;
            DELETE FROM [dbo].[RolePermissionFeatureLinks] WHERE [FeatureId] = 2 AND [PermissionId] = 49;
            DELETE FROM [dbo].[Permissions] WHERE [Id] = 49 AND [Name] = N'Client.RevealSsn';
            """);
        migrationBuilder.DropColumn(name: "ProtectedSsn", schema: "dbo", table: "Clients");
    }
}
