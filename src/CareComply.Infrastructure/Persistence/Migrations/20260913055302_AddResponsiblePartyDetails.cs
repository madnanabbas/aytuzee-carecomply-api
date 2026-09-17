using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResponsiblePartyDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "dbo",
                table: "ClientContacts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasSpenddown",
                schema: "dbo",
                table: "ClientContacts",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "dbo",
                table: "ClientContacts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "LivesWithClient",
                schema: "dbo",
                table: "ClientContacts",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                schema: "dbo",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "HasSpenddown",
                schema: "dbo",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "dbo",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "LivesWithClient",
                schema: "dbo",
                table: "ClientContacts");
        }
    }
}
