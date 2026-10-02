using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstFire.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddErpFieldOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErpFieldOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpFieldOptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErpFieldOptions_ListKey_Value",
                table: "ErpFieldOptions",
                columns: new[] { "ListKey", "Value" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErpFieldOptions");
        }
    }
}
