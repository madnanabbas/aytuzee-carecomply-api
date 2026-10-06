using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // EF-generated migration column arrays are single-use call arguments.

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNavigationMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NavigationModules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NavigationModules", x => x.Id);
                    table.CheckConstraint("CK_NavigationModules_SortOrder", "[SortOrder] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "NavigationFeatures",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    ParentFeatureId = table.Column<int>(type: "int", nullable: true),
                    Key = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Route = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    IconKey = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NavigationFeatures", x => x.Id);
                    table.CheckConstraint("CK_NavigationFeatures_SortOrder", "[SortOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_NavigationFeatures_NavigationFeatures_ParentFeatureId",
                        column: x => x.ParentFeatureId,
                        principalSchema: "dbo",
                        principalTable: "NavigationFeatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NavigationFeatures_NavigationModules_ModuleId",
                        column: x => x.ModuleId,
                        principalSchema: "dbo",
                        principalTable: "NavigationModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NavigationFeaturePermissions",
                schema: "dbo",
                columns: table => new
                {
                    FeatureId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NavigationFeaturePermissions", x => new { x.FeatureId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_NavigationFeaturePermissions_NavigationFeatures_FeatureId",
                        column: x => x.FeatureId,
                        principalSchema: "dbo",
                        principalTable: "NavigationFeatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NavigationFeaturePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "dbo",
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NavigationModules",
                columns: new[] { "Id", "IsActive", "Key", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, true, "overview", "Overview", 10 },
                    { 2, true, "people", "People", 20 },
                    { 3, true, "compliance", "Compliance", 30 },
                    { 4, true, "operations", "Operations", 40 },
                    { 5, true, "administration", "Administration", 50 }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NavigationFeatures",
                columns: new[] { "Id", "IconKey", "IsActive", "Key", "ModuleId", "Name", "ParentFeatureId", "Route", "SortOrder" },
                values: new object[,]
                {
                    { 1, "dashboard", true, "dashboard", 1, "Dashboard", null, "/dashboard", 10 },
                    { 2, "clients", true, "clients", 2, "Clients", null, "/clients", 10 },
                    { 3, "employees", true, "employees", 2, "Employees", null, "/employees", 20 },
                    { 4, "dhs-library", true, "dhs-library", 3, "DHS Library", null, "/dhs-library", 10 },
                    { 5, "documents", true, "documents", 3, "Documents", null, "/documents", 20 },
                    { 6, "expiry-review", true, "expiry-review", 3, "Expiry & Review", null, "/expiry-review", 30 },
                    { 7, "training", true, "training", 3, "Training", null, "/training", 40 },
                    { 8, "background-checks", true, "background-checks", 3, "Background Checks", null, "/background-checks", 50 },
                    { 9, "notifications", true, "notifications", 4, "Notifications", null, "/notifications", 10 },
                    { 10, "reports", true, "reports", 4, "Reports", null, "/reports", 20 },
                    { 11, "access-management", true, "access-management", 5, "Access Management", null, "/access-control", 10 }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NavigationFeaturePermissions",
                columns: new[] { "FeatureId", "PermissionId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 2 },
                    { 3, 11 },
                    { 4, 32 },
                    { 5, 6 },
                    { 5, 15 },
                    { 5, 24 },
                    { 5, 30 },
                    { 6, 37 },
                    { 7, 20 },
                    { 8, 26 },
                    { 9, 39 },
                    { 10, 41 },
                    { 11, 42 },
                    { 11, 43 },
                    { 11, 44 },
                    { 11, 45 },
                    { 11, 46 },
                    { 11, 47 },
                    { 11, 48 }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NavigationFeatures",
                columns: new[] { "Id", "IconKey", "IsActive", "Key", "ModuleId", "Name", "ParentFeatureId", "Route", "SortOrder" },
                values: new object[,]
                {
                    { 12, "users", true, "users", 5, "Users", 11, "/access-control/users", 10 },
                    { 13, "roles", true, "roles", 5, "Roles", 11, "/access-control/roles", 20 },
                    { 14, "permissions", true, "permissions", 5, "Permissions", 11, "/access-control/permissions", 30 },
                    { 15, "audit-logs", true, "audit-logs", 5, "Audit Logs", 11, "/access-control/audit-logs", 40 }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NavigationFeaturePermissions",
                columns: new[] { "FeatureId", "PermissionId" },
                values: new object[,]
                {
                    { 12, 42 },
                    { 13, 44 },
                    { 14, 46 },
                    { 15, 48 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_NavigationFeaturePermissions_PermissionId",
                schema: "dbo",
                table: "NavigationFeaturePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_NavigationFeatures_Module_Parent_Active_Order",
                schema: "dbo",
                table: "NavigationFeatures",
                columns: new[] { "ModuleId", "ParentFeatureId", "IsActive", "SortOrder", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_NavigationFeatures_ParentFeatureId",
                schema: "dbo",
                table: "NavigationFeatures",
                column: "ParentFeatureId");

            migrationBuilder.CreateIndex(
                name: "UX_NavigationFeatures_Key",
                schema: "dbo",
                table: "NavigationFeatures",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_NavigationFeatures_Route",
                schema: "dbo",
                table: "NavigationFeatures",
                column: "Route",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NavigationModules_Active_Order",
                schema: "dbo",
                table: "NavigationModules",
                columns: new[] { "IsActive", "SortOrder", "Key" });

            migrationBuilder.CreateIndex(
                name: "UX_NavigationModules_Key",
                schema: "dbo",
                table: "NavigationModules",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NavigationFeaturePermissions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "NavigationFeatures",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "NavigationModules",
                schema: "dbo");
        }
    }
}
