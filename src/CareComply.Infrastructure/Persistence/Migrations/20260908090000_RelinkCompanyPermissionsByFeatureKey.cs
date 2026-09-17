using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260908090000_RelinkCompanyPermissionsByFeatureKey")]
public sealed class RelinkCompanyPermissionsByFeatureKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[RolePermissionFeatureLinks] ([FeatureId], [PermissionId])
            SELECT feature.[Id], permission.[Id]
            FROM [dbo].[NavigationFeatures] AS feature
            CROSS JOIN [dbo].[Permissions] AS permission
            WHERE feature.[Key] = 'companies'
              AND permission.[Name] IN ('Company.View', 'Company.Create', 'Company.Edit', 'Company.Delete')
              AND NOT EXISTS (
                  SELECT 1 FROM [dbo].[RolePermissionFeatureLinks] AS existing
                  WHERE existing.[PermissionId] = permission.[Id]);

            INSERT INTO [dbo].[NavigationFeaturePermissions] ([FeatureId], [PermissionId])
            SELECT feature.[Id], permission.[Id]
            FROM [dbo].[NavigationFeatures] AS feature
            CROSS JOIN [dbo].[Permissions] AS permission
            WHERE feature.[Key] = 'companies'
              AND permission.[Name] = 'Company.View'
              AND NOT EXISTS (
                  SELECT 1 FROM [dbo].[NavigationFeaturePermissions] AS existing
                  WHERE existing.[FeatureId] = feature.[Id] AND existing.[PermissionId] = permission.[Id]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE link
            FROM [dbo].[RolePermissionFeatureLinks] AS link
            INNER JOIN [dbo].[Permissions] AS permission ON permission.[Id] = link.[PermissionId]
            WHERE permission.[Name] IN ('Company.View', 'Company.Create', 'Company.Edit', 'Company.Delete');
            """);
    }
}
