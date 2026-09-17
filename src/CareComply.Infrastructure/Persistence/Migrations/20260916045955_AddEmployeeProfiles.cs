using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Apartment",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "County",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                schema: "dbo",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DriverLicenseExpiresOn",
                schema: "dbo",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactAddress",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactEmail",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactName",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactPhone",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactRelationship",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeNotes",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "HireDate",
                schema: "dbo",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HoursPerMonth",
                schema: "dbo",
                table: "Users",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InactiveDate",
                schema: "dbo",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LanguagesCsv",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LicenseId",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Npi",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "PayerEffectiveFrom",
                schema: "dbo",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PayerEffectiveTo",
                schema: "dbo",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayerGroupNumber",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PayerMemberId",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PayerName",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PayerPhone",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PayerPlanName",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreferredName",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecondaryJobTitle",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecondaryPhone",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SsnProtected",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartOfServiceDate",
                schema: "dbo",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StreetAddress",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SupervisorUserId",
                schema: "dbo",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingCertificateNumber",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "TrainingCompletedOn",
                schema: "dbo",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TrainingExpiresOn",
                schema: "dbo",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingName",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TrainingProvider",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Umpi",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Apartment",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "County",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DriverLicenseExpiresOn",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmergencyContactAddress",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmergencyContactEmail",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmergencyContactName",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmergencyContactPhone",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmergencyContactRelationship",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmployeeNotes",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Gender",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HireDate",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HoursPerMonth",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InactiveDate",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LanguagesCsv",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LicenseId",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Npi",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PayerEffectiveFrom",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PayerEffectiveTo",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PayerGroupNumber",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PayerMemberId",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PayerName",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PayerPhone",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PayerPlanName",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreferredName",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecondaryJobTitle",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecondaryPhone",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SsnProtected",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StartOfServiceDate",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StreetAddress",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SupervisorUserId",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrainingCertificateNumber",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrainingCompletedOn",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrainingExpiresOn",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrainingName",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrainingProvider",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Umpi",
                schema: "dbo",
                table: "Users");
        }
    }
}
