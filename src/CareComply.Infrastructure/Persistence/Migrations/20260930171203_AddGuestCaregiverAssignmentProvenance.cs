using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestCaregiverAssignmentProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                schema: "dbo",
                table: "ClientCaregivers",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByGuestInvitationId",
                schema: "dbo",
                table: "ClientCaregivers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientCaregivers_CreatedByGuestInvitationId",
                schema: "dbo",
                table: "ClientCaregivers",
                column: "CreatedByGuestInvitationId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClientCaregivers_Creator",
                schema: "dbo",
                table: "ClientCaregivers",
                sql: "([CreatedByUserId] IS NULL AND [CreatedByGuestInvitationId] IS NOT NULL) OR ([CreatedByUserId] IS NOT NULL AND [CreatedByGuestInvitationId] IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientCaregivers_ClientGuestInvitations_CreatedByGuestInvitationId",
                schema: "dbo",
                table: "ClientCaregivers",
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
                name: "FK_ClientCaregivers_ClientGuestInvitations_CreatedByGuestInvitationId",
                schema: "dbo",
                table: "ClientCaregivers");

            migrationBuilder.DropIndex(
                name: "IX_ClientCaregivers_CreatedByGuestInvitationId",
                schema: "dbo",
                table: "ClientCaregivers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClientCaregivers_Creator",
                schema: "dbo",
                table: "ClientCaregivers");

            migrationBuilder.DropColumn(
                name: "CreatedByGuestInvitationId",
                schema: "dbo",
                table: "ClientCaregivers");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                schema: "dbo",
                table: "ClientCaregivers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
