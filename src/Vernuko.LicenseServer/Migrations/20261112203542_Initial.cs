using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vernuko.LicenseServer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    LicenseId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Licenses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CustomerEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Edition = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Features = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    MaxDevices = table.Column<int>(type: "INTEGER", nullable: false),
                    PaidUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ProviderSubscriptionId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ProviderCustomerId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ProviderEventAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trials",
                columns: table => new
                {
                    DeviceHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trials", x => x.DeviceHash);
                });

            migrationBuilder.CreateTable(
                name: "WebhookEvents",
                columns: table => new
                {
                    Provider = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EventId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LicenseId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookEvents", x => new { x.Provider, x.EventId });
                });

            migrationBuilder.CreateTable(
                name: "Activations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    LicenseId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    DeviceHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    MachineLabel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activations_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activations_LicenseId_DeviceHash",
                table: "Activations",
                columns: new[] { "LicenseId", "DeviceHash" });

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_KeyHash",
                table: "Licenses",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_Provider_ProviderSubscriptionId",
                table: "Licenses",
                columns: new[] { "Provider", "ProviderSubscriptionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activations");

            migrationBuilder.DropTable(
                name: "Audit");

            migrationBuilder.DropTable(
                name: "Trials");

            migrationBuilder.DropTable(
                name: "WebhookEvents");

            migrationBuilder.DropTable(
                name: "Licenses");
        }
    }
}
