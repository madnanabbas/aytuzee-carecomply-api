using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260904114035_AddEmployeeWorkforceMetadata")]
public partial class AddEmployeeWorkforceMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsEmployee", schema: "dbo", table: "Users", type: "bit",
            nullable: false, defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "JobTitle", schema: "dbo", table: "Users", type: "nvarchar(100)",
            maxLength: 100, nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsEmployee", schema: "dbo", table: "Users");
        migrationBuilder.DropColumn(name: "JobTitle", schema: "dbo", table: "Users");
    }
}
