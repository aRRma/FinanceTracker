using System.Globalization;
using Finance.Application.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Application.Infrastructure.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddUnsortedGroups : Migration
    {
        // Миграция заморожена: набор будет меняться и дальше, а она обязана сделать
        // то же самое на любой базе, сколько бы версий ни прошло. Поэтому ключи и
        // версия записаны здесь как есть, а не берутся из набора. Ключи выведены из
        // текстовых unsorted_exp и unsorted_inc — совпадение с Keys.Derive сверяет тест
        private const string ExpenseGroup = "241f309d-cbb5-53f9-8e0d-80271aa75c39";
        private const string ExpenseReceiver = "b3dbd3bd-583e-5b61-a1dd-643e8a9e0c6e";
        private const string IncomeGroup = "4c125039-775b-5c49-b5a4-f11d09c7480f";
        private const string IncomeReceiver = "5fc3d835-916f-5468-b0b0-e6a07c4b3a95";

        // Название — данные набора, как в preset.json, а не текст интерфейса: из ресурса
        // его переведут или перепишут, а из набора — правка набора изменила бы, что
        // делает уже накатанная где-то миграция. Совпадение с набором сверяет тест
        private const string Name = "Без категории";

        private const string PreviousVersion = "2";
        private const string Version = "3";

        // Метка набора — та же давняя, что у остальных его строк, в виде UtcMomentConverter
        private const string Seeded = "'2000-01-01 00:00:00.0000000+00:00'";

        // Пробелы по краям имени, которые встречаются в набранном: обычный, табуляция,
        // переводы строки и неразрывный. Домен срезает все пробельные знаки, но имена
        // хранит уже срезанными, так что прочие сюда не доходят
        private const string Blanks = "' ' || char(9) || char(10) || char(13) || char(160)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Набор пишется один раз за жизнь установки, поэтому группы «Без категории»
            // приезжают на уже установленные базы миграцией. На новой установке миграции
            // идут раньше набора, и строки набора там ещё нет: группы заведёт он сам, а
            // вставка здесь уронила бы его на повторном ключе
            AddGroup(migrationBuilder, ExpenseGroup, ExpenseReceiver, "Expense", Name);
            AddGroup(migrationBuilder, IncomeGroup, IncomeReceiver, "Income", Name);

            // Отметка версии поднимается только со второй: у базы первой версии группы
            // теперь есть, а пересмотра второй версии по-прежнему нет
            migrationBuilder.Sql(
                $"""
                UPDATE settings SET value = '{Version}'
                WHERE name = '{SettingName.PresetVersion}' AND value = '{PreviousVersion}'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Группы остаются: в них уже могут лежать операции, а удалённая категория
            // оставила бы их без статьи. Откатывается только отметка версии — и повторный
            // подъём не заводит групп заново, потому что их ключи уже есть
            migrationBuilder.Sql(
                $"""
                UPDATE settings SET value = '{PreviousVersion}'
                WHERE name = '{SettingName.PresetVersion}' AND value = '{Version}'
                """);
        }

        private static void AddGroup(MigrationBuilder migrationBuilder, string group, string receiver, string kind, string name)
        {
            // Своя группа с тем же именем того же вида у пользователя уже может быть,
            // а имя группы уникально в своём виде без учёта регистра. Тогда новая не
            // заводится: его группа и есть «Без категории». Пропусти миграция «без
            // Категории», две группы с одним именем запретили бы сохранить любую из них.
            // lower() в SQLite кириллицу не понимает, поэтому строчными имя делает
            // replace() — по одной на каждую заглавную букву, какая в нём бывает
            string lowered = $"trim(name, {Blanks})";

            foreach (char letter in name.ToUpper(CultureInfo.InvariantCulture).Distinct())
            {
                char lower = char.ToLower(letter, CultureInfo.InvariantCulture);

                if (lower != letter)
                {
                    lowered = $"replace({lowered}, {Text(letter.ToString())}, {Text(lower.ToString())})";
                }
            }

            migrationBuilder.Sql(
                $"""
                INSERT INTO categories
                    (key, parent_key, kind, accepts_any_kind, name, icon, role, exclude_from_reports,
                     created_at_utc, updated_at_utc)
                SELECT '{group}', NULL, '{kind}', 0, {Text(name)}, 'question-mark', 'Normal', 0, {Seeded}, {Seeded}
                WHERE EXISTS (SELECT 1 FROM settings WHERE name = '{SettingName.PresetVersion}')
                  AND NOT EXISTS (SELECT 1 FROM categories WHERE key = '{group}')
                  AND NOT EXISTS (
                      SELECT 1 FROM categories
                      WHERE parent_key IS NULL AND kind = '{kind}' AND deleted_at_utc IS NULL
                        AND {lowered} = {Text(name.ToLower(CultureInfo.InvariantCulture))})
                """);

            // Приёмник — только под своей живой группой: если группа не заведена,
            // повиснуть без неё он не должен
            migrationBuilder.Sql(
                $"""
                INSERT INTO categories
                    (key, parent_key, kind, accepts_any_kind, name, icon, role, exclude_from_reports,
                     created_at_utc, updated_at_utc)
                SELECT '{receiver}', '{group}', NULL, NULL, {Text(name)}, 'question-mark', 'Other', 0, {Seeded}, {Seeded}
                WHERE EXISTS (SELECT 1 FROM categories WHERE key = '{group}' AND deleted_at_utc IS NULL)
                  AND NOT EXISTS (SELECT 1 FROM categories WHERE key = '{receiver}')
                """);
        }

        // Строка SQL в кавычках: кавычка в названии иначе оборвала бы запрос
        private static string Text(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}
