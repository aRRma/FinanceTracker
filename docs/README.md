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
| `SYN-*` `FR-SYN-*` | [requirements/06-sync-deferred.md](requirements/06-sync-deferred.md) | **Вне MVP.** Обмен между устройствами |
| `TECH-*` | [architecture.md](architecture.md) | Состав проектов, слои, стек, ловушки реализации |
| `UC-*` | [use-cases.md](use-cases.md) | Сценарии использования |
| `A-*` `B-*` `C-*` `D-*` `E-*` | [ui/mockups.html](ui/mockups.html) | Экраны: каркас · ввод · состояния · справочники · отчёт |

Аннулированные и отложенные требования оставлены на своих местах зачёркнутым заголовком. Номер не освобождается и повторно не используется никогда: ссылки на него живут в коде, коммитах и обсуждениях.

## Что читать под задачу

| Задача | Читать |
|---|---|
| Доменные типы, инварианты, фабрики | [requirements/02-domain.md](requirements/02-domain.md), [architecture.md](architecture.md) |
| Экран или форма ввода | [requirements/03-functional.md](requirements/03-functional.md), [use-cases.md](use-cases.md), нужный экран в [ui/mockups.html](ui/mockups.html) |
| Отчёт | `FR-RPT-*` в [requirements/03-functional.md](requirements/03-functional.md), экраны `E-01…E-04`, сценарий UC-19 |
| Схема, миграции, индексы | [requirements/02-domain.md](requirements/02-domain.md), `NFR-01…NFR-08` в [requirements/05-quality.md](requirements/05-quality.md), `TECH-*` в [architecture.md](architecture.md) |
| Инициализация базы | [requirements/04-preset.md](requirements/04-preset.md), [preset-rationale.md](preset-rationale.md), [data/preset.json](../data/preset.json) |
| Темы и цвета | `NFR-21…NFR-25` в [requirements/05-quality.md](requirements/05-quality.md), переключатель темы в [ui/mockups.html](ui/mockups.html) — эталон палитр |
| Как назвать новое понятие | [../CONTEXT.md](../CONTEXT.md) — сначала словарь, потом код |

## Остальные документы

- [decisions.md](decisions.md) — что сознательно не делается, чем рискуем, что ещё не решено
- [adr/](adr/) — решения, которые дорого пересматривать, с обоснованием и отвергнутыми вариантами. Требование говорит «что», ADR — «почему»:
  - ADR-0001 справочник мест назван `Place`, отраслевое имя отвергнуто · ADR-0002 идентификаторы UUIDv7 с клиента, в SQLite текстом · ADR-0003 поля обмена и мягкое удаление до появления обмена · ADR-0004 деньги `decimal` с валютой в типе, суммы по валютам не существует · ADR-0005 два уровня категорий, место вместо третьего · ADR-0006 детерминированные ключи стартового набора и давняя метка времени · ADR-0007 MVP на одном устройстве, сервер без доменной логики · ADR-0008 слайсы по функциям вне MAUI, без посредника и репозиториев · ADR-0009 календарные даты без времени · ADR-0010 начальный остаток — поле счёта · ADR-0011 деньги хранятся целыми копейками · ADR-0012 текст в ресурсах, культура параметром
- [preset-rationale.md](preset-rationale.md) — почему стартовый набор категорий именно такой. Обоснование, не спецификация
- [design/audit-2026-09.md](design/audit-2026-09.md) — аудит интерфейса собранного приложения: находки по экранам и что с ними делать. Наброски трёх направлений оформления — [design/explorations.html](design/explorations.html), по четыре экрана в светлой и тёмной теме; в проверки макетов не входят, это эскизы, а не эталон
- [ui/prototype.html](ui/prototype.html) — интерактивный прототип, динамика сценариев. Собирается из макетов скриптом `tools/build_prototype.py`, руками не правится
- [../PLAN.md](../PLAN.md) — этапы и чеклисты

## Границы MVP

Одно устройство одного пользователя, без сети. Обмен с сервером отложен целиком — [requirements/06-sync-deferred.md](requirements/06-sync-deferred.md) сохранён потому, что поля схемы под него заведены сразу, и переделывать базу задним числом не придётся.

Отчёт о расходах и доходах по группам за месяц, наоборот, в MVP входит.
