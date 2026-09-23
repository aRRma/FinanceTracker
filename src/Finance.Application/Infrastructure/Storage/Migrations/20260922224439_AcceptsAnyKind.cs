using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Application.Infrastructure.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AcceptsAnyKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "accepts_any_kind",
                table: "categories",
                type: "INTEGER",
                nullable: true);

            // Уже созданным расходным группам признак проставляется здесь: стартовый
            // набор пишется только при инициализации, и без этого на установленном
            // приложении возврат по-прежнему было бы некуда записать. Расширение
            // допустимого ни одну записанную операцию не ломает, поэтому проставляется
            // всем расходным группам, включая заведённые пользователем
            migrationBuilder.Sql(
                "UPDATE categories SET accepts_any_kind = 1 WHERE parent_key IS NULL AND kind = 'Expense'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accepts_any_kind",
                table: "categories");
        }
    }
}
