using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleAnchorOccurrenceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScheduleAnchorOccurrenceNumber",
                table: "RecurrenceSeries",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduleAnchorOccurrenceNumber",
                table: "RecurrenceSeries");
        }
    }
}
