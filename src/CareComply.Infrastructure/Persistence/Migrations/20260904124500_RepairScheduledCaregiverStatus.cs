using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260904124500_RepairScheduledCaregiverStatus")]
public sealed class RepairScheduledCaregiverStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "UPDATE [dbo].[ClientCaregivers] SET [Status] = 'Active' WHERE [Status] = 'Ended' AND [EndDate] >= CAST(SYSUTCDATETIME() AS date);");

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
