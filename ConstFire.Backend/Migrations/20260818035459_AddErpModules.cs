using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstFire.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddErpModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErpModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpModules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ErpModuleFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    Ref = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Mandatory = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Validation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpModuleFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpModuleFields_ErpModules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "ErpModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ErpRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpRecords_ErpModules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "ErpModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErpModuleFields_ModuleId_Ref",
                table: "ErpModuleFields",
                columns: new[] { "ModuleId", "Ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpModules_Code",
                table: "ErpModules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpRecords_ModuleId",
                table: "ErpRecords",
                column: "ModuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErpModuleFields");

            migrationBuilder.DropTable(
                name: "ErpRecords");

            migrationBuilder.DropTable(
                name: "ErpModules");
        }
    }
}
