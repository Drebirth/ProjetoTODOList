using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTodoRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DataPrevistaUtc",
                table: "Todos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroOcorrencia",
                table: "Todos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SerieRecorrenciaId",
                table: "Todos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecurrenceSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    DescriptionBase = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Frequency = table.Column<string>(type: "TEXT", nullable: true),
                    Duration = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    NextOccurrenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    LastOccurrenceDueUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    NextDataPrevistaUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurrenceSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurrenceSeries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Todos_SerieRecorrenciaId_NumeroOcorrencia",
                table: "Todos",
                columns: new[] { "SerieRecorrenciaId", "NumeroOcorrencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurrenceSeries_UserId",
                table: "RecurrenceSeries",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Todos_RecurrenceSeries_SerieRecorrenciaId",
                table: "Todos",
                column: "SerieRecorrenciaId",
                principalTable: "RecurrenceSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Todos_RecurrenceSeries_SerieRecorrenciaId",
                table: "Todos");

            migrationBuilder.DropTable(
                name: "RecurrenceSeries");

            migrationBuilder.DropIndex(
                name: "IX_Todos_SerieRecorrenciaId_NumeroOcorrencia",
                table: "Todos");

            migrationBuilder.DropColumn(
                name: "DataPrevistaUtc",
                table: "Todos");

            migrationBuilder.DropColumn(
                name: "NumeroOcorrencia",
                table: "Todos");

            migrationBuilder.DropColumn(
                name: "SerieRecorrenciaId",
                table: "Todos");
        }
    }
}
