using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vernuko.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "ServiceTicketLines",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTicketLines_IdempotencyKey",
                table: "ServiceTicketLines",
                column: "IdempotencyKey",
                unique: true);

            // Frozen lists (never change them after release). Supplier ledger, sale lines and goods receipts are
            // append-only like the other money ledgers; corrections are new documents.
            foreach (var statement in DatabaseTriggers.AppendOnlyStatements("SupplierTransactions", "SaleLines", "Purchases", "PurchaseLines"))
            {
                migrationBuilder.Sql(statement);
            }

            // Sales and the service ticket history are never deleted; their texts may be erased for KVKK, the rest
            // of a history row never changes.
            foreach (var statement in DatabaseTriggers.NoDeleteStatements("Sales", "ServiceTicketEvents"))
            {
                migrationBuilder.Sql(statement);
            }

            migrationBuilder.Sql(DatabaseTriggers.NoUpdateOfStatement("ServiceTicketEvents", "Id", "TicketId", "Kind", "UserId", "AtUtc"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var statement in DatabaseTriggers.DropAppendOnlyStatements("SupplierTransactions", "SaleLines", "Purchases", "PurchaseLines", "Sales", "ServiceTicketEvents"))
            {
                migrationBuilder.Sql(statement);
            }

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"trg_ServiceTicketEvents_NoUpdateKey\";");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTicketLines_IdempotencyKey",
                table: "ServiceTicketLines");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "ServiceTicketLines");
        }
    }
}
