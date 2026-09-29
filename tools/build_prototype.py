#!/usr/bin/env python3
"""Сборка docs/ui/prototype.html из docs/ui/mockups.html.

Прототип не рисуется руками: каждый экран берётся из макетов как есть, а сценарии
описаны данными ниже. Поэтому макеты и прототип не могут разойтись — правится
только mockups.html, затем запускается этот скрипт.

Запуск:   python tools/build_prototype.py          # пересобрать
Проверка: python tools/build_prototype.py --check  # упасть, если prototype.html устарел

В собранный файл проставляются отпечатки источников. По ним свежесть сборки
проверяет Finance.Docs.Tests: вызывать Python из тестов нельзя, а расходиться
макетам и прототипу нельзя тем более.
"""
import hashlib, json, pathlib, re, sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
CRLF, LF = chr(13) + chr(10), chr(10)

SRC = ROOT / "docs/ui/mockups.html"
DST = ROOT / "docs/ui/prototype.html"

# Экраны, которые выезжают снизу как лист, а не сдвигаются сбоку
SHEETS = ["B-01", "B-05", "B-06", "C-01", "C-07", "D-02", "D-04", "D-07"]

# Стили, которыми сценарии прячут диалог, скрывают строки и включают переключатели
VEIL_OFF = {".veil": "display:none"}
# Пустая сумма формы — серый ноль; первая цифра возвращает итогу цвет вида
ZERO = {".amt .v": "0,00 ₽", ".amt .expr": ""}
ZERO_CSS = {".amt .v": "color:var(--ink3)"}
TONE = {".amt .v": ""}
# Незакрытое действие: вместо итога — приглушённая подсказка про «=»
PENDING = {".amt .v": "font-size:12.5px;font-weight:400;color:var(--ink3);line-height:36px"}

# Сценарии. Имя — имя сценария из docs/use-cases.md, uc — его номер.
# Шаг: s — экран, c — подпись, t — цель касания (селектор или text=…), k — клавиша
# цифровой клавиатуры, set — подставить текст, css — подставить стиль, theme — тема, w — пауза.
SCENARIOS = [
 {"n": "Запустить приложение впервые", "uc": "UC-20", "steps": [
  {"s": "C-06", "c": "Первый запуск: база инициализирована, категории есть, счетов нет — добавить операцию ещё нельзя", "w": 1700},
  {"s": "C-06", "c": "Касание «Создать счёт»", "t": ".btn"},
  {"s": "D-02", "c": "Тип и валюта заполнены умолчаниями — вводятся только название и остаток", "w": 1700,
   "set": {".hd h2": "Новый счёт", "text=Название": ""}},
  {"s": "D-02", "c": "Ввод названия", "t": "text=Название", "set": {"text=Название": "Карта осн"}, "w": 450},
  {"s": "D-02", "c": "Ввод названия", "set": {"text=Название": "Карта основная"}, "w": 700},
  {"s": "D-02", "c": "Начальный остаток — поле счёта, а не операция в ленте", "t": "text=Начальный остаток", "w": 1400},
  {"s": "D-02", "c": "Сохранение — кнопкой под формой", "t": ".btn", "w": 700},
  {"s": "A-01", "c": "Баланс равен начальному остатку — операций ещё нет", "w": 1800},
  {"s": "A-01", "c": "Касание счёта", "t": "text=Карта основная"},
  {"s": "C-03", "c": "Пустая лента: карточка начального остатка объясняет, откуда взялся баланс", "w": 2000},
  {"s": "C-03", "c": "Касание «Добавить операцию»", "t": ".btn"},
  {"s": "B-01", "c": "Дальше — обычный ввод расхода, сценарий «Записать расход»", "w": 2200,
   "set": ZERO, "css": ZERO_CSS},
 ]},
 {"n": "Записать расход", "uc": "UC-01", "steps": [
  {"s": "A-01", "c": "Главный экран. Доступно к тратам — 82 430,50 ₽", "w": 1300},
  {"s": "A-01", "c": "Касание «Операция»", "t": ".fab"},
  {"s": "B-01", "c": "Форма открывается сразу с клавиатурой: вид «расход», последний счёт и сегодняшняя дата подставлены", "w": 1700,
   "set": ZERO, "css": ZERO_CSS},
  {"s": "B-01", "c": "Ввод суммы", "k": "1", "set": {".amt .v": "-1,00 ₽"}, "css": TONE, "w": 260},
  {"s": "B-01", "c": "Ввод суммы", "k": "2", "set": {".amt .v": "-12,00 ₽"}, "w": 260},
  {"s": "B-01", "c": "Ввод суммы", "k": "5", "set": {".amt .v": "-125,00 ₽"}, "w": 260},
  {"s": "B-01", "c": "Ввод суммы", "k": "0", "set": {".amt .v": "-1 250,00 ₽"}, "w": 700},
  {"s": "B-01", "c": "Касание поля «Категория»", "t": "text=Категория"},
  {"s": "B-03", "c": "Группы свёрнуты, над ними — частые подкатегории: привычная выбирается одним касанием", "w": 1600},
  {"s": "B-03", "c": "Касание «Продукты»", "t": "text=Продукты"},
  {"s": "B-01", "c": "Категория выбрана, место необязательно — можно сохранять", "w": 1300, "set": {"text=Категория": "Продукты"}},
  {"s": "B-01", "c": "Сохранение", "t": ".hd .save"},
  {"s": "A-01", "c": "Расход записан: четыре касания сверх набора суммы", "w": 2800},
 ]},
 {"n": "Записать вчерашнюю покупку", "uc": "UC-24", "steps": [
  {"s": "A-01", "c": "Вспомнил про вчерашнюю покупку", "w": 1100},
  {"s": "A-01", "c": "Касание «Операция»", "t": ".fab"},
  {"s": "B-01", "c": "Сумма", "set": ZERO, "css": ZERO_CSS, "w": 500},
  {"s": "B-01", "c": "Ввод суммы", "k": "8", "set": {".amt .v": "-8,00 ₽"}, "css": TONE, "w": 260},
  {"s": "B-01", "c": "Ввод суммы", "k": "9", "set": {".amt .v": "-89,00 ₽"}, "w": 260},
  {"s": "B-01", "c": "Ввод суммы", "k": "0", "set": {".amt .v": "-890,00 ₽"}, "w": 600},
  {"s": "B-01", "c": "«Вчера» — чипом прямо в форме, без календаря", "t": "text=Вчера", "set": {"#b1-date": "Вчера"}, "w": 1300},
  {"s": "B-01", "c": "Дата старше — третий чип открывает календарь", "t": "text=…"},
  {"s": "B-04", "c": "Системный календарь: будущие дни погашены. Сегодня он не нужен", "w": 1800},
  {"s": "B-04", "c": "Отмена", "t": ".dlg .no"},
  {"s": "B-01", "c": "Касание поля «Место»", "t": "text=Место"},
  {"s": "B-07", "c": "Отбор по первым буквам, частые выше; нет совпадения — место заводится отсюда же", "w": 1500},
  {"s": "B-07", "c": "Касание «Пятёрочка»", "t": "text=Пятёрочка"},
  {"s": "B-01", "c": "Место подставлено", "w": 900, "set": {"text=Место": "Пятёрочка"}},
  {"s": "B-01", "c": "Касание поля «Категория»", "t": "text=Категория"},
  {"s": "B-03", "c": "Касание «Продукты»", "t": "text=Продукты", "w": 800},
  {"s": "B-01", "c": "Всё заполнено", "w": 1000, "set": {"text=Категория": "Продукты"}},
  {"s": "B-01", "c": "Сохранение", "t": ".hd .save"},
  {"s": "A-03", "c": "Операция встала во вчерашний день ленты", "w": 2600},
 ]},
 {"n": "Перевести деньги между счетами", "uc": "UC-04", "steps": [
  {"s": "A-01", "c": "Кредитка в минусе на 1 890 ₽ — погашаем с основной карты", "w": 1600},
  {"s": "A-01", "c": "Касание «Операция»", "t": ".fab"},
  {"s": "B-01", "c": "Переключение вида на «Перевод»", "t": "text=Перевод", "w": 900,
   "set": ZERO, "css": ZERO_CSS},
  {"s": "B-05", "c": "Категория уступает место «Куда», место исчезает: у перевода его нет", "w": 1700,
   "set": {"text=Куда": "Выбрать", ".amt .v": "1 890,00 ₽"}},
  {"s": "B-05", "c": "Касание «Куда»", "t": "text=Куда"},
  {"s": "B-02", "c": "Выбор счёта назначения", "t": "text=Карта кредитная", "w": 900},
  {"s": "B-05", "c": "Валюты совпадают — сумма одна", "w": 1400, "set": {"text=Куда": "Карта кредитная"}},
  {"s": "B-05", "c": "Сохранение", "t": ".hd .save"},
  {"s": "A-01", "c": "Долг по кредитке закрыт: баланс вырос от -1 890 до нуля", "w": 2800,
   "set": {"#a1-credit .num": "0,00 ₽"}, "css": {"#a1-credit .num": "color:var(--ink)"}},
 ]},
 {"n": "Отложить в накопления", "uc": "UC-05", "steps": [
  {"s": "A-01", "c": "Накопления показаны своей карточкой без подытога: они скрытые", "w": 1500},
  {"s": "A-01", "c": "Касание «Операция»", "t": ".fab"},
  {"s": "B-01", "c": "Переключение вида на «Перевод»", "t": "text=Перевод", "w": 900},
  {"s": "B-05", "c": "Категория уступает место «Куда», место исчезает: у перевода его нет", "w": 1700},
  {"s": "B-05", "c": "Касание «Куда»", "t": "text=Куда"},
  {"s": "B-02", "c": "Выбор счёта назначения", "t": "text=Копилка на отпуск", "w": 900},
  {"s": "B-05", "c": "Валюты совпадают — сумма одна", "w": 1400},
  {"s": "B-05", "c": "Сохранение", "t": ".hd .save"},
  {"s": "A-01", "c": "Доступно к тратам уменьшилось, накопления выросли. Общая сумма денег не изменилась", "w": 2800},
 ]},
 {"n": "Перевести между валютами", "uc": "UC-10", "steps": [
  {"s": "B-05", "c": "Перевод: счёт назначения ещё не выбран", "w": 1200},
  {"s": "B-05", "c": "Касание «Куда»", "t": "text=Куда"},
  {"s": "B-02", "c": "Выбор счёта в другой валюте", "t": "text=Карта евро", "w": 900},
  {"s": "B-06", "c": "Появилась вторая сумма — зачисление. Курс не запрашивается, только две суммы", "w": 2200},
  {"s": "B-06", "c": "Сохранение", "t": ".hd .save"},
  {"s": "A-01", "c": "Оба баланса изменились, каждый в своей валюте", "w": 2400},
 ]},
 {"n": "Просмотреть ленту счёта", "uc": "UC-06", "steps": [
  {"s": "A-01", "c": "Главный экран", "w": 1000},
  {"s": "A-01", "c": "Касание счёта", "t": "text=Карта основная"},
  {"s": "A-03", "c": "Лента одного счёта: по дням, с итогом дня. Подпись — заметка, место или группа", "w": 2200},
  {"s": "A-03", "c": "Назад", "t": ".hd .act", "w": 600},
  {"s": "A-01", "c": "Касание вкладки «Операции»", "t": ".nav a:nth-child(2)"},
  {"s": "A-02", "c": "Все счета вперемешку, счёт подписан у каждой строки. Перевод — одной строкой, «откуда → куда»", "w": 2600},
  {"s": "A-02", "c": "Касание вкладки «Балансы»", "t": ".nav a:nth-child(1)"},
  {"s": "A-01", "c": "Касание счёта без операций", "t": "text=Карта евро"},
  {"s": "C-03", "c": "Пустая лента: карточка начального остатка и приглашение добавить операцию", "w": 2400},
 ]},
 {"n": "Исправить ошибочную операцию", "uc": "UC-07", "steps": [
  {"s": "A-03", "c": "Лента счёта. Ошибка: кофе записан как продукты", "w": 1500},
  {"s": "A-03", "c": "Касание строки", "t": "text=Продукты"},
  {"s": "C-07", "c": "Карточка операции — та же форма, что при вводе", "w": 1500, "css": VEIL_OFF},
  {"s": "C-07", "c": "Касание поля «Категория»", "t": "text=Категория"},
  {"s": "B-03", "c": "Выбор правильной подкатегории", "t": "text=Кофе", "w": 900},
  {"s": "C-07", "c": "Категория заменена. Баланс не меняется — сумма та же", "w": 1500, "set": {"text=Категория": "Кофе"}, "css": VEIL_OFF},
  {"s": "C-07", "c": "Сохранение", "t": ".hd .save", "css": VEIL_OFF},
  {"s": "A-03", "c": "Строка в ленте обновилась", "w": 2200},
 ]},
 {"n": "Найти операцию через отчёт", "uc": "UC-07", "steps": [
  {"s": "A-01", "c": "Поиска нет, но помнишь категорию и месяц — отчёт приводит к операции", "w": 1800},
  {"s": "A-01", "c": "Касание вкладки «Отчёт»", "t": ".nav a:nth-child(3)"},
  {"s": "E-01", "c": "Касание «Еда»", "t": "text=Еда", "w": 900},
  {"s": "E-02", "c": "Касание «Доставка еды»", "t": "text=Доставка еды", "w": 900},
  {"s": "E-03", "c": "Вот она: «Самокат» 28 августа записан как доставка, а это были продукты", "w": 2000},
  {"s": "E-03", "c": "Касание строки", "t": "text=Самокат"},
  {"s": "C-07", "c": "Та же карточка операции, что открывается из ленты", "w": 1500, "css": VEIL_OFF,
   "set": {".amt .v": "-890,00 ₽", "text=Категория": "Доставка еды", "text=Место": "Самокат", "#c7-date": "28 августа"}},
  {"s": "C-07", "c": "Касание поля «Категория»", "t": "text=Категория"},
  {"s": "B-03", "c": "Выбор «Продукты»", "t": "text=Продукты", "w": 800},
  {"s": "C-07", "c": "Категория заменена", "w": 1200, "set": {"text=Категория": "Продукты"}, "css": VEIL_OFF},
  {"s": "C-07", "c": "Сохранение", "t": ".hd .save", "css": VEIL_OFF},
  {"s": "E-03", "c": "Операция ушла из «Доставки еды», суммы отчёта пересчитаны", "w": 2600,
   "css": {"#e3-samokat": "display:none"}, "set": {"#e3-total": "-2 300,00 ₽", "#e3-cap": "август 2026 · 7 операций"}},
 ]},
 {"n": "Оформить возврат покупки", "uc": "UC-22", "steps": [
  {"s": "A-03", "c": "Вернул часть покупки в аптеке, 600 ₽ пришли обратно на карту", "w": 1800},
  {"s": "A-03", "c": "Касание строки", "t": "text=Аптека"},
  {"s": "C-07", "c": "Карточка операции. Возврат оформляется правкой исходного расхода", "w": 1700, "css": VEIL_OFF,
   "set": {".amt .v": "-1 890,00 ₽", "text=Категория": "Аптека", "text=Место": "Ригла", "#c7-date": "22 августа"}},
  {"s": "C-07", "c": "Касание суммы", "t": ".amt"},
  {"s": "B-01", "c": "Сумму можно не пересчитывать в уме: выражение прямо в поле, итог — только по «=»", "w": 2000,
   "set": {".hd h2": "Операция", ".amt .expr": "1890 − 600", ".amt .v": "нажмите «=», чтобы посчитать", "text=Категория": "Аптека", "text=Место": "Ригла", "#b1-date": "22 августа"},
   "css": PENDING},
  {"s": "B-01", "c": "Итог считает клавиша «=»", "k": "=", "w": 1600,
   "set": {".amt .expr": "", ".amt .v": "-1 290,00 ₽"}, "css": TONE},
  {"s": "B-01", "c": "Сохранение", "t": ".hd .save"},
  {"s": "A-03", "c": "Расход уменьшен, баланс вырос на 600 ₽. Вернули всё — операцию удаляют", "w": 2800,
   "set": {"text=Аптека": "-1 290,00 ₽", "text=22 августа": "-1 335,00 ₽"}},
 ]},
 {"n": "Записать расход: забыта категория", "uc": "UC-01", "steps": [
  {"s": "B-01", "c": "Сумма набрана, категория не выбрана", "w": 1400, "set": {".amt .v": "-1 250,00 ₽", ".amt .expr": ""}},
  {"s": "B-01", "c": "Сохранение", "t": ".hd .save"},
  {"s": "C-01", "c": "Карточка над клавиатурой называет, чего не хватает. Сохранение не гасло заранее", "w": 2600},
  {"s": "C-01", "c": "Касание поля «Категория»", "t": "text=Категория"},
  {"s": "B-03", "c": "Выбор «Продукты»", "t": "text=Продукты", "w": 800},
  {"s": "B-01", "c": "Категория выбрана, ошибка ушла", "w": 1200, "set": {"text=Категория": "Продукты"}},
  {"s": "B-01", "c": "Сохранение", "t": ".hd .save"},
  {"s": "A-01", "c": "Расход записан", "w": 2000},
 ]},
 {"n": "Удалить операцию", "uc": "UC-08", "steps": [
  {"s": "A-03", "c": "Лента счёта", "w": 1100},
  {"s": "A-03", "c": "Касание строки", "t": "text=Продукты"},
  {"s": "C-07", "c": "Карточка операции", "w": 1200, "css": VEIL_OFF},
  {"s": "C-07", "c": "Касание «Удалить» в шапке", "t": ".hd .del", "css": VEIL_OFF},
  {"s": "C-07", "c": "Подтверждение называет, каким станет баланс. Отмены и корзины нет — это единственная защита", "w": 2600, "css": {".veil": ""}},
  {"s": "C-07", "c": "Касание «Удалить»", "t": ".dlg .yes"},
  {"s": "A-03", "c": "Строка исчезла, баланс пересчитан", "w": 2200},
 ]},
 {"n": "Завести подкатегорию", "uc": "UC-11", "steps": [
  {"s": "A-04", "c": "Раздел «Ещё»", "w": 1000},
  {"s": "A-04", "c": "Касание «Категории»", "t": "text=Категории"},
  {"s": "D-03", "c": "Касание группы «Еда»", "t": "text=Еда", "w": 900},
  {"s": "D-07", "c": "Карточка группы: подкатегории и кнопка добавления", "w": 1500},
  {"s": "D-07", "c": "Касание «Добавить подкатегорию»", "t": "text=Добавить подкатегорию"},
  {"s": "C-02", "c": "Группа и вид подставлены, значок унаследован от группы, вводится только название", "w": 1600, "css": {".veil": "display:none", ".btn.danger": "display:none"},
   "set": {".hd h2": "Новая подкатегория", "text=Название": "", "text=Группа": "Еда"}},
  {"s": "C-02", "c": "Ввод названия", "t": "text=Название", "set": {"text=Название": "Пек"}, "w": 450},
  {"s": "C-02", "c": "Ввод названия", "set": {"text=Название": "Пекарня"}, "w": 700},
  {"s": "C-02", "c": "Сохранение — кнопкой под формой", "t": ".btn"},
  {"s": "D-07", "c": "Подкатегория появилась в группе", "w": 2000, "css": {"#d7-new": ""}},
 ]},
 {"n": "Завести группу", "uc": "UC-11", "steps": [
  {"s": "A-04", "c": "Раздел «Ещё»", "w": 900},
  {"s": "A-04", "c": "Касание «Категории»", "t": "text=Категории"},
  {"s": "D-03", "c": "Касание «+» в шапке", "t": ".hd .tool", "w": 700},
  {"s": "D-04", "c": "Вид и универсальность задаются здесь и потом не меняются", "w": 1500, "set": {"text=Название": "", "#d4-name": ""}},
  {"s": "D-04", "c": "Ввод названия", "t": "text=Название", "set": {"text=Название": "Питомцы", "#d4-name": "Питомцы"}, "w": 900},
  {"s": "D-04", "c": "«Прочее» будет создано автоматически — сказано до нажатия", "w": 1600},
  {"s": "D-04", "c": "Сохранение", "t": ".btn"},
  {"s": "D-03", "c": "Группа появилась в дереве", "w": 2000, "css": {"#d3-new": ""}},
 ]},
 {"n": "Навести порядок в категориях", "uc": "UC-23", "steps": [
  {"s": "A-04", "c": "Раздел «Ещё»", "w": 900},
  {"s": "A-04", "c": "Касание «Категории»", "t": "text=Категории"},
  {"s": "D-03", "c": "Касание подкатегории «Такси»", "t": "text=Такси", "w": 800},
  {"s": "C-02", "c": "Карточка подкатегории: имя, значок, группа", "w": 1400, "css": VEIL_OFF},
  {"s": "C-02", "c": "Переименование", "t": "text=Название", "set": {"text=Название": "Такси и каршеринг"}, "w": 1100},
  {"s": "C-02", "c": "Перенос в другую группу того же вида — список групп", "t": "text=Группа", "set": {"text=Группа": "Автомобиль"}, "w": 1400},
  {"s": "C-02", "c": "Сохранение", "t": ".btn"},
  {"s": "D-03", "c": "Подкатегория переехала под «Автомобиль». Операции не тронуты", "w": 1800, "set": {"text=Такси": "Такси и каршеринг"}},
  {"s": "D-03", "c": "Касание группы «Еда»", "t": "text=Еда"},
  {"s": "D-07", "c": "Карточка группы: имя и значок правятся, вид и универсальность заперты", "w": 1400},
  {"s": "D-07", "c": "Переименование", "t": "text=Название", "set": {"text=Название": "Еда и кафе"}, "w": 1100},
  {"s": "D-07", "c": "Сохранение", "t": ".btn"},
  {"s": "D-03", "c": "Группа переименована, подкатегории на месте", "w": 2000, "set": {"text=Еда": "Еда и кафе"}},
 ]},
 {"n": "Удалить подкатегорию", "uc": "UC-12", "steps": [
  {"s": "A-04", "c": "Раздел «Ещё»: справочники и настройки", "w": 1200},
  {"s": "A-04", "c": "Касание «Категории»", "t": "text=Категории"},
  {"s": "D-03", "c": "Дерево из двух уровней. «Прочее» неудаляемо", "w": 1600},
  {"s": "D-03", "c": "Касание «Такси»", "t": "text=Такси"},
  {"s": "C-02", "c": "Карточка подкатегории", "w": 1200, "css": VEIL_OFF},
  {"s": "C-02", "c": "Касание «Удалить подкатегорию»", "t": "text=Удалить подкатегорию", "css": VEIL_OFF},
  {"s": "C-02", "c": "Диалог называет число операций и куда они переедут", "w": 2600, "css": {".veil": ""}},
  {"s": "C-02", "c": "Касание «Удалить»", "t": ".dlg .yes"},
  {"s": "D-03", "c": "Операции перенесены в «Прочее» той же группы. Балансы не изменились", "w": 2600},
 ]},
 {"n": "Завести счёт", "uc": "UC-13", "steps": [
  {"s": "A-04", "c": "Раздел «Ещё»", "w": 900},
  {"s": "A-04", "c": "Касание «Счета»", "t": "text=Счета"},
  {"s": "D-01", "c": "Справочник счетов. Действующие, накопления, заблокированные", "w": 1500},
  {"s": "D-01", "c": "Касание «+» в шапке", "t": ".hd .tool"},
  {"s": "D-02", "c": "Тип и валюта — умолчания; вводятся название и остаток", "w": 1500,
   "set": {".hd h2": "Новый счёт", "text=Название": ""}},
  {"s": "D-02", "c": "Ввод названия", "t": "text=Название", "set": {"text=Название": "Вклад"}, "w": 900},
  {"s": "D-02", "c": "Сохранение", "t": ".btn"},
  {"s": "D-01", "c": "Счёт появился в списке", "w": 2000},
 ]},
 {"n": "Изменить счёт", "uc": "UC-14", "steps": [
  {"s": "A-04", "c": "Раздел «Ещё»", "w": 900},
  {"s": "A-04", "c": "Касание «Счета»", "t": "text=Счета"},
  {"s": "D-01", "c": "Касание счёта", "t": "text=Карта основная", "w": 800},
  {"s": "D-02", "c": "Валюта заперта: по счёту есть операции. Каждое ограничение объяснено рядом с полем", "w": 2200},
  {"s": "D-02", "c": "Касание «Скрытый»", "t": "text=Скрытый", "css": {".sw2:not(#d2close)": "background:var(--acc)", ".sw2:not(#d2close) i": "left:16px"}, "w": 1400},
  {"s": "D-02", "c": "Сохранение", "t": ".btn"},
  {"s": "D-01", "c": "Счёт переехал в накопления, операции не изменились", "w": 2000},
 ]},
 {"n": "Заблокировать ненужный счёт", "uc": "UC-21", "steps": [
  {"s": "A-01", "c": "Кредитка погашена и закрыта в банке. Баланс 0, но счёт торчит на главном экране", "w": 2000,
   "set": {"#a1-credit .num": "0,00 ₽"}, "css": {"#a1-credit .num": "color:var(--ink)"}},
  {"s": "A-01", "c": "Касание вкладки «Ещё»", "t": ".nav a:nth-child(4)"},
  {"s": "A-04", "c": "Касание «Счета»", "t": "text=Счета", "w": 700},
  {"s": "D-01", "c": "Касание «Карта кредитная»", "t": "text=Карта кредитная", "w": 800},
  {"s": "D-02", "c": "Карточка счёта", "w": 1200, "set": {"text=Название": "Карта кредитная", "text=Начальный остаток": "0"}},
  {"s": "D-02", "c": "Касание «Заблокированный». Подпись под переключателем называет последствие; снять блокировку можно здесь же", "t": "text=Заблокированный",
   "css": {"#d2close": "background:var(--acc)", "#d2close i": "left:16px"}, "w": 2000},
  {"s": "D-02", "c": "Сохранение. Остаток нулевой — подтверждать нечего", "t": ".btn"},
  {"s": "D-01", "c": "Счёт ушёл в раздел «Заблокированные». Операции и отчёт не изменились", "w": 2200,
   "css": {"#d1-credit": "display:none"}, "set": {"#d1-closed .t": "Карта кредитная"}},
  {"s": "D-01", "c": "Назад", "t": ".hd .act", "w": 600},
  {"s": "A-04", "c": "Касание вкладки «Балансы»", "t": ".nav a:nth-child(1)"},
  {"s": "A-01", "c": "На главном экране заблокированного счёта больше нет. В выборе счёта при вводе — тоже", "w": 2800,
   "css": {"#a1-credit": "display:none"}},
 ]},
 {"n": "Привести в порядок справочник мест", "uc": "UC-18", "steps": [
  {"s": "A-04", "c": "Раздел «Ещё»", "w": 900},
  {"s": "A-04", "c": "Касание «Места»", "t": "text=Места"},
  {"s": "D-05", "c": "Список по частоте. «Пятерочка» с тремя операциями — дубль «Пятёрочки»", "w": 2000},
  {"s": "D-05", "c": "Касание дубля", "t": "text=Пятерочка"},
  {"s": "C-08", "c": "Карточка места: число операций и категория. Слить два места нельзя — операции переносятся вручную", "w": 2200, "css": VEIL_OFF},
  {"s": "C-08", "c": "Касание «Удалить место»", "t": "text=Удалить место", "css": VEIL_OFF},
  {"s": "C-08", "c": "Диалог называет, сколько операций останется без места. Строки не правятся", "w": 2400, "css": {".veil": ""}},
  {"s": "C-08", "c": "Касание «Удалить»", "t": ".dlg .yes"},
  {"s": "D-05", "c": "Дубль исчез", "w": 1400, "css": {"#d5-dup": "display:none"}},
  {"s": "D-05", "c": "Касание «МТС»", "t": "text=МТС"},
  {"s": "C-08", "c": "Карточка места", "w": 1000, "css": VEIL_OFF,
   "set": {"text=Название": "МТС", "text=3 операции": "28 операций · чаще всего «Мобильная связь»"}},
  {"s": "C-08", "c": "Переименование правит все операции разом", "t": "text=Название", "set": {"text=Название": "МТС · связь"}, "w": 1200},
  {"s": "C-08", "c": "Сохранение", "t": ".btn"},
  {"s": "D-05", "c": "Имя изменилось во всех операциях, править их по одной не нужно", "w": 2400, "set": {"text=МТС": "МТС · связь"}},
 ]},
 {"n": "Упорядочить счета", "uc": "UC-15", "steps": [
  {"s": "A-04", "c": "Раздел «Ещё»", "w": 900},
  {"s": "A-04", "c": "Касание «Счета»", "t": "text=Счета"},
  {"s": "D-01", "c": "Порядок задаётся перетаскиванием за рукоятку слева. Он же — порядок на главном экране", "w": 2200},
  {"s": "D-01", "c": "Захват «Наличные»", "t": "text=Наличные", "w": 900},
  {"s": "D-01", "c": "Перетаскивание под «Карта основная»", "t": "text=Карта основная", "w": 1200,
   "set": {"text=Наличные": "Карта основная", "text=Карта основная": "Наличные"}},
  {"s": "D-01", "c": "Заблокированные счета не перетаскиваются: порядок нужен только действующим", "w": 2400},
 ]},
 {"n": "Разобраться, куда ушли деньги", "uc": "UC-19", "steps": [
  {"s": "A-01", "c": "Главный экран", "w": 1000},
  {"s": "A-01", "c": "Касание вкладки «Отчёт»", "t": ".nav a:nth-child(3)"},
  {"s": "E-01", "c": "Расходы за месяц по группам. Доли и полосы — для сравнения глазом", "w": 2200},
  {"s": "E-01", "c": "Касание «Еда»", "t": "text=Еда"},
  {"s": "E-02", "c": "Подкатегории группы. Доли пересчитаны внутри группы", "w": 2200},
  {"s": "E-02", "c": "Касание «Доставка еды»", "t": "text=Доставка еды"},
  {"s": "E-03", "c": "Сами операции — те же строки, что в ленте, другой разрез", "w": 2400},
  {"s": "E-03", "c": "Назад", "t": ".hd .act", "w": 600},
  {"s": "E-02", "c": "Назад", "t": ".hd .act", "w": 600},
  {"s": "E-01", "c": "Переключение месяца назад", "t": "#e1-prev"},
  {"s": "E-04", "c": "Месяц без трат: пустое состояние, а не список нулей", "w": 2600},
 ]},
 {"n": "Выбрать тему оформления", "uc": "UC-20", "steps": [
  {"s": "A-04", "c": "Раздел «Ещё»", "w": 1000},
  {"s": "A-04", "c": "Касание «Оформление»", "t": "text=Оформление"},
  {"s": "D-06", "c": "Три состояния: как в системе, светлая, тёмная", "w": 1600},
  {"s": "D-06", "c": "Касание «Тёмная»", "t": "text=Тёмная", "theme": "dark", "w": 1800},
  {"s": "D-06", "c": "Все экраны следуют теме: цвета заданы токенами, прямых значений нет", "w": 1800},
  {"s": "D-06", "c": "Назад", "t": ".hd .act", "w": 800},
  {"s": "A-04", "c": "Раздел «Ещё» в тёмной теме", "w": 1400},
  {"s": "A-04", "c": "Возврат к системной теме", "theme": "", "w": 1400},
 ]},
]

TEMPLATE = r"""<!doctype html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Личный трекер финансов — прототип</title>
<!-- СГЕНЕРИРОВАНО из mockups.html скриптом tools/build_prototype.py. Руками не править. -->
__FINGERPRINT__
<style>
__MOCKUP_CSS__

/* --- каркас прототипа ------------------------------------------------------ */
:root{--z:0.6}
body{padding:0}
.page{max-width:1080px;margin:0 auto;padding:34px 20px 60px}
.page h1{font-size:23px;font-weight:500;margin:0 0 5px}
.lede{color:var(--ink2);font-size:13.5px;margin:0 0 22px;max-width:70ch}
.lede b{font-weight:500;color:var(--ink);font-family:var(--mono);font-size:13px}
.bar{display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-bottom:14px}
.tab,.ctl{border:1px solid var(--line2);background:var(--card);border-radius:8px;padding:7px 13px;font-size:13.5px;cursor:pointer;font-family:inherit;color:var(--ink)}
.tab.on{background:var(--acc);color:var(--onacc);border-color:var(--acc)}
.tab small{font-family:var(--mono);font-size:10.5px;opacity:.7;margin-left:6px}
.ctl.wide{min-width:96px}
.sep{flex:1}
.capt{background:var(--card);border:1px solid var(--line2);border-radius:10px;padding:11px 15px;font-size:14px;min-height:44px;display:flex;align-items:center;gap:11px;margin-bottom:16px}
.capt .step{font-family:var(--mono);font-size:11.5px;color:var(--acc);background:var(--accbg);padding:2px 7px;border-radius:4px;flex:none}
.track{height:3px;background:var(--line2);border-radius:2px;margin:0 0 16px;overflow:hidden}
.track i{display:block;height:100%;background:var(--acc);width:0;transition:width .25s}
.meter{background:var(--card);border:1px solid var(--line2);border-radius:8px;padding:6px 12px;font-size:13px;color:var(--ink2);font-family:var(--mono)}
.meter b{color:var(--acc);font-weight:500}
.holder{width:498px;margin:0 auto;height:calc(1058px * var(--z));transition:height .2s}
.frame{width:498px;height:1058px;transform:scale(var(--z));transform-origin:top center;transition:transform .2s;background:var(--frame);border-radius:46px;padding:9px;position:relative;cursor:pointer}
.dev{position:relative;width:100%;height:100%;background:var(--card);border-radius:38px;overflow:hidden;display:flex;flex-direction:column}
.dev.nofx *{transition:none!important;animation:none!important}
.sb{height:36px;display:flex;align-items:center;justify-content:space-between;padding:0 26px;font-size:13.5px;font-family:var(--mono);color:var(--ink);flex:none}
.sb .r{display:flex;gap:6px;align-items:center}
.sb .dot{width:7px;height:7px;border-radius:4px;background:var(--ink)}
.stage{position:relative;flex:1;overflow:hidden}
.scr{position:absolute;inset:0;background:var(--card);transition:transform .34s cubic-bezier(.3,.7,.3,1);will-change:transform;zoom:1.4545}
.scr.right{transform:translateX(100%)}
.scr.down{transform:translateY(100%)}
.scr .phone{border:none;border-radius:0}
.scr .veil{z-index:2}
.rip{position:absolute;width:64px;height:64px;margin:-32px 0 0 -32px;border-radius:32px;background:var(--ripple);pointer-events:none;opacity:0;z-index:5}
.rip.go{animation:rp .55s ease-out}
@keyframes rp{0%{opacity:1;transform:scale(.3)}70%{opacity:.5}100%{opacity:0;transform:scale(1.15)}}
.hit{background:var(--acc-press)!important}
.legend{margin-top:22px;font-size:13px;color:var(--ink2);line-height:1.7;max-width:80ch}
.legend code{font-family:var(--mono);font-size:12.5px;background:var(--card);border:1px solid var(--line);padding:1px 5px;border-radius:4px}
</style>
</head>
<body>
__SPRITE__

<div class="page">
<h1>Анимированный прототип — итерация 5</h1>
<p class="lede">Экраны взяты из <b>mockups.html</b> без изменений, файл собирается скриптом <b>tools/build_prototype.py</b> — править нужно макеты, а не этот файл. Вкладки названы по сценариям из <b>docs/use-cases.md</b>. Экран — Samsung Galaxy S25 Ultra: <b>480 × 1040</b> логических пикселей; макеты нарисованы в масштабе 330 × 690 и увеличены в 480/330 раза.</p>

<div class="bar" id="tabs"></div>
<div class="bar">
  <button class="ctl wide" id="pp">Пауза</button>
  <button class="ctl" id="bk">◂ шаг</button>
  <button class="ctl" id="fw">шаг ▸</button>
  <button class="ctl" id="rs">Сначала</button>
  <button class="ctl" id="sp">1×</button>
  <button class="ctl" id="zm">60%</button>
  <button class="ctl" id="th">Тема</button>
  <span class="sep"></span>
  <span class="meter">касаний по интерфейсу: <b id="m-ui">0</b> · цифр: <b id="m-key">0</b></span>
</div>

<div class="capt"><span class="step" id="stepno">1 / 1</span><span id="captxt">Загрузка…</span></div>
<div class="track"><i id="trackbar"></i></div>

<div class="holder"><div class="frame" id="frame"><div class="dev" id="dev">
<div class="sb"><span>21:40</span><div class="r"><span class="dot"></span><span class="dot"></span><span class="dot"></span></div></div>
<div class="stage" id="stage">
__SCREENS__
<div class="rip" id="rip"></div>
</div>
</div></div></div>

<p class="legend">Зелёный круг — касание. Счётчик считает касания по интерфейсу отдельно от набора цифр: первое зависит от раскладки экранов, второе — от суммы. С клавиатуры: <code>пробел</code> — пауза, <code>←</code> и <code>→</code> — шаг, цифры — первые десять сценариев. Клик по кадру ставит на паузу. Экранов в прототипе столько же, сколько в макетах: __N_SCREENS__.</p>
</div>

<script>
const SC=__SCENARIOS__;
const SHEETS=__SHEETS__;
const stage=document.getElementById('stage'), snapshot=stage.innerHTML;
const cap=document.getElementById('captxt'), stepno=document.getElementById('stepno');
const trackbar=document.getElementById('trackbar'), mUi=document.getElementById('m-ui'), mKey=document.getElementById('m-key');
let timer=null,steps=[],idx=0,playing=true,speed=1,cur=0,uiTaps=0,keyTaps=0,silent=false,shown=null;
const q=id=>document.getElementById(id);
const scr=id=>stage.querySelector('[data-screen="'+id+'"]');

function show(id){
  stage.querySelectorAll('.scr').forEach(el=>{
    const k=el.dataset.screen;
    if(k===id) el.classList.remove('right','down');
    else if(!el.classList.contains('right')&&!el.classList.contains('down'))
      el.classList.add(SHEETS.includes(k)?'down':'right');
  });
  shown=id;
}
function find(root,spec){
  if(!spec) return null;
  if(spec.startsWith('text=')){
    const t=spec.slice(5); let best=null;
    root.querySelectorAll('*').forEach(el=>{
      if(el.children.length>3) return;
      const own=(el.textContent||'').trim();
      if(own===t||own.startsWith(t)){ if(!best||el.textContent.length<=best.textContent.length) best=el; }
    });
    return best;
  }
  return root.querySelector(spec);
}
// куда писать текст: подпись поля → его значение, заголовок строки с суммой → сумма,
// дата в шапке дня → итог дня; иначе — сам элемент
function valueOf(el){
  if(el.classList.contains('k')||el.classList.contains('t')){
    const box=el.closest('.fld,.row'); const v=box&&box.querySelector('.v,.num'); if(v) return v;
  }
  if(el.parentElement&&el.parentElement.classList.contains('date')&&el.nextElementSibling) return el.nextElementSibling;
  return el;
}
function counters(){mUi.textContent=uiTaps;mKey.textContent=keyTaps}
function ripple(el){
  if(!el||silent) return;
  const r=q('rip'), a=el.getBoundingClientRect(), b=stage.getBoundingClientRect();
  const kx=stage.offsetWidth/b.width, ky=stage.offsetHeight/b.height;
  r.style.left=((a.left+a.width/2-b.left)*kx)+'px'; r.style.top=((a.top+a.height/2-b.top)*ky)+'px';
  r.classList.remove('go'); void r.offsetWidth; r.classList.add('go');
  el.classList.add('hit'); setTimeout(()=>el.classList.remove('hit'),170);
}
function apply(st){
  if(st.s&&st.s!==shown) show(st.s);
  const root=scr(st.s||shown); if(!root) return;
  if(st.t){ uiTaps++; const el=find(root,st.t); if(!el) console.warn('нет цели', st.s, st.t); ripple(el); }
  if(st.k){ keyTaps++; ripple([...root.querySelectorAll('.kp b')].find(b=>b.textContent.trim()===st.k)); }
  if(st.set) for(const [sel,txt] of Object.entries(st.set)){
    const el=find(root,sel); if(!el){ console.warn('нет элемента', st.s, sel); continue; }
    const v=valueOf(el); v.textContent=txt; v.classList.remove('ask','none');
  }
  if(st.css) for(const [sel,style] of Object.entries(st.css)){
    const list=sel.startsWith('text=')?[find(root,sel)]:[...root.querySelectorAll(sel)];
    list.forEach(el=>{ if(el) el.style.cssText=style; });
  }
  if('theme' in st) document.documentElement.dataset.theme=st.theme;
  counters();
}
function reset(){
  clearTimeout(timer); stage.innerHTML=snapshot; idx=0; uiTaps=0; keyTaps=0; counters(); shown=null;
  stage.querySelectorAll('.scr').forEach(el=>el.classList.add('right'));
  steps=SC[cur].steps; show(steps[0].s);
}
function paint(){stepno.textContent=idx+' / '+steps.length;trackbar.style.width=(idx/steps.length*100)+'%'}
function run(){
  if(!playing)return;
  if(idx>=steps.length){reset();timer=setTimeout(run,800/speed);return}
  const s=steps[idx++];
  try{apply(s)}catch(e){console.warn(e)}
  cap.textContent=s.c; paint();
  timer=setTimeout(run,(s.w||700)/speed);
}
function jump(n){
  clearTimeout(timer); playing=false; q('pp').textContent='Продолжить';
  n=Math.max(1,Math.min(steps.length,n));
  silent=true; stage.classList.add('nofx'); reset();
  for(let i=0;i<n;i++){try{apply(steps[i])}catch(e){}}
  idx=n; cap.textContent=steps[n-1].c; paint();
  void stage.offsetWidth; stage.classList.remove('nofx'); silent=false;
}
function start(i){
  cur=i; document.querySelectorAll('.tab').forEach((t,n)=>t.classList.toggle('on',n===i));
  document.documentElement.dataset.theme=sysTheme();
  reset(); playing=true; q('pp').textContent='Пауза'; paint(); timer=setTimeout(run,450/speed);
}
function toggle(){playing=!playing;q('pp').textContent=playing?'Пауза':'Продолжить';if(playing)run();else clearTimeout(timer)}
const sysTheme=()=>matchMedia('(prefers-color-scheme: dark)').matches?'dark':'';
const tabs=q('tabs');
SC.forEach((sc,i)=>{const b=document.createElement('button');b.className='tab'+(i?'':' on');b.innerHTML=sc.n+'<small>'+sc.uc+'</small>';b.onclick=()=>start(i);tabs.appendChild(b)});
q('pp').onclick=toggle; q('rs').onclick=()=>start(cur);
q('bk').onclick=()=>jump(idx-1); q('fw').onclick=()=>jump(idx+1);
q('frame').onclick=toggle;
q('sp').onclick=function(){speed=speed===1?1.75:speed===1.75?0.6:1;this.textContent=speed===0.6?'0,6×':speed===1?'1×':'1,75×'};
q('zm').onclick=function(){const z=getComputedStyle(document.documentElement).getPropertyValue('--z').trim();
  const nz=z==='0.6'?'1':'0.6';document.documentElement.style.setProperty('--z',nz);this.textContent=nz==='1'?'100%':'60%'};
document.addEventListener('keydown',e=>{
  if(e.key===' '){e.preventDefault();toggle()}
  else if(e.key==='ArrowLeft')jump(idx-1);
  else if(e.key==='ArrowRight')jump(idx+1);
  else if(e.key>='1'&&e.key<='9'&&SC[+e.key-1])start(+e.key-1);
  else if(e.key==='0'&&SC[9])start(9);
});
q('th').onclick=()=>{const r=document.documentElement;r.dataset.theme=r.dataset.theme==='dark'?'':'dark'};
document.documentElement.dataset.theme=sysTheme();
start(0);
</script>
</body>
</html>
"""


def fingerprint(path):
    """Отпечаток источника сборки.

    Переводы строк приводятся к одному виду, а метка порядка байтов отбрасывается:
    иначе отпечаток зависел бы от настроек checkout, и у второго разработчика
    сборка считалась бы устаревшей на ровном месте.
    """
    text = path.read_text(encoding="utf-8-sig").replace(CRLF, LF)
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def build():
    src = SRC.read_text(encoding="utf-8")
    css = re.search(r"<style>(.*?)</style>", src, re.S).group(1).strip()
    sprite = re.search(r"<svg[^>]*>\s*<defs>.*?</defs>\s*</svg>", src, re.S).group(0)
    screens, texts = [], {}
    for fig in re.findall(r"<figure class=\"unit\">(.*?)</figure>", src, re.S):
        sid = re.search(r'<span class="sid">([A-Z]-\d\d)</span>', fig).group(1)
        title = re.search(r"<b>(.*?)</b>", fig).group(1)
        body = fig[fig.index('<div class="phone">'):].rstrip()
        screens.append(f'<div class="scr right" data-screen="{sid}" title="{sid} · {title}">\n{body}\n</div>')
        texts[sid] = re.sub(r"<[^>]+>", " ", body)
    for sc in SCENARIOS:
        dynamic = set()
        for st in sc["steps"]:
            assert st["s"] in texts, f"сценарий «{sc['n']}» ссылается на несуществующий экран {st['s']}"
            # цель касания text=… должна существовать в разметке экрана, если её не подставил set
            for spec in [st.get("t")] + list((st.get("set") or {}).keys()) + list((st.get("css") or {}).keys()):
                if spec and spec.startswith("text="):
                    lit = spec[5:]
                    if lit not in texts[st["s"]] and (st["s"], lit) not in dynamic:
                        print(f"  предупреждение: «{sc['n']}» шаг «{st['c']}»: на {st['s']} нет текста «{lit}»")
            for spec, val in (st.get("set") or {}).items():
                dynamic.add((st["s"], val))
    # Отпечаток берётся и со сборщика: сценарии и шаблон живут в нём, значит
    # его правка меняет прототип так же, как правка макетов
    stamp = (f"<!-- отпечатки источников: mockups.html={fingerprint(SRC)} "
             f"build_prototype.py={fingerprint(pathlib.Path(__file__))} -->")

    out = (TEMPLATE
           .replace("__FINGERPRINT__", stamp)
           .replace("__MOCKUP_CSS__", css)
           .replace("__SPRITE__", sprite)
           .replace("__SCREENS__", "\n\n".join(screens))
           .replace("__N_SCREENS__", str(len(screens)))
           .replace("__SCENARIOS__", json.dumps(SCENARIOS, ensure_ascii=False))
           .replace("__SHEETS__", json.dumps(SHEETS)))
    return out, len(screens)


if __name__ == "__main__":
    html, n = build()
    if "--check" in sys.argv:
        if not DST.exists() or DST.read_text(encoding="utf-8") != html:
            print("prototype.html устарел: запустите python tools/build_prototype.py")
            sys.exit(1)
        print(f"prototype.html актуален, экранов {n}")
    else:
        DST.write_text(html, encoding="utf-8")
        print(f"prototype.html собран: экранов {n}, сценариев {len(SCENARIOS)}")
