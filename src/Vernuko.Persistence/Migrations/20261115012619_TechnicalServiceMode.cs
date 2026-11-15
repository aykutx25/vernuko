using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vernuko.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TechnicalServiceMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Device = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Brand = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    SerialNumber = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Complaint = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Accessories = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Condition = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    BroughtBy = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OnSite = table.Column<bool>(type: "INTEGER", nullable: false),
                    SiteAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReceivedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TechnicianStaffId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    StatusChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Diagnosis = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    WorkDone = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    EstimateMinor = table.Column<long>(type: "INTEGER", nullable: true),
                    Approval = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ApprovalChannel = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    ApprovalAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovalBy = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UnrepairableReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DiagnosticFeeMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    WarrantyOfTicketId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UnlockCodeProtected = table.Column<byte[]>(type: "BLOB", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeliveredTo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Repaired = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalChargedMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    DocumentKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceTickets_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTicketEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTicketEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceTicketEvents_ServiceTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ServiceTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTicketLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineNo = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPriceMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    VatRate = table.Column<int>(type: "INTEGER", nullable: false),
                    StaffId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StockMovementId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsRemoved = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTicketLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceTicketLines_ServiceTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ServiceTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTicketEvents_TicketId_AtUtc",
                table: "ServiceTicketEvents",
                columns: new[] { "TicketId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTicketLines_TicketId_LineNo",
                table: "ServiceTicketLines",
                columns: new[] { "TicketId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_CustomerId",
                table: "ServiceTickets",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_DocumentKey",
                table: "ServiceTickets",
                column: "DocumentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_Number",
                table: "ServiceTickets",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_SerialNumber",
                table: "ServiceTickets",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_Status_DueDate",
                table: "ServiceTickets",
                columns: new[] { "Status", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceTicketEvents");

            migrationBuilder.DropTable(
                name: "ServiceTicketLines");

            migrationBuilder.DropTable(
                name: "ServiceTickets");
        }
    }
}
