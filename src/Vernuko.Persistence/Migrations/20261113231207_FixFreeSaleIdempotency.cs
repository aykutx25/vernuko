using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vernuko.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixFreeSaleIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SaleKey",
                table: "StockMovements",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SaleKey",
                table: "CustomerPackages",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_SaleKey",
                table: "StockMovements",
                column: "SaleKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPackages_SaleKey",
                table: "CustomerPackages",
                column: "SaleKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_SaleKey",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPackages_SaleKey",
                table: "CustomerPackages");

            migrationBuilder.DropColumn(
                name: "SaleKey",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "SaleKey",
                table: "CustomerPackages");
        }
    }
}
