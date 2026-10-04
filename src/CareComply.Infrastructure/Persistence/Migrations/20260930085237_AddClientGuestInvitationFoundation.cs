using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // Generated EF migration uses constant metadata arrays.

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientGuestInvitationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientGuestInvitations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "varchar(320)", unicode: false, maxLength: 320, nullable: false),
                    MobileNumber = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    CreatedClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientCreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientGuestInvitations", x => x.Id);
                    table.CheckConstraint("CK_ClientGuestInvitations_ExpiresAfterCreation", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_ClientGuestInvitations_Clients_CreatedClientId",
                        column: x => x.CreatedClientId,
                        principalSchema: "dbo",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientGuestInvitations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientGuestInvitations_Users_CreatedByUserId",
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
                values: new object[] { 97, "Client.GuestInvite" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientGuestInvitations_CompanyId_ExpiresAtUtc",
                schema: "dbo",
                table: "ClientGuestInvitations",
                columns: new[] { "CompanyId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientGuestInvitations_CreatedByUserId",
                schema: "dbo",
                table: "ClientGuestInvitations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_ClientGuestInvitations_CreatedClientId",
                schema: "dbo",
                table: "ClientGuestInvitations",
                column: "CreatedClientId",
                unique: true,
                filter: "[CreatedClientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ClientGuestInvitations_TokenHash",
                schema: "dbo",
                table: "ClientGuestInvitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientGuestInvitations",
                schema: "dbo");

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 97);
        }
    }
}
