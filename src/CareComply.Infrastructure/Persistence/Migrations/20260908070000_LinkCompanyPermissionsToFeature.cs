using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260908070000_LinkCompanyPermissionsToFeature")]
public sealed class LinkCompanyPermissionsToFeature : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[RolePermissionFeatureLinks] ([FeatureId], [PermissionId])
            SELECT 16, permission.[Id]
            FROM [dbo].[Permissions] AS permission
            WHERE permission.[Name] IN ('Company.View', 'Company.Create', 'Company.Edit', 'Company.Delete')
              AND EXISTS (
                  SELECT 1
                  FROM [dbo].[NavigationFeatures] AS feature
                  WHERE feature.[Id] = 16 AND feature.[Key] = 'companies')
              AND NOT EXISTS (
                  SELECT 1
                  FROM [dbo].[RolePermissionFeatureLinks] AS link
                  WHERE link.[PermissionId] = permission.[Id]);
            """);

        migrationBuilder.Sql("""
            INSERT INTO [dbo].[NavigationFeaturePermissions] ([FeatureId], [PermissionId])
            SELECT 16, permission.[Id]
            FROM [dbo].[Permissions] AS permission
            WHERE permission.[Name] = 'Company.View'
              AND NOT EXISTS (
                  SELECT 1
                  FROM [dbo].[NavigationFeaturePermissions] AS link
                  WHERE link.[FeatureId] = 16 AND link.[PermissionId] = permission.[Id]);
            """);

        migrationBuilder.Sql("""
            INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
            SELECT role.[Id], permission.[Id]
            FROM [dbo].[Roles] AS role
            CROSS JOIN [dbo].[Permissions] AS permission
            WHERE role.[NormalizedName] = 'SYSTEM ADMINISTRATOR'
              AND permission.[Name] IN ('Company.View', 'Company.Create', 'Company.Edit', 'Company.Delete')
              AND NOT EXISTS (
                  SELECT 1
                  FROM [dbo].[RolePermissions] AS assigned
                  WHERE assigned.[RoleId] = role.[Id]
                    AND assigned.[PermissionId] = permission.[Id]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE link
            FROM [dbo].[RolePermissionFeatureLinks] AS link
            INNER JOIN [dbo].[Permissions] AS permission ON permission.[Id] = link.[PermissionId]
            WHERE link.[FeatureId] = 16
              AND permission.[Name] IN ('Company.View', 'Company.Create', 'Company.Edit', 'Company.Delete');
            """);
    }
}
