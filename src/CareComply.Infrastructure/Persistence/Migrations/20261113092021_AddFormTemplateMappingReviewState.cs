using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormTemplateMappingReviewState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MappingsReviewedAtUtc",
                schema: "dbo",
                table: "FormTemplateVersions",
                type: "datetimeoffset(0)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MappingsReviewedByUserId",
                schema: "dbo",
                table: "FormTemplateVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresWarningAcknowledgement",
                schema: "dbo",
                table: "FormTemplateVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplateVersions_MappingsReviewedByUserId",
                schema: "dbo",
                table: "FormTemplateVersions",
                column: "MappingsReviewedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FormTemplateVersions_MappingReview",
                schema: "dbo",
                table: "FormTemplateVersions",
                sql: "([MappingsReviewedByUserId] IS NULL AND [MappingsReviewedAtUtc] IS NULL) OR ([MappingsReviewedByUserId] IS NOT NULL AND [MappingsReviewedAtUtc] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_FormTemplateVersions_Users_MappingsReviewedByUserId",
                schema: "dbo",
                table: "FormTemplateVersions",
                column: "MappingsReviewedByUserId",
                principalSchema: "dbo",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormTemplateVersions_Users_MappingsReviewedByUserId",
                schema: "dbo",
                table: "FormTemplateVersions");

            migrationBuilder.DropIndex(
                name: "IX_FormTemplateVersions_MappingsReviewedByUserId",
                schema: "dbo",
                table: "FormTemplateVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FormTemplateVersions_MappingReview",
                schema: "dbo",
                table: "FormTemplateVersions");

            migrationBuilder.DropColumn(
                name: "MappingsReviewedAtUtc",
                schema: "dbo",
                table: "FormTemplateVersions");

            migrationBuilder.DropColumn(
                name: "MappingsReviewedByUserId",
                schema: "dbo",
                table: "FormTemplateVersions");

            migrationBuilder.DropColumn(
                name: "RequiresWarningAcknowledgement",
                schema: "dbo",
                table: "FormTemplateVersions");
        }
    }
}
