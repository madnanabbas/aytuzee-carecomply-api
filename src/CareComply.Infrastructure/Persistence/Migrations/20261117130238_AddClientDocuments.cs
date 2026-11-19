using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration column arrays are single-use arguments.

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientDocuments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientDocuments", x => x.Id);
                    table.CheckConstraint("CK_ClientDocuments_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_ClientDocuments_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "dbo",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientDocuments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientDocuments_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientDocumentVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    StorageObjectKey = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientDocumentVersions", x => x.Id);
                    table.CheckConstraint("CK_ClientDocumentVersions_VersionNumber", "[VersionNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_ClientDocumentVersions_ClientDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "ClientDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientDocumentVersions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "dbo",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientDocumentVersions_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_ClientId_Status_Category",
                schema: "dbo",
                table: "ClientDocuments",
                columns: new[] { "ClientId", "Status", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_CompanyId_ClientId",
                schema: "dbo",
                table: "ClientDocuments",
                columns: new[] { "CompanyId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_CreatedByUserId",
                schema: "dbo",
                table: "ClientDocuments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_CurrentVersionId",
                schema: "dbo",
                table: "ClientDocuments",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_UpdatedByUserId",
                schema: "dbo",
                table: "ClientDocuments",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocumentVersions_Sha256",
                schema: "dbo",
                table: "ClientDocumentVersions",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocumentVersions_UploadedByUserId",
                schema: "dbo",
                table: "ClientDocumentVersions",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_ClientDocumentVersions_ClientId_Sha256",
                schema: "dbo",
                table: "ClientDocumentVersions",
                columns: new[] { "ClientId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClientDocumentVersions_DocumentId_Current",
                schema: "dbo",
                table: "ClientDocumentVersions",
                column: "DocumentId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_ClientDocumentVersions_DocumentId_VersionNumber",
                schema: "dbo",
                table: "ClientDocumentVersions",
                columns: new[] { "DocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClientDocumentVersions_StorageObjectKey",
                schema: "dbo",
                table: "ClientDocumentVersions",
                column: "StorageObjectKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientDocumentVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ClientDocuments",
                schema: "dbo");
        }
    }
}
