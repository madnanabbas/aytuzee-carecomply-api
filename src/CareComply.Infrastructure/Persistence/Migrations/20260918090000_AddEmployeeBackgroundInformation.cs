using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260918090000_AddEmployeeBackgroundInformation")]
public sealed class AddEmployeeBackgroundInformation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EmployeeBackgroundInformationJson",
            schema: "dbo",
            table: "Users",
            type: "nvarchar(max)",
            maxLength: 16000,
            nullable: false,
            defaultValue: "{}");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EmployeeBackgroundInformationJson",
            schema: "dbo",
            table: "Users");
    }
}
