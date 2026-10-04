using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260930120000_LinkClientGuestInviteToClientFeature")]
public sealed class LinkClientGuestInviteToClientFeature : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF NOT EXISTS (
                SELECT 1
                FROM [dbo].[RolePermissionFeatureLinks]
                WHERE [FeatureId] = 2 AND [PermissionId] = 97)
            BEGIN
                INSERT INTO [dbo].[RolePermissionFeatureLinks] ([FeatureId], [PermissionId])
                VALUES (2, 97);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM [dbo].[RolePermissionFeatureLinks]
            WHERE [FeatureId] = 2 AND [PermissionId] = 97;
            """);
    }
}
