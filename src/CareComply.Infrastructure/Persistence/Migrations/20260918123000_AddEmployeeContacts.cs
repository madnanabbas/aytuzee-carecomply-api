using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260918123000_AddEmployeeContacts")]
public sealed class AddEmployeeContacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(
        name: "EmployeeContactsJson", schema: "dbo", table: "Users", type: "nvarchar(max)",
        maxLength: 16000, nullable: false, defaultValue: "[]");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "EmployeeContactsJson", schema: "dbo", table: "Users");
}
