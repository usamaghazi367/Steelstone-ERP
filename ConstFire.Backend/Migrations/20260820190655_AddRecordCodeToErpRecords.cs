using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstFire.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordCodeToErpRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ErpRecords_ModuleId",
                table: "ErpRecords");

            migrationBuilder.AddColumn<string>(
                name: "RecordCode",
                table: "ErpRecords",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpRecords_ModuleId_RecordCode",
                table: "ErpRecords",
                columns: new[] { "ModuleId", "RecordCode" },
                unique: true,
                filter: "[RecordCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ErpRecords_ModuleId_RecordCode",
                table: "ErpRecords");

            migrationBuilder.DropColumn(
                name: "RecordCode",
                table: "ErpRecords");

            migrationBuilder.CreateIndex(
                name: "IX_ErpRecords_ModuleId",
                table: "ErpRecords",
                column: "ModuleId");
        }
    }
}
