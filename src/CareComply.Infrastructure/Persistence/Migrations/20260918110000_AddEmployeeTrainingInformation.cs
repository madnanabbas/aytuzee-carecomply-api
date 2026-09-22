using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260918110000_AddEmployeeTrainingInformation")]
public sealed class AddEmployeeTrainingInformation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(
        name: "EmployeeTrainingInformationJson", schema: "dbo", table: "Users", type: "nvarchar(max)",
        maxLength: 32000, nullable: false, defaultValue: "{}");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "EmployeeTrainingInformationJson", schema: "dbo", table: "Users");
}
