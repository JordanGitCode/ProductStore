using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductStoreAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddScanStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScanStatus",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedCategory",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedDescription",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedName",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScanStatus",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SuggestedCategory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SuggestedDescription",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SuggestedName",
                table: "Products");
        }
    }
}
