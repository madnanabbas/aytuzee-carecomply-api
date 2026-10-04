using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientGuestOneTimeSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RedeemedAtUtc",
                schema: "dbo",
                table: "ClientGuestInvitations",
                type: "datetimeoffset(0)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientGuestSessions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientGuestInvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionTokenHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CsrfTokenHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    LastActivityAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientGuestSessions", x => x.Id);
                    table.CheckConstraint("CK_ClientGuestSessions_ExpiresAfterCreation", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_ClientGuestSessions_ClientGuestInvitations_InvitationId",
                        column: x => x.ClientGuestInvitationId,
                        principalSchema: "dbo",
                        principalTable: "ClientGuestInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ClientGuestSessions_InvitationId",
                schema: "dbo",
                table: "ClientGuestSessions",
                column: "ClientGuestInvitationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClientGuestSessions_SessionTokenHash",
                schema: "dbo",
                table: "ClientGuestSessions",
                column: "SessionTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientGuestSessions",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "RedeemedAtUtc",
                schema: "dbo",
                table: "ClientGuestInvitations");
        }
    }
}
