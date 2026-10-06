using CareComply.Infrastructure.Persistence;using Microsoft.EntityFrameworkCore.Infrastructure;using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace CareComply.Infrastructure.Persistence.Migrations;
[DbContext(typeof(CareComplyDbContext))][Migration("20260905070000_HardenClientHospitalizations")]
public sealed class HardenClientHospitalizations:Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
  migrationBuilder.AddColumn<Guid>(name:"CreatedByUserId",schema:"dbo",table:"ClientHospitalizations",type:"uniqueidentifier",nullable:true);
  migrationBuilder.Sql("UPDATE h SET [CreatedByUserId]=(SELECT TOP(1) [Id] FROM [dbo].[Users] ORDER BY [CreatedAtUtc]) FROM [dbo].[ClientHospitalizations] h");
  migrationBuilder.AlterColumn<Guid>(name:"CreatedByUserId",schema:"dbo",table:"ClientHospitalizations",type:"uniqueidentifier",nullable:false,oldClrType:typeof(Guid),oldType:"uniqueidentifier",oldNullable:true);
  migrationBuilder.AddColumn<DateTimeOffset>(name:"CreatedAtUtc",schema:"dbo",table:"ClientHospitalizations",type:"datetimeoffset",nullable:false,defaultValueSql:"SYSUTCDATETIME()");
  migrationBuilder.AddColumn<string>(name:"Diagnosis",schema:"dbo",table:"ClientHospitalizations",type:"nvarchar(2000)",maxLength:2000,nullable:false,defaultValue:"");
  migrationBuilder.AddColumn<string>(name:"DischargeInstructions",schema:"dbo",table:"ClientHospitalizations",type:"nvarchar(2000)",maxLength:2000,nullable:false,defaultValue:"");
  migrationBuilder.AddColumn<byte[]>(name:"RowVersion",schema:"dbo",table:"ClientHospitalizations",type:"rowversion",rowVersion:true,nullable:false);
  migrationBuilder.AddColumn<DateTimeOffset>(name:"UpdatedAtUtc",schema:"dbo",table:"ClientHospitalizations",type:"datetimeoffset",nullable:false,defaultValueSql:"SYSUTCDATETIME()");
  migrationBuilder.CreateIndex(name:"IX_ClientHospitalizations_CreatedByUserId",schema:"dbo",table:"ClientHospitalizations",column:"CreatedByUserId");
  migrationBuilder.AddForeignKey(name:"FK_ClientHospitalizations_Users_CreatedByUserId",schema:"dbo",table:"ClientHospitalizations",column:"CreatedByUserId",principalSchema:"dbo",principalTable:"Users",principalColumn:"Id",onDelete:ReferentialAction.Restrict);
 }
 protected override void Down(MigrationBuilder migrationBuilder){migrationBuilder.DropForeignKey(name:"FK_ClientHospitalizations_Users_CreatedByUserId",schema:"dbo",table:"ClientHospitalizations");migrationBuilder.DropIndex(name:"IX_ClientHospitalizations_CreatedByUserId",schema:"dbo",table:"ClientHospitalizations");foreach(var x in new[]{"CreatedByUserId","CreatedAtUtc","Diagnosis","DischargeInstructions","RowVersion","UpdatedAtUtc"})migrationBuilder.DropColumn(name:x,schema:"dbo",table:"ClientHospitalizations");}
}
