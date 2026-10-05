# Документация проекта

Точка входа. Здесь написано, где что лежит и что читать под конкретную задачу, чтобы не тянуть в контекст лишнее.

## Где искать требование по его идентификатору

Идентификаторы сквозные и не меняются никогда. Один префикс — один файл.

| Префикс | Файл | О чём |
|---|---|---|
| `SYS-*` | [requirements/01-scope.md](requirements/01-scope.md) | Границы MVP, платформа, объёмы, валюты |
| `INV-*` | [requirements/02-domain.md](requirements/02-domain.md) | Сущности, поля и правила, которые обязаны соблюдаться всегда |
| `FR-ACC-*` `FR-CAT-*` `FR-TRX-*` `FR-PAY-*` `FR-BAL-*` `FR-LED-*` `FR-RPT-*` `FR-SET-*` | [requirements/03-functional.md](requirements/03-functional.md) | Что приложение умеет делать |
| `SEED-*` | [requirements/04-preset.md](requirements/04-preset.md) | Инициализация базы: стартовые категории |
| `NFR-*` | [requirements/05-quality.md](requirements/05-quality.md) | Производительность, надёжность, время, оформление |
| `SYN-*` | [requirements/06-sync-deferred.md](requirements/06-sync-deferred.md); `FR-SYN-*` — в разделе «Синхронизация» [requirements/03-functional.md](requirements/03-functional.md) | **Вне MVP.** Обмен между устройствами |
| `TECH-*` | [architecture.md](architecture.md) | Состав проектов, слои, стек, ловушки реализации |
| `UC-*` | [use-cases.md](use-cases.md) | Сценарии использования |
| `A-*` `B-*` `C-*` `D-*` `E-*` | [ui/mockups.html](ui/mockups.html) | Экраны: каркас · ввод · состояния · справочники · отчёт |

Аннулированные и отложенные требования оставлены на своих местах зачёркнутым заголовком. Номер не освобождается и повторно не используется никогда: ссылки на него живут в документах, коммитах и обсуждениях (в код номера не попадают).

## Что читать под задачу

| Задача | Читать |
|---|---|
| Доменные типы, инварианты, фабрики | [requirements/02-domain.md](requirements/02-domain.md), [architecture.md](architecture.md) |
| Экран или форма ввода | [requirements/03-functional.md](requirements/03-functional.md), [use-cases.md](use-cases.md), нужный экран в [ui/mockups.html](ui/mockups.html) |
| Отчёт | `FR-RPT-*` в [requirements/03-functional.md](requirements/03-functional.md), экраны `E-01…E-04`, сценарий UC-19 |
| Схема, миграции, индексы | [requirements/02-domain.md](requirements/02-domain.md), `NFR-01…NFR-08` и `NFR-26` в [requirements/05-quality.md](requirements/05-quality.md), `TECH-*` в [architecture.md](architecture.md) |
| Инициализация базы | [requirements/04-preset.md](requirements/04-preset.md), [preset-rationale.md](preset-rationale.md), [data/preset.json](../data/preset.json) |
| Темы, цвета, значки | `NFR-21…NFR-25` в [requirements/05-quality.md](requirements/05-quality.md), переключатель темы в [ui/mockups.html](ui/mockups.html) — эталон палитр; перечень значков — [data/icons.json](../data/icons.json), контуры — [data/icon-paths.json](../data/icon-paths.json), значки макетов собирает `dotnet tools/mockup_icons.cs` |
| Как назвать новое понятие | [../CONTEXT.md](../CONTEXT.md) — сначала словарь, потом код |
| Перенос истории из других приложений (сборка `Finance.Import`) | [ADR-0014](adr/0014-wallet-import-one-shot-through-domain-path.md) |
| Выгрузка в файл и восстановление из неё | [ADR-0015](adr/0015-export-is-database-file.md), `FR-SET-05`, `FR-SET-06`, `NFR-27`, сценарий UC-25 |
| Цвет и значок счёта | [ADR-0016](adr/0016-account-color-and-icon.md), `FR-ACC-07`, экран D-12, сценарии UC-13 и UC-14 |
| Сборка, тесты, эмулятор, экраны, тексты | навыки в `.claude/skills/` — список и когда какой загружать в [../CLAUDE.md](../CLAUDE.md) |

## Остальные документы

- [decisions.md](decisions.md) — что сознательно не делается, чем рискуем, что ещё не решено
- [adr/](adr/) — решения, которые дорого пересматривать, с обоснованием и отвергнутыми вариантами. Требование говорит «что», ADR — «почему»; имя файла — суть решения, ссылка на него — `ADR-NNNN`
- [uncovered.md](uncovered.md) — функциональные требования без сценария и причина у каждого
- [preset-rationale.md](preset-rationale.md) — почему стартовый набор категорий именно такой. Обоснование, не спецификация
- [../tasks/README.md](../tasks/README.md) — как вести долгую задачу со своим состоянием: папка на задачу, закрытая уходит в архив вне репозитория
- [ui/prototype.html](ui/prototype.html) — интерактивный прототип, динамика сценариев; генерируется из макетов, руками не правится
- [../PLAN.md](../PLAN.md) — открытое, этап развития, итоги закрытого
