using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // EF migration APIs require generated array arguments

namespace CareComply.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormTemplateFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormTemplates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OwnerType = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormTemplates", x => x.Id);
                    table.CheckConstraint("CK_FormTemplates_Owner", "([OwnerType] = 'Platform' AND [CompanyId] IS NULL) OR ([OwnerType] = 'Company' AND [CompanyId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_FormTemplates_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "dbo",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormTemplates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormTemplateVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StorageObjectKey = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: false),
                    FormTechnology = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormTemplateVersions", x => x.Id);
                    table.CheckConstraint("CK_FormTemplateVersions_EffectiveDates", "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_FormTemplateVersions_FormTechnology", "[FormTechnology] IN ('AcroForm', 'HybridXfaAcroForm', 'Unsupported')");
                    table.CheckConstraint("CK_FormTemplateVersions_PdfContent", "[ContentType] = 'application/pdf' AND LEN([StorageObjectKey]) = 32 AND [StorageObjectKey] NOT LIKE '%[^0-9a-f]%' AND LEN([Sha256]) = 64 AND [Sha256] NOT LIKE '%[^0-9a-f]%' ");
                    table.CheckConstraint("CK_FormTemplateVersions_PositiveValues", "[VersionNumber] > 0 AND [SizeBytes] > 0 AND [PageCount] > 0");
                    table.CheckConstraint("CK_FormTemplateVersions_PublishedState", "([Status] = 'Draft' AND [PublishedByUserId] IS NULL AND [PublishedAtUtc] IS NULL) OR ([Status] IN ('Published', 'Retired') AND [PublishedByUserId] IS NOT NULL AND [PublishedAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_FormTemplateVersions_FormTemplates_FormTemplateId",
                        column: x => x.FormTemplateId,
                        principalSchema: "dbo",
                        principalTable: "FormTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormTemplateVersions_Users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormTemplateVersions_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormTemplateFieldMappings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormTemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PdfFieldName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PdfFieldType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    FieldPurpose = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    BindingKey = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    FormatKey = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    IsUserEditable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormTemplateFieldMappings", x => x.Id);
                    table.CheckConstraint("CK_FormTemplateFieldMappings_Binding", "([FieldPurpose] = 'Data' AND [BindingKey] IS NOT NULL) OR ([FieldPurpose] IN ('Manual', 'Signature', 'Attestation') AND [BindingKey] IS NULL)");
                    table.CheckConstraint("CK_FormTemplateFieldMappings_PdfFieldType", "[PdfFieldType] IN ('Text', 'CheckBox', 'RadioButton', 'Choice', 'Button', 'Signature', 'Unknown')");
                    table.ForeignKey(
                        name: "FK_FormTemplateFieldMappings_FormTemplateVersions_FormTemplateVersionId",
                        column: x => x.FormTemplateVersionId,
                        principalSchema: "dbo",
                        principalTable: "FormTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Permissions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 104, "FormTemplate.View" },
                    { 105, "FormTemplate.Manage" },
                    { 106, "ClientForm.Export" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplateFieldMappings_BindingKey",
                schema: "dbo",
                table: "FormTemplateFieldMappings",
                column: "BindingKey");

            migrationBuilder.CreateIndex(
                name: "UX_FormTemplateFieldMappings_Version_FieldName",
                schema: "dbo",
                table: "FormTemplateFieldMappings",
                columns: new[] { "FormTemplateVersionId", "PdfFieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_CreatedByUserId",
                schema: "dbo",
                table: "FormTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_Owner_Active_Name",
                schema: "dbo",
                table: "FormTemplates",
                columns: new[] { "OwnerType", "CompanyId", "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "UX_FormTemplates_Company_Key",
                schema: "dbo",
                table: "FormTemplates",
                columns: new[] { "CompanyId", "Key" },
                unique: true,
                filter: "[OwnerType] = 'Company'");

            migrationBuilder.CreateIndex(
                name: "UX_FormTemplates_Platform_Key",
                schema: "dbo",
                table: "FormTemplates",
                column: "Key",
                unique: true,
                filter: "[OwnerType] = 'Platform'");

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplateVersions_PublishedByUserId",
                schema: "dbo",
                table: "FormTemplateVersions",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplateVersions_Template_Status_Version",
                schema: "dbo",
                table: "FormTemplateVersions",
                columns: new[] { "FormTemplateId", "Status", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplateVersions_UploadedByUserId",
                schema: "dbo",
                table: "FormTemplateVersions",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_FormTemplateVersions_StorageObjectKey",
                schema: "dbo",
                table: "FormTemplateVersions",
                column: "StorageObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FormTemplateVersions_Template_Version",
                schema: "dbo",
                table: "FormTemplateVersions",
                columns: new[] { "FormTemplateId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormTemplateFieldMappings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FormTemplateVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FormTemplates",
                schema: "dbo");

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 105);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 106);
        }
    }
}
