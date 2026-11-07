using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTodoCompletionTimestamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DataConclusaoUtc",
                table: "Todos",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataConclusaoUtc",
                table: "Todos");
        }
    }
}
