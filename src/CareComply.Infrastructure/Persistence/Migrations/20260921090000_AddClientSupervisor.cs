using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260921090000_AddClientSupervisor")]
public sealed class AddClientSupervisor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<Guid>(
        name: "SupervisorUserId", schema: "dbo", table: "Clients", type: "uniqueidentifier", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "SupervisorUserId", schema: "dbo", table: "Clients");
}
