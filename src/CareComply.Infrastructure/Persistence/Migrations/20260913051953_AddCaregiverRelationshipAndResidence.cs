using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaregiverRelationshipAndResidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LivesWithClient",
                schema: "dbo",
                table: "ClientCaregivers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelationshipToClient",
                schema: "dbo",
                table: "ClientCaregivers",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LivesWithClient",
                schema: "dbo",
                table: "ClientCaregivers");

            migrationBuilder.DropColumn(
                name: "RelationshipToClient",
                schema: "dbo",
                table: "ClientCaregivers");
        }
    }
}
