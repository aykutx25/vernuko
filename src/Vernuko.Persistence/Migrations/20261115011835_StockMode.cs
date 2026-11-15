using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vernuko.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StockMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                table: "StockMovements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "Products",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Products",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShelfLocation",
                table: "Products",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VatRate",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReturnOfSaleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GrossMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    LineDiscountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    DocumentDiscountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    VatMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    CashMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    CardMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    TransferMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    OnAccountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DocumentKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sales_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TaxOffice = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TaxNumber = table.Column<string>(type: "TEXT", maxLength: 11, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Iban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaleLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SaleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineNo = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPriceMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    VatRate = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    NetTotalMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    UnitCostMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    ReturnOfLineId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleLines_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Purchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SupplierId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentNo = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    DocumentDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    NetMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    VatMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    DocumentKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Purchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Purchases_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SupplierId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    BalanceEffectMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    PurchaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReversesId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierTransactions_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineNo = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitCostMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    VatRate = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    NetMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    VatMinor = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseLines_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_DocumentId",
                table: "StockMovements",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseLines_ProductId",
                table: "PurchaseLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseLines_PurchaseId_LineNo",
                table: "PurchaseLines",
                columns: new[] { "PurchaseId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_DocumentKey",
                table: "Purchases",
                column: "DocumentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_Number",
                table: "Purchases",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_SupplierId_OccurredAtUtc",
                table: "Purchases",
                columns: new[] { "SupplierId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_ProductId",
                table: "SaleLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_ReturnOfLineId",
                table: "SaleLines",
                column: "ReturnOfLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_SaleId_LineNo",
                table: "SaleLines",
                columns: new[] { "SaleId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_DocumentKey",
                table: "Sales",
                column: "DocumentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Number",
                table: "Sales",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_OccurredAtUtc",
                table: "Sales",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ReturnOfSaleId",
                table: "Sales",
                column: "ReturnOfSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierTransactions_IdempotencyKey",
                table: "SupplierTransactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierTransactions_ReversesId",
                table: "SupplierTransactions",
                column: "ReversesId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierTransactions_SupplierId_OccurredAtUtc",
                table: "SupplierTransactions",
                columns: new[] { "SupplierId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseLines");

            migrationBuilder.DropTable(
                name: "SaleLines");

            migrationBuilder.DropTable(
                name: "SupplierTransactions");

            migrationBuilder.DropTable(
                name: "Purchases");

            migrationBuilder.DropTable(
                name: "Sales");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_DocumentId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShelfLocation",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "Products");
        }
    }
}
