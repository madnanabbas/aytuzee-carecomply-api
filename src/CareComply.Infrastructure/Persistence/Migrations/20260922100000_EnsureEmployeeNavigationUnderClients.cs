using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CareComplyDbContext))]
[Migration("20260922100000_EnsureEmployeeNavigationUnderClients")]
public sealed class EnsureEmployeeNavigationUnderClients : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DECLARE @moduleId int = (
            SELECT TOP (1) ModuleId FROM dbo.NavigationFeatures
            WHERE [Key] = 'clients' AND ParentFeatureId IS NULL);
        IF @moduleId IS NULL
            SELECT @moduleId = Id FROM dbo.NavigationModules WHERE [Key] = 'people';

        DECLARE @permissionId int = (
            SELECT Id FROM dbo.Permissions WHERE [Name] = 'Employee.View');

        IF @moduleId IS NOT NULL AND @permissionId IS NOT NULL
        BEGIN
            UPDATE dbo.NavigationModules SET IsActive = 1 WHERE Id = @moduleId;

            IF NOT EXISTS (SELECT 1 FROM dbo.NavigationFeatures WHERE [Key] = 'employees')
                INSERT INTO dbo.NavigationFeatures
                    (Id, ModuleId, ParentFeatureId, [Key], [Name], [Route], IconKey, SortOrder, IsActive)
                SELECT ISNULL(MAX(Id), 0) + 1, @moduleId, NULL,
                    'employees', 'Employees', '/employees', 'employees', 20, 1
                FROM dbo.NavigationFeatures;

            UPDATE dbo.NavigationFeatures
            SET ModuleId = @moduleId, ParentFeatureId = NULL, [Name] = 'Employees',
                [Route] = '/employees', IconKey = 'employees', IsActive = 1
            WHERE [Key] = 'employees';

            DECLARE @featureId int = (
                SELECT Id FROM dbo.NavigationFeatures WHERE [Key] = 'employees');
            IF NOT EXISTS (
                SELECT 1 FROM dbo.NavigationFeaturePermissions
                WHERE FeatureId = @featureId AND PermissionId = @permissionId)
                INSERT INTO dbo.NavigationFeaturePermissions (FeatureId, PermissionId)
                VALUES (@featureId, @permissionId);
        END
        """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This is a data repair. Do not delete an administrator-configured feature on rollback.
    }
}
