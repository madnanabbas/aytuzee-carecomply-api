using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // EF-generated migration arguments are intentionally local arrays.

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientTodos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientTodos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientTodos", x => x.Id);
                    table.CheckConstraint("CK_ClientTodos_Completion", "([Status] = 3 AND [CompletedByUserId] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL) OR ([Status] <> 3 AND [CompletedByUserId] IS NULL AND [CompletedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_ClientTodos_Priority", "[Priority] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_ClientTodos_Status", "[Status] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_ClientTodos_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "dbo",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientTodos_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientTodos_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientTodos_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Permissions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 50, "ClientTodo.View" },
                    { 51, "ClientTodo.Create" },
                    { 52, "ClientTodo.Edit" },
                    { 53, "ClientTodo.Complete" }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "RolePermissionFeatureLinks",
                columns: new[] { "FeatureId", "PermissionId" },
                values: new object[,]
                {
                    { 2, 50 },
                    { 2, 51 },
                    { 2, 52 },
                    { 2, 53 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientTodos_AssignedToUserId_Status",
                schema: "dbo",
                table: "ClientTodos",
                columns: new[] { "AssignedToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientTodos_ClientId_Status",
                schema: "dbo",
                table: "ClientTodos",
                columns: new[] { "ClientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientTodos_CompletedByUserId",
                schema: "dbo",
                table: "ClientTodos",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTodos_CreatedByUserId",
                schema: "dbo",
                table: "ClientTodos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTodos_DueDate_Status",
                schema: "dbo",
                table: "ClientTodos",
                columns: new[] { "DueDate", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientTodos",
                schema: "dbo");

            migrationBuilder.DeleteData(schema: "dbo", table: "RolePermissionFeatureLinks", keyColumns: new[] { "FeatureId", "PermissionId" }, keyValues: new object[] { 2, 50 });
            migrationBuilder.DeleteData(schema: "dbo", table: "RolePermissionFeatureLinks", keyColumns: new[] { "FeatureId", "PermissionId" }, keyValues: new object[] { 2, 51 });
            migrationBuilder.DeleteData(schema: "dbo", table: "RolePermissionFeatureLinks", keyColumns: new[] { "FeatureId", "PermissionId" }, keyValues: new object[] { 2, 52 });
            migrationBuilder.DeleteData(schema: "dbo", table: "RolePermissionFeatureLinks", keyColumns: new[] { "FeatureId", "PermissionId" }, keyValues: new object[] { 2, 53 });

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 53);
        }
    }
}
