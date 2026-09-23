using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Application.Infrastructure.Storage.Migrations
{
    /// <inheritdoc />
    public partial class MergeServiceGroups : Migration
    {
        // Ключи стартового набора выведены из текстовых ключей и namespace, поэтому
        // одинаковы на всех устройствах и записаны здесь как есть: считать UUIDv5
        // средствами SQLite нечем, а брать их из набора на ходу нельзя — доходной
        // служебной группы в нём больше нет. Совпадение с Keys.Derive сверяет тест
        private const string ServiceAdjustment = "465b3b2e-3ba0-5a2f-a8c0-5e669a51c457";
        private const string IncomeServiceGroup = "2d81b2e3-697d-5466-a02b-e035063a9876";
        private const string IncomeServiceAdjustment = "5d9d12cd-2f80-5707-90c6-bbe38398ab3b";

        // Метка времени собирается в том же виде, в каком её пишет UtcMomentConverter:
        // фиксированная длина и нулевое смещение. %f даёт три знака доли секунды,
        // остальные четыре дописываются нулями — иначе строка не разберётся при чтении
        private const string Now = "strftime('%Y-%m-%d %H:%M:%f', 'now') || '0000+00:00'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Служебных групп было две — расходная и доходная — только потому, что
            // операция обязана была совпадать по виду с группой. С универсальными
            // группами доходная лишняя: она показывалась в выборе дохода второй
            // строкой «Служебное», неотличимой от первой. Записанные в неё операции
            // переезжают в расходную, она универсальна и принимает оба вида
            migrationBuilder.Sql(
                $"""
                UPDATE transactions
                SET category_key = '{ServiceAdjustment}', updated_at_utc = {Now}
                WHERE category_key = '{IncomeServiceAdjustment}'
                """);

            // Удаление мягкое: физический DELETE не даст будущему обмену узнать,
            // что этих категорий больше нет, и на другом устройстве они воскреснут
            migrationBuilder.Sql(
                $"""
                UPDATE categories
                SET deleted_at_utc = {Now}, updated_at_utc = {Now}
                WHERE key IN ('{IncomeServiceGroup}', '{IncomeServiceAdjustment}')
                  AND deleted_at_utc IS NULL
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Надгробие снимается, но операции назад не разъезжаются: какие из них
            // лежали в доходной «Разнице» до слияния, не помнит уже никто
            migrationBuilder.Sql(
                $"""
                UPDATE categories
                SET deleted_at_utc = NULL, updated_at_utc = {Now}
                WHERE key IN ('{IncomeServiceGroup}', '{IncomeServiceAdjustment}')
                """);
        }
    }
}
