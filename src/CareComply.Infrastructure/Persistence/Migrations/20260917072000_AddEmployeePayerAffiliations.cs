using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260917072000_AddEmployeePayerAffiliations")]
public sealed class AddEmployeePayerAffiliations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "InsuranceAffiliationRequired",
            schema: "dbo",
            table: "Users",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "PayerAffiliationsJson",
            schema: "dbo",
            table: "Users",
            type: "nvarchar(max)",
            maxLength: 16000,
            nullable: false,
            defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "InsuranceAffiliationRequired", schema: "dbo", table: "Users");
        migrationBuilder.DropColumn(name: "PayerAffiliationsJson", schema: "dbo", table: "Users");
    }
}
