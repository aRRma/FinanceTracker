using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Application.Infrastructure.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountColorAndIcon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "accounts",
                type: "TEXT",
                nullable: false,
                defaultValue: "Blue");

            migrationBuilder.AddColumn<string>(
                name: "icon",
                table: "accounts",
                type: "TEXT",
                nullable: true);

            // Уже заведённым счетам цвета раздаются по очереди, чтобы после обновления
            // балансы сразу были разноцветными. Действующие идут первыми и получают первые
            // цвета, за ними заблокированные, последними удалённые — им цвет не виден.
            // Очередь записана здесь, а не взята из кода: миграция обязана делать то же
            // самое и после того, как очередь в приложении поменяют
            migrationBuilder.Sql(
                """
                UPDATE accounts SET color = (
                    SELECT CASE (ranked.position - 1) % 8
                        WHEN 0 THEN 'Blue'
                        WHEN 1 THEN 'Orange'
                        WHEN 2 THEN 'Sky'
                        WHEN 3 THEN 'Green'
                        WHEN 4 THEN 'Violet'
                        WHEN 5 THEN 'Magenta'
                        WHEN 6 THEN 'Red'
                        ELSE 'Pink'
                    END
                    FROM (
                        SELECT key, ROW_NUMBER() OVER (
                            ORDER BY deleted_at_utc IS NOT NULL, is_closed, sort_order, key) AS position
                        FROM accounts) AS ranked
                    WHERE ranked.key = accounts.key)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "color",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "icon",
                table: "accounts");
        }
    }
}
