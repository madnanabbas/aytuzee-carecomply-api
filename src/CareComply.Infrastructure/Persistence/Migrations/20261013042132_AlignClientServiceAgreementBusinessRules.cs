using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignClientServiceAgreementBusinessRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [dbo].[ClientServiceAgreements]
                    WHERE NULLIF(LTRIM(RTRIM([Status])), '') IS NOT NULL
                      AND LOWER(REPLACE(REPLACE(LTRIM(RTRIM([Status])), ' ', ''), '-', '')) NOT IN
                          ('draft', 'pendingauthorization', 'active', 'onhold', 'expired', 'terminated', 'cancelled')
                )
                    THROW 51000, 'Cannot migrate ClientServiceAgreements: an unsupported Status value exists.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [dbo].[ClientServiceAgreementLines]
                    WHERE NULLIF(LTRIM(RTRIM([UnitType])), '') IS NOT NULL
                      AND LOWER(REPLACE(REPLACE(LTRIM(RTRIM([UnitType])), ' ', ''), '-', '')) NOT IN
                          ('fifteenminutes', '15minutes', 'hour', 'hours', 'visit', 'visits', 'day', 'days',
                           'perdiem', 'serviceunit', 'dollaramount')
                )
                    THROW 51001, 'Cannot migrate ClientServiceAgreementLines: an unsupported UnitType value exists.', 1;

                UPDATE [dbo].[ClientServiceAgreements]
                SET [Status] = CASE LOWER(REPLACE(REPLACE(LTRIM(RTRIM([Status])), ' ', ''), '-', ''))
                    WHEN 'draft' THEN 'Draft'
                    WHEN 'pendingauthorization' THEN 'PendingAuthorization'
                    WHEN 'active' THEN 'Active'
                    WHEN 'onhold' THEN 'OnHold'
                    WHEN 'expired' THEN 'Expired'
                    WHEN 'terminated' THEN 'Terminated'
                    WHEN 'cancelled' THEN 'Cancelled'
                    ELSE NULL
                END;

                UPDATE [dbo].[ClientServiceAgreementLines]
                SET [UnitType] = CASE LOWER(REPLACE(REPLACE(LTRIM(RTRIM([UnitType])), ' ', ''), '-', ''))
                    WHEN 'fifteenminutes' THEN 'FifteenMinutes'
                    WHEN '15minutes' THEN 'FifteenMinutes'
                    WHEN 'hour' THEN 'Hour'
                    WHEN 'hours' THEN 'Hour'
                    WHEN 'visit' THEN 'Visit'
                    WHEN 'visits' THEN 'Visit'
                    WHEN 'day' THEN 'Day'
                    WHEN 'days' THEN 'Day'
                    WHEN 'perdiem' THEN 'PerDiem'
                    WHEN 'serviceunit' THEN 'ServiceUnit'
                    WHEN 'dollaramount' THEN 'DollarAmount'
                    ELSE NULL
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "ClientServiceAgreements",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDummy",
                schema: "dbo",
                table: "ClientServiceAgreements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                schema: "dbo",
                table: "ClientServiceAgreements",
                type: "varchar(16)",
                unicode: false,
                maxLength: 16,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UnitType",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AuthorizedBudgetAmount",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RateEffectiveFrom",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RateEffectiveTo",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE agreementLine
                SET agreementLine.[StartDate] = agreement.[StartDate]
                FROM [dbo].[ClientServiceAgreementLines] agreementLine
                INNER JOIN [dbo].[ClientServiceAgreements] agreement
                    ON agreement.[Id] = agreementLine.[ClientServiceAgreementId];
                """);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "StartDate",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ClientServiceAgreementLineDiagnoses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientServiceAgreementLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiagnosisText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IcdCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientServiceAgreementLineDiagnoses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientServiceAgreementLineDiagnoses_ClientServiceAgreementLines_ClientServiceAgreementLineId",
                        column: x => x.ClientServiceAgreementLineId,
                        principalSchema: "dbo",
                        principalTable: "ClientServiceAgreementLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [dbo].[ClientServiceAgreementLineDiagnoses]
                    ([Id], [ClientServiceAgreementLineId], [DiagnosisText], [IcdCode], [IsPrimary], [CreatedAtUtc], [UpdatedAtUtc])
                SELECT NEWID(), line.[Id],
                       NULLIF(LTRIM(RTRIM(line.[PrimaryDiagnosisText])), ''),
                       NULLIF(LTRIM(RTRIM(line.[IcdCode])), ''),
                       CAST(1 AS bit), line.[CreatedAtUtc], line.[UpdatedAtUtc]
                FROM [dbo].[ClientServiceAgreementLines] line
                WHERE NULLIF(LTRIM(RTRIM(line.[PrimaryDiagnosisText])), '') IS NOT NULL
                   OR NULLIF(LTRIM(RTRIM(line.[IcdCode])), '') IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "IcdCode",
                schema: "dbo",
                table: "ClientServiceAgreementLines");

            migrationBuilder.DropColumn(
                name: "PrimaryDiagnosisText",
                schema: "dbo",
                table: "ClientServiceAgreementLines");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClientServiceAgreements_Priority",
                schema: "dbo",
                table: "ClientServiceAgreements",
                sql: "[Priority] IS NULL OR [Priority] IN ('Normal', 'High', 'Urgent')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClientServiceAgreements_Status",
                schema: "dbo",
                table: "ClientServiceAgreements",
                sql: "[Status] IS NULL OR [Status] IN ('Draft', 'PendingAuthorization', 'Active', 'OnHold', 'Expired', 'Terminated', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClientServiceAgreementLines_AuthorizedBudgetAmount",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                sql: "[AuthorizedBudgetAmount] IS NULL OR [AuthorizedBudgetAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClientServiceAgreementLines_Dates",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                sql: "[EndDate] IS NULL OR [EndDate] >= [StartDate]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClientServiceAgreementLines_RateEffectiveDates",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                sql: "[RateEffectiveFrom] IS NULL OR [RateEffectiveTo] IS NULL OR [RateEffectiveTo] >= [RateEffectiveFrom]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClientServiceAgreementLines_UnitType",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                sql: "[UnitType] IS NULL OR [UnitType] IN ('FifteenMinutes', 'Hour', 'Visit', 'Day', 'PerDiem', 'ServiceUnit', 'DollarAmount')");

            migrationBuilder.CreateIndex(
                name: "UX_ClientServiceAgreementLineDiagnoses_OnePrimaryPerLine",
                schema: "dbo",
                table: "ClientServiceAgreementLineDiagnoses",
                column: "ClientServiceAgreementLineId",
                unique: true,
                filter: "[IsPrimary] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IcdCode",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryDiagnosisText",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE agreementLine
                SET agreementLine.[PrimaryDiagnosisText] = diagnosis.[DiagnosisText],
                    agreementLine.[IcdCode] = diagnosis.[IcdCode]
                FROM [dbo].[ClientServiceAgreementLines] agreementLine
                INNER JOIN [dbo].[ClientServiceAgreementLineDiagnoses] diagnosis
                    ON diagnosis.[ClientServiceAgreementLineId] = agreementLine.[Id]
                   AND diagnosis.[IsPrimary] = 1;
                """);

            migrationBuilder.DropTable(
                name: "ClientServiceAgreementLineDiagnoses",
                schema: "dbo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClientServiceAgreements_Priority",
                schema: "dbo",
                table: "ClientServiceAgreements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClientServiceAgreements_Status",
                schema: "dbo",
                table: "ClientServiceAgreements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClientServiceAgreementLines_AuthorizedBudgetAmount",
                schema: "dbo",
                table: "ClientServiceAgreementLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClientServiceAgreementLines_Dates",
                schema: "dbo",
                table: "ClientServiceAgreementLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClientServiceAgreementLines_RateEffectiveDates",
                schema: "dbo",
                table: "ClientServiceAgreementLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClientServiceAgreementLines_UnitType",
                schema: "dbo",
                table: "ClientServiceAgreementLines");

            migrationBuilder.DropColumn(
                name: "IsDummy",
                schema: "dbo",
                table: "ClientServiceAgreements");

            migrationBuilder.DropColumn(
                name: "Priority",
                schema: "dbo",
                table: "ClientServiceAgreements");

            migrationBuilder.DropColumn(
                name: "AuthorizedBudgetAmount",
                schema: "dbo",
                table: "ClientServiceAgreementLines");

            migrationBuilder.DropColumn(
                name: "EndDate",
                schema: "dbo",
                table: "ClientServiceAgreementLines");

            migrationBuilder.DropColumn(
                name: "RateEffectiveFrom",
                schema: "dbo",
                table: "ClientServiceAgreementLines");

            migrationBuilder.DropColumn(
                name: "RateEffectiveTo",
                schema: "dbo",
                table: "ClientServiceAgreementLines");

            migrationBuilder.DropColumn(
                name: "StartDate",
                schema: "dbo",
                table: "ClientServiceAgreementLines");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "ClientServiceAgreements",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldUnicode: false,
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UnitType",
                schema: "dbo",
                table: "ClientServiceAgreementLines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldUnicode: false,
                oldMaxLength: 32,
                oldNullable: true);

        }
    }
}
