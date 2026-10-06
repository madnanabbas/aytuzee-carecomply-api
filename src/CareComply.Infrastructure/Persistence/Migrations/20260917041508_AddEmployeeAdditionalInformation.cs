using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeAdditionalInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvailableDaysCsv",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "AvailableEndTime",
                schema: "dbo",
                table: "Users",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "AvailableStartTime",
                schema: "dbo",
                table: "Users",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BirthPlace",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "CanDriveForWork",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanWorkWithPets",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanWorkWithSmokers",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CareProgram",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CfssCertificationNumber",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClientTypesCsv",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DemographicDescription",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ethnicity",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EyeColor",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HairColor",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasOtherJob",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasUsedDifferentName",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasWorkingHourRestrictions",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "HeightInches",
                schema: "dbo",
                table: "Users",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMilitaryVeteran",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnionMember",
                schema: "dbo",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MaritalStatus",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MilitaryServiceExplanation",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OtherEmployerName",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OtherEmploymentDuration",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PetRestrictionExplanation",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreviousAddressesJson",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "PreviousName",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreviousWorkExperience",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "QualificationsCsv",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Race",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SmokerRestrictionExplanation",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPounds",
                schema: "dbo",
                table: "Users",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableDaysCsv",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AvailableEndTime",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AvailableStartTime",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BirthPlace",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanDriveForWork",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanWorkWithPets",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanWorkWithSmokers",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CareProgram",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CfssCertificationNumber",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClientTypesCsv",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DemographicDescription",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Ethnicity",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EyeColor",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HairColor",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HasOtherJob",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HasUsedDifferentName",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HasWorkingHourRestrictions",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HeightInches",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsMilitaryVeteran",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsUnionMember",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MilitaryServiceExplanation",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OtherEmployerName",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OtherEmploymentDuration",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PetRestrictionExplanation",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreviousAddressesJson",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreviousName",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreviousWorkExperience",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "QualificationsCsv",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Race",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SmokerRestrictionExplanation",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WeightPounds",
                schema: "dbo",
                table: "Users");
        }
    }
}
