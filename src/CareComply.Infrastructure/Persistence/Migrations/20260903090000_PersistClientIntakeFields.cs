using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260903090000_PersistClientIntakeFields")]
public sealed class PersistClientIntakeFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Country",
            schema: "dbo",
            table: "Clients",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "AdditionalInformation",
            schema: "dbo",
            table: "ClientAdditionalProfiles",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Country", schema: "dbo", table: "Clients");
        migrationBuilder.DropColumn(name: "AdditionalInformation", schema: "dbo", table: "ClientAdditionalProfiles");
    }
}
