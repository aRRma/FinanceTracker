# Документация проекта

Точка входа: где что лежит и что читать под конкретную задачу, чтобы не тянуть в контекст лишнее.

## Где искать требование по его идентификатору

Идентификаторы сквозные и не меняются никогда. Один префикс — один файл.

| Префикс | Файл | О чём |
|---|---|---|
| `SYS-*` | [requirements/01-scope.md](requirements/01-scope.md) | Границы MVP, платформа, объёмы, валюты |
| `INV-*` | [requirements/02-domain.md](requirements/02-domain.md) | Сущности, поля и правила, которые обязаны соблюдаться всегда |
| `FR-ACC-*` `FR-CAT-*` `FR-TRX-*` `FR-PAY-*` `FR-BAL-*` `FR-LED-*` `FR-RPT-*` `FR-SET-*` | [requirements/03-functional.md](requirements/03-functional.md) | Что приложение умеет делать |
| `SEED-*` | [requirements/04-preset.md](requirements/04-preset.md) | Инициализация базы: стартовые категории |
| `NFR-*` | [requirements/05-quality.md](requirements/05-quality.md) | Производительность, надёжность, время, безопасность, оформление |
| `SYN-*` | [requirements/06-sync-deferred.md](requirements/06-sync-deferred.md); `FR-SYN-*` — в разделе «Синхронизация — отложено» [requirements/03-functional.md](requirements/03-functional.md) | **Вне MVP.** Обмен между устройствами |
| `TECH-*` | [architecture.md](architecture.md) | Состав проектов, слои, стек, миграции, особенности реализации |
| `UC-*` | [use-cases.md](use-cases.md) | Сценарии использования |
| `A-*` `B-*` `C-*` `D-*` `E-*` | [ui/mockups.html](ui/mockups.html) | Экраны: каркас · ввод · состояния · справочники · отчёт |

Аннулированные и отложенные требования оставлены на своих местах зачёркнутым заголовком. Номер не освобождается и повторно не используется никогда.

## Что читать под задачу

| Задача | Читать |
|---|---|
| Доменные типы, инварианты, фабрики | [requirements/02-domain.md](requirements/02-domain.md), [architecture.md](architecture.md) |
| Экран или форма ввода | [requirements/03-functional.md](requirements/03-functional.md), [use-cases.md](use-cases.md), нужный экран в [ui/mockups.html](ui/mockups.html), стиль — [ui/style.md](ui/style.md) |
| Счета | `FR-ACC-*`, `FR-BAL-*`, [ADR-0010](adr/0010-opening-balance-is-account-field.md), экраны A-01, D-01, D-02, сценарии UC-13…UC-15, UC-21 |
| Цвет и значок счёта | [ADR-0016](adr/0016-account-color-and-icon.md), `FR-ACC-07`, экран D-12, сценарии UC-13 и UC-14 |
| Операции, лента | `FR-TRX-*`, `FR-LED-*`, `TECH-17`, экраны B-01…B-09, A-02, A-03, сценарии UC-01, UC-04, UC-06…UC-08 |
| Категории и места | `FR-CAT-*`, `FR-PAY-*`, ADR-0001, [ADR-0005](adr/0005-two-level-categories-place-instead-of-third.md), экраны D-03…D-05, D-07, сценарии UC-11, UC-12, UC-18, UC-23 |
| Универсальные группы, возвраты | [ADR-0013](adr/0013-universal-groups-refund-nets-against-expense.md), `FR-CAT-14`, `FR-RPT-11`, сценарий UC-22 |
| Отчёт | `FR-RPT-*`, `TECH-08`, `TECH-21`, экраны E-01…E-07, сценарий UC-19 |
| Деньги, даты, часовой пояс | [ADR-0004](adr/0004-money-decimal-currency-in-type-no-conversion.md), [ADR-0009](adr/0009-calendar-dates-without-time.md), [ADR-0011](adr/0011-money-stored-in-minor-units.md), `NFR-09…NFR-14`, `FR-SET-01`, экран D-08 |
| Схема, миграции, индексы | [requirements/02-domain.md](requirements/02-domain.md), `NFR-01…NFR-08` и `NFR-26` в [requirements/05-quality.md](requirements/05-quality.md), раздел «Миграции схемы» и `TECH-*` в [architecture.md](architecture.md) |
| Инициализация базы и запуск | [requirements/04-preset.md](requirements/04-preset.md), [preset-rationale.md](preset-rationale.md), [data/preset.json](../data/preset.json), `TECH-22` |
| Темы, цвета, значки | `NFR-21…NFR-25`, переключатель темы в [ui/mockups.html](ui/mockups.html) — эталон палитр; стиль «Изумруд» — [ui/style.md](ui/style.md), там же знак приложения; перечень значков — [data/icons.json](../data/icons.json), контуры — [data/icon-paths.json](../data/icon-paths.json), значки макетов собирает `dotnet tools/mockup_icons.cs` |
| Тексты и перевод | [ADR-0012](adr/0012-text-in-resources-culture-as-parameter.md), навык `texts` |
| Как назвать новое понятие | [../CONTEXT.md](../CONTEXT.md) — сначала словарь, потом код |
| Перенос истории из других приложений (сборка `Finance.Import`) | [ADR-0014](adr/0014-wallet-import-one-shot-through-domain-path.md) |
| Выгрузка в файл и восстановление из неё | [ADR-0015](adr/0015-export-is-database-file.md), `FR-SET-05`, `FR-SET-06`, `NFR-27`, `TECH-15`, экраны D-10, D-11, C-15, C-16, сценарий UC-25 |
| Счёт по умолчанию | `FR-SET-07`, `FR-TRX-06`, `TECH-18`, экран D-13, сценарии UC-28, UC-15, UC-21 |
| Защита входа ПИН-кодом | [ADR-0017](adr/0017-entry-protection-own-pin-outside-database.md), `FR-SET-08`, `NFR-28`, `NFR-29`, `TECH-19`, экраны C-10…C-12, C-22, D-14, D-15, сценарий UC-26 |
| Отчёты о сбоях | [ADR-0018](adr/0018-crash-reports-file-outside-database.md), `FR-SET-09`, `TECH-20`, раздел «Сбои» в [../CONTEXT.md](../CONTEXT.md), экраны D-16, D-17, C-18, сценарий UC-27; как достать отчёты на эмуляторе — навык `emulator` |
| Обмен с сервером (вне MVP) | [requirements/06-sync-deferred.md](requirements/06-sync-deferred.md), [ADR-0003](adr/0003-sync-fields-and-soft-delete-before-sync.md), [ADR-0007](adr/0007-mvp-single-device-dumb-server.md) |
| Сборка, тесты, эмулятор, экраны, тексты | навыки в `.claude/skills/` — список и когда какой загружать в [../CLAUDE.md](../CLAUDE.md) |

## Остальные документы

- [decisions.md](decisions.md) — что сознательно не делается, чем рискуем, что ещё не решено
- [adr/](adr/) — решения, которые дорого пересматривать, с обоснованием и отвергнутыми вариантами. Требование говорит «что», ADR — «почему»; имя файла — суть решения, ссылка на него — `ADR-NNNN`
- [uncovered.md](uncovered.md) — функциональные требования без сценария и причина у каждого
- [preset-rationale.md](preset-rationale.md) — почему стартовый набор категорий именно такой
- [ui/prototype.html](ui/prototype.html) — интерактивный прототип, динамика сценариев; генерируется из макетов, руками не правится
- [../tasks/README.md](../tasks/README.md) — долгие задачи со своим состоянием
- [../PLAN.md](../PLAN.md) — открытое, этап развития, итоги закрытого
