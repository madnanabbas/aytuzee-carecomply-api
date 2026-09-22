using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260921100000_RepairEmployeeNavigation")]
public sealed class RepairEmployeeNavigation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DECLARE @peopleId int = (SELECT Id FROM dbo.NavigationModules WHERE [Key] = 'people');
        DECLARE @employeePermissionId int = (SELECT Id FROM dbo.Permissions WHERE [Name] = 'Employee.View');

        IF @peopleId IS NOT NULL AND @employeePermissionId IS NOT NULL
        BEGIN
            UPDATE dbo.NavigationModules SET IsActive = 1 WHERE Id = @peopleId;

            IF NOT EXISTS (SELECT 1 FROM dbo.NavigationFeatures WHERE [Key] = 'employees')
                INSERT INTO dbo.NavigationFeatures (Id, ModuleId, ParentFeatureId, [Key], [Name], [Route], IconKey, SortOrder, IsActive)
                SELECT ISNULL(MAX(Id), 0) + 1, @peopleId, NULL, 'employees', 'Employees', '/employees', 'employees', 20, 1
                FROM dbo.NavigationFeatures;

            UPDATE dbo.NavigationFeatures
            SET ModuleId = @peopleId, ParentFeatureId = NULL, [Name] = 'Employees',
                [Route] = '/employees', IconKey = 'employees', IsActive = 1
            WHERE [Key] = 'employees';

            DECLARE @employeesId int = (SELECT Id FROM dbo.NavigationFeatures WHERE [Key] = 'employees');
            IF NOT EXISTS (
                SELECT 1 FROM dbo.NavigationFeaturePermissions
                WHERE FeatureId = @employeesId AND PermissionId = @employeePermissionId)
                INSERT INTO dbo.NavigationFeaturePermissions (FeatureId, PermissionId)
                VALUES (@employeesId, @employeePermissionId);
        END
        """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve navigation configuration; the original seed owns these records.
    }
}
