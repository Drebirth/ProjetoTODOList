using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTodoTitlesCategoriesPriorities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoriaId",
                table: "Todos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Prioridade",
                table: "Todos",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                table: "Todos",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CategoryIdBase",
                table: "RecurrenceSeries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriorityBase",
                table: "RecurrenceSeries",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TitleBase",
                table: "RecurrenceSeries",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categorias_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("UPDATE Todos SET Titulo = Descricao WHERE Titulo = '';");
            migrationBuilder.Sql("UPDATE RecurrenceSeries SET TitleBase = DescriptionBase WHERE TitleBase = '';");
            migrationBuilder.Sql("UPDATE Todos SET Prioridade = 'Media' WHERE Prioridade = '';");
            migrationBuilder.Sql("UPDATE RecurrenceSeries SET PriorityBase = 'Media' WHERE PriorityBase = '';");
            migrationBuilder.Sql("INSERT INTO Categorias (Nome, UserId) SELECT names.Nome, Users.Id FROM Users CROSS JOIN (SELECT 'Estudos' AS Nome UNION ALL SELECT 'Reunião' UNION ALL SELECT 'Trabalho' UNION ALL SELECT 'Pessoal') AS names;");

            migrationBuilder.CreateIndex(
                name: "IX_Todos_CategoriaId",
                table: "Todos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurrenceSeries_CategoryIdBase",
                table: "RecurrenceSeries",
                column: "CategoryIdBase");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_UserId",
                table: "Categorias",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecurrenceSeries_Categorias_CategoryIdBase",
                table: "RecurrenceSeries",
                column: "CategoryIdBase",
                principalTable: "Categorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Todos_Categorias_CategoriaId",
                table: "Todos",
                column: "CategoriaId",
                principalTable: "Categorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecurrenceSeries_Categorias_CategoryIdBase",
                table: "RecurrenceSeries");

            migrationBuilder.DropForeignKey(
                name: "FK_Todos_Categorias_CategoriaId",
                table: "Todos");

            migrationBuilder.DropTable(
                name: "Categorias");

            migrationBuilder.DropIndex(
                name: "IX_Todos_CategoriaId",
                table: "Todos");

            migrationBuilder.DropIndex(
                name: "IX_RecurrenceSeries_CategoryIdBase",
                table: "RecurrenceSeries");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "Todos");

            migrationBuilder.DropColumn(
                name: "Prioridade",
                table: "Todos");

            migrationBuilder.DropColumn(
                name: "Titulo",
                table: "Todos");

            migrationBuilder.DropColumn(
                name: "CategoryIdBase",
                table: "RecurrenceSeries");

            migrationBuilder.DropColumn(
                name: "PriorityBase",
                table: "RecurrenceSeries");

            migrationBuilder.DropColumn(
                name: "TitleBase",
                table: "RecurrenceSeries");
        }
    }
}

