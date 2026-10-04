using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestHospitalizationProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                schema: "dbo",
                table: "ClientHospitalizations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByGuestInvitationId",
                schema: "dbo",
                table: "ClientHospitalizations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientHospitalizations_CreatedByGuestInvitationId",
                schema: "dbo",
                table: "ClientHospitalizations",
                column: "CreatedByGuestInvitationId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClientHospitalizations_Creator",
                schema: "dbo",
                table: "ClientHospitalizations",
                sql: "([CreatedByUserId] IS NULL AND [CreatedByGuestInvitationId] IS NOT NULL) OR ([CreatedByUserId] IS NOT NULL AND [CreatedByGuestInvitationId] IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientHospitalizations_ClientGuestInvitations_CreatedByGuestInvitationId",
                schema: "dbo",
                table: "ClientHospitalizations",
                column: "CreatedByGuestInvitationId",
                principalSchema: "dbo",
                principalTable: "ClientGuestInvitations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientHospitalizations_ClientGuestInvitations_CreatedByGuestInvitationId",
                schema: "dbo",
                table: "ClientHospitalizations");

            migrationBuilder.DropIndex(
                name: "IX_ClientHospitalizations_CreatedByGuestInvitationId",
                schema: "dbo",
                table: "ClientHospitalizations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClientHospitalizations_Creator",
                schema: "dbo",
                table: "ClientHospitalizations");

            migrationBuilder.DropColumn(
                name: "CreatedByGuestInvitationId",
                schema: "dbo",
                table: "ClientHospitalizations");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                schema: "dbo",
                table: "ClientHospitalizations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
