using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814, CA1861 // EF-generated migration seed arrays

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "dbo",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "dbo",
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dbo",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Permissions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Dashboard.View" },
                    { 2, "Client.View" },
                    { 3, "Client.Create" },
                    { 4, "Client.Edit" },
                    { 5, "Client.Archive" },
                    { 6, "ClientDocument.View" },
                    { 7, "ClientDocument.Upload" },
                    { 8, "ClientDocument.Download" },
                    { 9, "ClientDocument.Replace" },
                    { 10, "ClientDocument.Archive" },
                    { 11, "Employee.View" },
                    { 12, "Employee.Create" },
                    { 13, "Employee.Edit" },
                    { 14, "Employee.Archive" },
                    { 15, "EmployeeDocument.View" },
                    { 16, "EmployeeDocument.Upload" },
                    { 17, "EmployeeDocument.Download" },
                    { 18, "EmployeeDocument.Replace" },
                    { 19, "EmployeeDocument.Archive" },
                    { 20, "Training.View" },
                    { 21, "Training.Create" },
                    { 22, "Training.Edit" },
                    { 23, "Training.Manage" },
                    { 24, "TrainingDocument.View" },
                    { 25, "TrainingDocument.Upload" },
                    { 26, "BackgroundCheck.View" },
                    { 27, "BackgroundCheck.Create" },
                    { 28, "BackgroundCheck.Edit" },
                    { 29, "BackgroundCheck.Manage" },
                    { 30, "BackgroundCheckDocument.View" },
                    { 31, "BackgroundCheckDocument.Upload" },
                    { 32, "DHSLibrary.View" },
                    { 33, "DHSLibrary.Create" },
                    { 34, "DHSLibrary.Edit" },
                    { 35, "DHSLibrary.PublishVersion" },
                    { 36, "DHSLibrary.Archive" },
                    { 37, "ExpiryReview.View" },
                    { 38, "ExpiryReview.Manage" },
                    { 39, "Notification.View" },
                    { 40, "Notification.Manage" },
                    { 41, "FinancialReport.View" },
                    { 42, "User.View" },
                    { 43, "User.Manage" },
                    { 44, "Role.View" },
                    { 45, "Role.Manage" },
                    { 46, "Permission.View" },
                    { 47, "Permission.Manage" },
                    { 48, "AuditLog.View" }
                });

            migrationBuilder.CreateIndex(
                name: "UX_Permissions_Name",
                schema: "dbo",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                schema: "dbo",
                table: "RolePermissions",
                column: "PermissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "dbo");
        }
    }
}
