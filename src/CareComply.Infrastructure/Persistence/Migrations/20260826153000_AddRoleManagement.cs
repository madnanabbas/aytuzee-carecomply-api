using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260826153000_AddRoleManagement")]
public sealed class AddRoleManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RoleProfiles",
            schema: "dbo",
            columns: table => new
            {
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RoleProfiles", value => value.RoleId);
                table.ForeignKey(
                    name: "FK_RoleProfiles_Roles_RoleId",
                    column: value => value.RoleId,
                    principalSchema: "dbo",
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RolePermissionFeatureLinks",
            schema: "dbo",
            columns: table => new
            {
                FeatureId = table.Column<int>(type: "int", nullable: false),
                PermissionId = table.Column<int>(type: "int", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolePermissionFeatureLinks", value => new { value.FeatureId, value.PermissionId });
                table.ForeignKey(
                    name: "FK_RolePermissionFeatureLinks_NavigationFeatures_FeatureId",
                    column: value => value.FeatureId,
                    principalSchema: "dbo",
                    principalTable: "NavigationFeatures",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_RolePermissionFeatureLinks_Permissions_PermissionId",
                    column: value => value.PermissionId,
                    principalSchema: "dbo",
                    principalTable: "Permissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "UX_RolePermissionFeatureLinks_PermissionId",
            schema: "dbo",
            table: "RolePermissionFeatureLinks",
            column: "PermissionId",
            unique: true);

        migrationBuilder.Sql(
            """
            INSERT INTO [dbo].[RolePermissionFeatureLinks] ([FeatureId], [PermissionId]) VALUES
            (1,1),
            (2,2),(2,3),(2,4),(2,5),(2,6),(2,7),(2,8),(2,9),(2,10),
            (3,11),(3,12),(3,13),(3,14),(3,15),(3,16),(3,17),(3,18),(3,19),
            (7,20),(7,21),(7,22),(7,23),(7,24),(7,25),
            (8,26),(8,27),(8,28),(8,29),(8,30),(8,31),
            (4,32),(4,33),(4,34),(4,35),(4,36),
            (6,37),(6,38),
            (9,39),(9,40),
            (10,41),
            (12,42),(12,43),
            (13,44),(13,45),
            (14,46),(14,47),
            (15,48);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RolePermissionFeatureLinks", schema: "dbo");
        migrationBuilder.DropTable(name: "RoleProfiles", schema: "dbo");
    }
}
