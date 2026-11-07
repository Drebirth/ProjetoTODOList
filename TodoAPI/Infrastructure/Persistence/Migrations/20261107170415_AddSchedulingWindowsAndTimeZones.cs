using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingWindowsAndTimeZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "America/Fortaleza");

            migrationBuilder.AddColumn<long>(
                name: "DataFimUtc",
                table: "Todos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DataInicioUtc",
                table: "Todos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleAnchorLocalDate",
                table: "RecurrenceSeries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleEndLocalTime",
                table: "RecurrenceSeries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleStartLocalTime",
                table: "RecurrenceSeries",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DataFimUtc",
                table: "Todos");

            migrationBuilder.DropColumn(
                name: "DataInicioUtc",
                table: "Todos");

            migrationBuilder.DropColumn(
                name: "ScheduleAnchorLocalDate",
                table: "RecurrenceSeries");

            migrationBuilder.DropColumn(
                name: "ScheduleEndLocalTime",
                table: "RecurrenceSeries");

            migrationBuilder.DropColumn(
                name: "ScheduleStartLocalTime",
                table: "RecurrenceSeries");
        }
    }
}
