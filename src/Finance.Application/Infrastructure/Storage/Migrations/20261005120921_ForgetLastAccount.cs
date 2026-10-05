using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Application.Infrastructure.Storage.Migrations
{
    /// <inheritdoc />
    public partial class ForgetLastAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Форма больше не подставляет последний использованный счёт: её счёт —
            // счёт по умолчанию. Его строки здесь нет и заводить её не нужно — без
            // выбора счётом по умолчанию считается верхний незаблокированный в списке.
            // Строка настройки удаляется физически: обмену настройки не подлежат
            migrationBuilder.Sql("DELETE FROM settings WHERE name = 'transactions.last_account_key'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Возвращать нечего: последний использованный счёт после удаления не восстановить,
            // а прежняя форма без строки подставляла первый незаблокированный — так же, как сейчас
        }
    }
}
