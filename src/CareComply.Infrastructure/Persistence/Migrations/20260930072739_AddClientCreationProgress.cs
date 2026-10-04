using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientCreationProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreationProgressStep",
                schema: "dbo",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clients_CreationProgressStep",
                schema: "dbo",
                table: "Clients",
                sql: "[CreationProgressStep] BETWEEN 0 AND 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Clients_CreationProgressStep",
                schema: "dbo",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CreationProgressStep",
                schema: "dbo",
                table: "Clients");
        }
    }
}
