using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendEmployeeBasicInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowAggregator",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowManualEntry",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowOneTimeLogin",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowTravelTime",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowVisitScreen",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeTier",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvvEmail",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvvIvrAuthorization",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EvvOnly",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EvvPasswordProtected",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarding",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IvrPinProtected",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "OverrideRestriction",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RestrictDateAndTime",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TierEffectiveDate",
                schema: "dbo",
                table: "Users",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowAggregator",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AllowManualEntry",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AllowOneTimeLogin",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AllowTravelTime",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AllowVisitScreen",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmployeeTier",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EvvEmail",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EvvIvrAuthorization",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EvvOnly",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EvvPasswordProtected",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsOnboarding",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IvrPinProtected",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OverrideRestriction",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RestrictDateAndTime",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TierEffectiveDate",
                schema: "dbo",
                table: "Users");
        }
    }
}
