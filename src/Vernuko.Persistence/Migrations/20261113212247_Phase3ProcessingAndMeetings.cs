using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vernuko.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3ProcessingAndMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppointmentBookings_AppointmentId_ResourceId",
                table: "AppointmentBookings");

            migrationBuilder.AddColumn<int>(
                name: "ProcessingMinutes",
                table: "Services",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingStartMinutes",
                table: "Services",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MeetingUrl",
                table: "Appointments",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingMinutes",
                table: "Appointments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingStartMinutes",
                table: "Appointments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentBookings_AppointmentId_ResourceId_BlockStartUtc",
                table: "AppointmentBookings",
                columns: new[] { "AppointmentId", "ResourceId", "BlockStartUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppointmentBookings_AppointmentId_ResourceId_BlockStartUtc",
                table: "AppointmentBookings");

            migrationBuilder.DropColumn(
                name: "ProcessingMinutes",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ProcessingStartMinutes",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "MeetingUrl",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ProcessingMinutes",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ProcessingStartMinutes",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentBookings_AppointmentId_ResourceId",
                table: "AppointmentBookings",
                columns: new[] { "AppointmentId", "ResourceId" },
                unique: true);
        }
    }
}
