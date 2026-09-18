"""Sobiraet stranicu analitiki kategoriy Wallet iz dampa.

Zapusk: python tasks/wallet-import/tools/wallet_page.py
Rezultat: artifacts/categories.html ryadom s zadachey - vne repozitoriya, tam summy.

Chernovik sopostavleniya lezhit v PLAN nizhe dannymi: kategoriya Wallet ->
(reshenie, nasha podkategoriya). Eto ne kod importa, a material dlya razgovora.
"""
import html
import io
import os
from collections import Counter, defaultdict
from datetime import datetime, timedelta
from decimal import Decimal

from wallet_dump import DATA, GROUPS, kind_of, load, money

OUT = os.path.join(DATA, "categories.html")
MSK = 3  # sdvig mestnogo vremeni ot UTC, proveren sverkoy s ekranom

PLACE, KEEP, NEW, TRANSFER = "place", "keep", "new", "transfer"

# Chernovik resheniya po kategoriyam Wallet.
PLAN = {
    "Перевести, снять": (TRANSFER, "перевод — категория не нужна"),

    "Пятерочка": (PLACE, "Еда / Продукты"),
    "Магнит": (PLACE, "Еда / Продукты"),
    "Спар": (PLACE, "Еда / Продукты"),
    "ВкусВилл": (PLACE, "Еда / Продукты"),
    "Ашан": (PLACE, "Еда / Продукты"),
    "Лента": (PLACE, "Еда / Продукты"),
    "Перекресток": (PLACE, "Еда / Продукты"),
    "Дикси": (PLACE, "Еда / Продукты"),
    "Фикс прайс": (PLACE, "Покупки / Дом"),
    "Красное и Белое": (PLACE, "Еда / Алкоголь"),
    "Винлаб": (PLACE, "Еда / Алкоголь"),
    "Озон": (PLACE, "Покупки / Маркетплейсы и техника"),
    "Вайлберис": (PLACE, "Покупки / Маркетплейсы и техника"),
    "Яндекс Маркет": (PLACE, "Покупки / Маркетплейсы и техника"),
    "Детский Мир": (PLACE, "Дети / Детские товары"),
    "Спортмастер": (PLACE, "Покупки / Одежда и обувь"),
    "Бургер Кинг": (PLACE, "Еда / Кафе и рестораны"),
    "КФС": (PLACE, "Еда / Кафе и рестораны"),
    "Вкусно и точка": (PLACE, "Еда / Кафе и рестораны"),
    "Теремок": (PLACE, "Еда / Кафе и рестораны"),
    "Пончики": (PLACE, "Еда / Кафе и рестораны"),
    "Индейки Дом": (PLACE, "Еда / Кафе и рестораны"),
    "МТС": (PLACE, "Связь и подписки / Мобильная связь"),
    "Яндекс": (PLACE, "Связь и подписки / Подписки и приложения"),
    "Сбербанк": (PLACE, "Финансы / Комиссии банка"),
    "Мультифактор": (PLACE, "Доход / Зарплата"),
    "Пэйча": (PLACE, "Доход / Зарплата"),
    "Мисис": (PLACE, "Доход / Зарплата"),
    "Импульс": (PLACE, "Доход / Зарплата"),
    "Лига": (PLACE, "Доход / Зарплата"),

    "Метро": (KEEP, "Транспорт / Общественный транспорт"),
    "Автобус": (KEEP, "Транспорт / Общественный транспорт"),
    "Общественный транспорт": (KEEP, "Транспорт / Общественный транспорт"),
    "Такси": (KEEP, "Транспорт / Такси"),
    "Поезд": (KEEP, "Транспорт / Самолёт и поезд"),
    "Самолет": (KEEP, "Транспорт / Самолёт и поезд"),
    "Бизнес ланч": (KEEP, "Еда / Бизнес-ланч"),
    "Кофе": (KEEP, "Еда / Кофе"),
    "Еда и напитки": (KEEP, "Еда / Продукты"),
    "Магазины, вендинг": (KEEP, "Еда / Продукты"),
    "Аптека": (KEEP, "Покупки / Аптека"),
    "Разница": (KEEP, "Служебное / Разница"),
    "Доктор": (KEEP, "Здоровье / Врачи"),
    "Зубной": (KEEP, "Здоровье / Стоматология"),
    "Стрижка": (KEEP, "Здоровье / Стрижка"),
    "Анализы": (KEEP, "Здоровье / Анализы"),
    "Др": (KEEP, "Покупки / Подарки и цветы"),
    "Цветы": (KEEP, "Покупки / Подарки и цветы"),
    "Подарки": (KEEP, "Покупки / Подарки и цветы"),
    "Подарки и благотворительность": (KEEP, "Отдых / Благотворительность"),
    "На фронт": (KEEP, "Отдых / Благотворительность"),
    "Займы, проценты": (KEEP, "Финансы / Проценты по займам"),
    "Перевод Жене": (KEEP, "Финансы / Переводы близким"),
    "Финансовые расходы": (KEEP, "Финансы / Прочее"),
    "Страхование": (KEEP, "Финансы / Страхование"),
    "Диме на счет": (KEEP, "Дети / Карманные и на счёт"),
    "Игровая": (KEEP, "Дети / Игровые и развлечения"),
    "Дети": (KEEP, "Дети / Детские товары"),
    "Садик": (KEEP, "Дети / Садик"),
    "Возврат денег (налог, покупка)": (KEEP, "Доход / Возвраты и вычеты"),
    "Интересы, дивиденды": (KEEP, "Доход / Проценты и дивиденды"),
    "Родители": (KEEP, "Доход / Поступления от близких"),
    "Доход": (KEEP, "Доход / Прочее"),
    "Аренда": (KEEP, "Жильё / Аренда"),
    "Ипотека": (KEEP, "Жильё / Ипотека"),
    "Электричество, коммунальные услуги": (KEEP, "Жильё / Коммунальные услуги"),
    "Интернет": (KEEP, "Связь и подписки / Интернет"),
    "Телефон": (KEEP, "Связь и подписки / Мобильная связь"),
    "Телеграм премиум": (KEEP, "Связь и подписки / Подписки и приложения"),
    "Одежда и обувь": (KEEP, "Покупки / Одежда и обувь"),
    "Дом и сад": (KEEP, "Покупки / Дом"),
    "Маркетплэйсы, электроника": (KEEP, "Покупки / Маркетплейсы и техника"),
    "Культура, спортивные мероприятия": (KEEP, "Отдых / Концерты и матчи"),
    "Хоккей билеты": (KEEP, "Отдых / Концерты и матчи"),
    "Отдых": (KEEP, "Отдых / Отпуск и поездки"),
    "Отпуск, поездки, отели": (KEEP, "Отдых / Отпуск и поездки"),
    "Парковка": (KEEP, "Автомобиль / Парковка"),
    "Прочее": (KEEP, "Доход / Прочее"),

    "Самокат": (NEW, "Транспорт / Самокат и аренда"),
    "Алименты": (NEW, "Доход / Алименты"),
    "Кредит, аренда": (NEW, "Доход / Сдача и аренда"),
    "Транспортное средство": (NEW, "Автомобиль / Покупка и продажа"),
}

# Bezymyannye standartnye kategorii Wallet: reshenie po nomeru konverta.
BY_ENVELOPE = {
    1000: (KEEP, "Еда / Продукты"),
    1001: (KEEP, "Еда / Кафе и рестораны"),
    2010: (KEEP, "Покупки / Прочее"),
    10000: (KEEP, "Доход / Прочее"),
    20000: (TRANSFER, "перевод — категория не нужна"),
}

PROPOSALS = [
    ("Алкоголь", "Еда", "Винлаб и Красное и Белое — 152 операции",
     "Средний чек на порядок выше продуктового: это не продукты между делом. "
     "Без своей строки статья растворится в «Продуктах» и пропадёт из отчёта."),
    ("Самокат и аренда", "Транспорт", "227 операций, чек мелкий",
     "Кикшеринг. «Общественный транспорт» — не он: там проезд по тарифу, "
     "а здесь поминутная аренда."),
    ("Алименты", "Доход", "38 операций",
     "Регулярная и крупная статья дохода. «Поступления от близких» её маскируют."),
    ("Сдача и аренда", "Доход", "57 операций",
     "Доходной аренды в наборе нет вовсе."),
    ("Покупка и продажа", "Автомобиль", "15 операций",
     "Третья строка по деньгам за всю историю. Сейчас упала бы в «Прочее» "
     "и утянула бы за собой весь месяц в отчёте."),
    ("Снеки и вендинг", "Еда", "204 операции — под вопросом",
     "Живая статья (81 операция за два года), но её можно оставить и в «Продуктах»: "
     "решение о дробности, а не о пропаже данных."),
]

EMPTY_BRANCHES = [
    ("Автомобиль", "Топливо, Обслуживание и ремонт, Страховка и налоги — ноль операций за восемь лет; "
                   "Парковка — три. Машины нет"),
    ("Отдых", "Фотосессии, Массаж и спа — ноль"),
    ("Доход", "Продажа вещей — ноль"),
    ("Дети", "Кружки и секции — ноль"),
    ("Еда", "Доставка еды — ноль: заказы записывались кафе и ресторанами"),
]

RULES = [
    ("Переводы сворачиваются в одну операцию",
     "В Wallet перевод — это две записи с общим ключом: 842 записи дали ровно 421 пару. "
     "У нас перевод — одна операция с двумя счетами. Сворачивать по ключу перевода, "
     "а не по совпадению суммы и даты."),
    ("Безымянные категории разбираются по номеру конверта",
     "821 операция лежит в стандартных категориях Wallet, у которых названия нет в документе. "
     "Разбирать руками не нужно: конверт 1000 — продукты, 1001 — кафе и рестораны, "
     "2010 — прочие покупки. Видно по заметкам: вода и мороженое против пиццы и хинкали."),
    ("«Разница» ложится на служебную группу",
     "261 операция: 202 расхода и 59 доходов — ровно две наши служебные подкатегории."),
    ("Работодатели становятся местами",
     "Пять доходных категорий — названия компаний. Все едут в «Доход / Зарплата», "
     "различаясь местом."),
    ("Дата открытия счёта выводится из первой операции",
     "В Wallet её нет, а у нас операция раньше открытия счёта отвергается доменным правилом."),
]


# Gruppy Wallet po-russki: stranica russkaya, a taksonomiya u nih angliyskaya.
GROUP_RU = {
    "Food & Drinks": "Еда и напитки", "Transportation": "Транспорт", "Shopping": "Покупки",
    "Transfers": "Переводы", "Financial expenses": "Финансовые расходы",
    "Life & Entertainment": "Жизнь и развлечения", "Income": "Доход",
    "Communication, PC": "Связь и техника", "Vehicle": "Транспортное средство",
    "Housing": "Жильё", "Investments": "Вложения", "Others": "Прочее",
}


def group_ru(name):
    return GROUP_RU.get(name, name)


def spaced(number):
    """Razryady nerazryvnym probelom, kak v samom prilozhenii."""
    return "{:,}".format(int(number)).replace(",", "\u00a0")


def plural(number, one, few, many):
    """Russkaya schetnaya forma: 1 operaciya, 2 operacii, 5 operaciy."""
    tail, hundred = number % 10, number % 100
    if 11 <= hundred <= 14 or tail == 0 or tail >= 5:
        return many
    return one if tail == 1 else few


def ops_text(number):
    return "%s %s" % (spaced(number), plural(number, "операция", "операции", "операций"))


def records_text(number):
    return "%s %s" % (spaced(number), plural(number, "запись", "записи", "записей"))


def money_text(value):
    whole = int(value)
    return "{:,}".format(whole).replace(",", " ") + " ₽"


def decide(wallet, cid):
    name = wallet.name(cid)
    if name and name in PLAN:
        return PLAN[name]
    envelope = wallet.categories.get(cid, {}).get("envelopeId", 0)
    if not name and envelope in BY_ENVELOPE:
        return BY_ENVELOPE[envelope]
    if envelope // 1000 == 20:
        return PLAN["Перевести, снять"]
    return (None, "не разобрано")


def collect(wallet):
    per = {}
    for rec in wallet.records:
        cid = rec.get("categoryId")
        box = per.setdefault(cid, {
            "id": cid, "count": 0, "sum": Decimal(0), "kinds": Counter(),
            "name": wallet.name(cid), "envelope": wallet.categories.get(cid, {}).get("envelopeId", 0),
        })
        box["count"] += 1
        box["sum"] += money(rec.get("amount", 0))
        box["kinds"][kind_of(rec)] += 1
    for box in per.values():
        box["decision"], box["target"] = decide(wallet, box["id"])
        box["group"] = GROUPS.get(box["envelope"] // 1000, "—")
        box["title"] = box["name"] or "без имени, конверт %d" % box["envelope"]
    return sorted(per.values(), key=lambda b: -b["count"])


def squeeze(wallet):
    """Skolko zapisey ostanetsya, esli szhat operacii po mesyacam."""
    raw, with_place, without = set(), set(), set()
    months = set()
    for rec in wallet.records:
        when = datetime.strptime(rec["recordDate"][:19], "%Y-%m-%dT%H:%M:%S") + timedelta(hours=MSK)
        month = when.strftime("%Y-%m")
        months.add(month)
        kind = kind_of(rec)
        cid = rec.get("categoryId")
        envelope = wallet.categories.get(cid, {}).get("envelopeId", 0)
        raw.add(rec["_id"])
        with_place.add((month, cid, rec.get("accountId"), kind))
        without.add((month, envelope, rec.get("accountId"), kind))
    return len(raw), len(with_place), len(without), len(months)


def bar(share, tip, color="var(--acc)"):
    return ('<span class="bar" data-tip="%s"><span class="fill" style="width:%.1f%%;background:%s"></span></span>'
            % (html.escape(tip), max(share * 100, 1.2), color))


def main():
    wallet = load()
    rows = collect(wallet)
    total_ops = len(wallet.records)
    raw, with_place, without, months = squeeze(wallet)

    used = [r for r in rows if r["count"]]
    running, enough = 0, 0
    for row in used:
        running += row["count"]
        enough += 1
        if running >= total_ops * 0.9:
            break

    by_decision = Counter()
    ops_by_decision = Counter()
    for row in used:
        by_decision[row["decision"] or "open"] += 1
        ops_by_decision[row["decision"] or "open"] += row["count"]

    groups = defaultdict(lambda: {"count": 0, "sum": Decimal(0)})
    for row in used:
        box = groups[row["group"]]
        box["count"] += row["count"]
        box["sum"] += row["sum"]
    group_rows = sorted(groups.items(), key=lambda kv: -kv[1]["count"])
    top_group = group_rows[0][1]["count"]

    places = sorted([r for r in used if r["decision"] == PLACE], key=lambda r: -r["count"])

    out = []
    add = out.append
    add(HEAD)

    add('<header class="head"><div>')
    add('<h1>Категории Wallet: что переносим и чего не хватает</h1>')
    add('<p class="sub">Выгрузка от 18 сентября 2026 · %s · %s — %s · '
        'источник: локальная база веб-версии Wallet</p>' % (
            ops_text(total_ops),
            min(r["recordDate"][:7] for r in wallet.records),
            max(r["recordDate"][:7] for r in wallet.records)))
    add('</div><button class="theme" onclick="flip()">Переключить тему</button></header>')

    add('<section class="tiles">')
    kat = ("категория", "категории", "категорий")
    for value, label in (
        (total_ops, plural(total_ops, "операция", "операции", "операций") + " за восемь лет"),
        (len(used), plural(len(used), *kat) + " Wallet в деле"),
        (enough, plural(enough, *kat) + " — это 90% операций"),
        (len(places), plural(len(places), *kat) + " — на самом деле места"),
        (ops_by_decision["open"], plural(ops_by_decision["open"], "операция", "операции", "операций")
         + " ещё без решения"),
    ):
        add('<div class="tile"><div class="num">%s</div><div class="cap">%s</div></div>'
            % (spaced(value), label))
    add('</section>')

    add('<section><h2>Главный вывод: две трети «категорий» — это магазины</h2>')
    add('<p class="lead">В Wallet категорией названо место покупки: Пятёрочка, Озон, Детский Мир, '
        'МТС. У нас для этого есть справочник мест, и дерево категорий намеренно двухуровневое. '
        'Значит, таблица соответствий двухколоночная: категория Wallet превращается в '
        '<b>подкатегорию плюс место</b>.</p>')
    add('<div class="split">')
    for key, title, note in (
        (PLACE, "станут местом", "магазин, банк, работодатель"),
        (KEEP, "лягут на наш набор", "подкатегория уже есть"),
        (NEW, "просят новую подкатегорию", "смысла в наборе нет"),
        (TRANSFER, "переводы", "категория не нужна вовсе"),
    ):
        add('<div class="card %s"><div class="num">%s</div><div class="cap">%s</div>'
            '<div class="hint">%s · %s</div></div>'
            % (key, spaced(by_decision[key]), title, note, ops_text(ops_by_decision[key])))
    add('</div></section>')

    add('<section><h2>Группы Wallet по весу</h2>')
    add('<p class="lead">Число операций; сумма подписана рядом. Еда и транспорт — половина всей истории.</p>')
    add('<div class="chart">')
    for name, box in group_rows:
        add('<div class="row"><span class="label">%s</span>%s'
            '<span class="value">%s <i>%s</i></span></div>'
            % (html.escape(group_ru(name)), bar(box["count"] / top_group,
                                      "%s: %s, %s" % (group_ru(name), ops_text(box["count"]), money_text(box["sum"]))),
               spaced(box["count"]), money_text(box["sum"])))
    add('</div></section>')

    add('<section><h2>Тридцать самых частых категорий</h2>')
    add('<p class="lead">Решение по каждой — черновик: он и есть предмет разговора.</p>')
    add('<table><thead><tr><th>Категория Wallet</th><th>Группа</th><th class="n">Операций</th>'
        '<th class="n">Сумма</th><th>Решение</th><th>Куда у нас</th></tr></thead><tbody>')
    top_count = used[0]["count"]
    for row in used[:30]:
        mark = {PLACE: "место", KEEP: "есть", NEW: "новая", None: "открыто"}.get(row["decision"], "перевод")
        add('<tr><td class="name">%s%s</td><td class="dim">%s</td>'
            '<td class="n">%s %s</td><td class="n dim">%s</td>'
            '<td><span class="tag %s">%s</span></td><td class="dim">%s</td></tr>'
            % (html.escape(row["title"]),
               '' if row["name"] else ' <i class="dim">стандартная</i>',
               html.escape(group_ru(row["group"])),
               bar(row["count"] / top_count, "%s: %s" % (row["title"], ops_text(row["count"]))),
               spaced(row["count"]), money_text(row["sum"]),
               row["decision"] or "open", mark, html.escape(row["target"])))
    add('</tbody></table></section>')

    add('<section><h2>Чего в нашем наборе нет</h2><div class="props">')
    for name, group, volume, why in PROPOSALS:
        add('<div class="prop"><div class="prop-h"><b>%s</b><span class="dim">в группу «%s»</span></div>'
            '<div class="vol">%s</div><p>%s</p></div>' % (name, group, volume, why))
    add('</div></section>')

    add('<section><h2>Что у нас есть и восемь лет не понадобилось</h2>')
    add('<p class="lead">Удалять не предлагаю — в стартовом наборе эти строки уместны. '
        'Но знать стоит: после переноса ветки останутся пустыми.</p><ul class="plain">')
    for group, note in EMPTY_BRANCHES:
        add('<li><b>%s.</b> %s</li>' % (group, note))
    add('</ul></section>')

    add('<section><h2>Сжатие операций по месяцам</h2>')
    add('<p class="lead">Одна запись на «месяц + подкатегория + счёт + вид» вместо каждой покупки. '
        'Сколько записей останется — зависит от того, сохраняем ли место.</p>')
    add('<div class="chart wide">')
    for label, value, note, color in (
        ("Как есть", raw, "каждая покупка отдельной операцией", "var(--ink3)"),
        ("Сжать, место сохранить", with_place, "«Пятёрочка, сентябрь» отдельно от «Магнит, сентябрь»", "var(--place)"),
        ("Сжать без места", without, "все продукты месяца одной строкой", "var(--new)"),
    ):
        add('<div class="row"><span class="label">%s</span>%s<span class="value">%s <i>%.0f в месяц</i></span></div>'
            % (label, bar(value / raw, "%s: %s" % (label, records_text(value)), color),
               spaced(value), value / months))
    add('</div>')
    add('<p class="note"><b>Что теряется вместе с отдельными операциями.</b> Заметки — их 1620. '
        'Дни внутри месяца: лента за прошлые годы станет списком итогов, а не покупок, '
        'и «вчерашняя трата» в истории не найдётся. Панель частых подкатегорий считает частоту '
        'за три месяца по числу операций — после сжатия у каждой подкатегории будет ровно одна '
        'операция в месяц, и панель начнёт показывать не то, чем пользуются чаще, а всё подряд. '
        'Это не довод против сжатия, а список того, что нужно решить до него: например, сжимать '
        'только годы до прошлого, а последние двенадцать месяцев переносить как есть.</p>')
    add('</section>')

    add('<section><h2>Правила переноса, которые уже видны</h2><ol class="rules">')
    for title, note in RULES:
        add('<li><b>%s.</b> %s</li>' % (title, note))
    add('</ol></section>')

    add('<footer><p>Страница собрана скриптом <code>tasks/wallet-import/tools/wallet_page.py</code> '
        'из выгрузки базы Wallet. Данные и эта страница лежат в <code>tasks/wallet-import/artifacts/</code> '
        'и в репозиторий не попадают: это личные финансы. Как снять выгрузку заново — '
        '<code>tasks/wallet-import/extraction.md</code>.</p></footer>')
    add(TAIL)

    with io.open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out))
    print("-> %s" % OUT)
    print("razobrano: %d operaciy iz %d" % (total_ops - ops_by_decision["open"], total_ops))


HEAD = """<!doctype html>
<html lang="ru" data-theme="light">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Категории Wallet: анализ и предложения</title>
<style>
:root{
  --paper:#EDEFF1; --card:#FFFFFF; --sunk:#F4F6F7;
  --ink:#16191C; --ink2:#4C535A; --ink3:#676D74;
  --line:#E3E6E9; --line2:#CBD1D6;
  --acc:#0F5C4C;
  --keep:#00806A; --place:#2563C9; --new:#B45309;
  --keepbg:#E2EFEB; --placebg:#E4ECF9; --newbg:#F8EDE2;
  --sans:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif;
}
[data-theme="dark"]{
  --paper:#101315; --card:#1C2124; --sunk:#252B2F;
  --ink:#E7EAEC; --ink2:#A8AFB5; --ink3:#8A9299;
  --line:#2F373C; --line2:#3A4147;
  --acc:#48A78E;
  --keep:#2A9C83; --place:#5590E0; --new:#B9862F;
  --keepbg:#1B342D; --placebg:#1B2739; --newbg:#332818;
}
*{box-sizing:border-box}
body{margin:0;background:var(--paper);color:var(--ink);font:15px/1.55 var(--sans);
  -webkit-font-smoothing:antialiased}
.head{max-width:1040px;margin:0 auto;padding:36px 24px 8px;display:flex;gap:16px;
  align-items:flex-start;justify-content:space-between}
h1{font-size:27px;line-height:1.25;margin:0 0 6px}
h2{font-size:19px;margin:0 0 4px}
.sub{color:var(--ink3);margin:0;font-size:13px}
.theme{background:var(--card);color:var(--ink2);border:1px solid var(--line2);border-radius:8px;
  padding:8px 12px;font:inherit;font-size:13px;cursor:pointer;white-space:nowrap}
.theme:hover{border-color:var(--ink3)}
section{max-width:1040px;margin:0 auto;padding:22px 24px}
.lead{color:var(--ink2);margin:6px 0 16px;max-width:76ch}
.tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:12px;padding-top:14px}
.tile{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 16px}
.num{font-size:26px;font-weight:650;letter-spacing:-.01em}
.cap{color:var(--ink3);font-size:12.5px;margin-top:2px}
.split{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:12px;margin-top:8px}
.card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 16px;
  border-left:3px solid var(--line2)}
.card.place{border-left-color:var(--place)} .card.keep{border-left-color:var(--keep)}
.card.new{border-left-color:var(--new)} .card.transfer{border-left-color:var(--ink3)}
.hint{color:var(--ink3);font-size:12px;margin-top:6px}
.chart{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:16px 18px}
.row{display:grid;grid-template-columns:200px 1fr 170px;gap:12px;align-items:center;padding:5px 0}
.chart.wide .row{grid-template-columns:210px 1fr 190px}
.label{color:var(--ink2);font-size:13.5px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.bar{display:block;height:12px;background:var(--sunk);border-radius:4px;overflow:hidden}
.fill{display:block;height:100%;border-radius:4px}
.value{text-align:right;font-size:13.5px;font-variant-numeric:tabular-nums}
.value i{color:var(--ink3);font-style:normal;font-size:12.5px;margin-left:6px}
table{width:100%;border-collapse:collapse;background:var(--card);border:1px solid var(--line);
  border-radius:12px;overflow:hidden}
th{text-align:left;font-size:12px;text-transform:uppercase;letter-spacing:.04em;color:var(--ink3);
  font-weight:600;padding:10px 12px;border-bottom:1px solid var(--line);background:var(--sunk)}
td{padding:8px 12px;border-bottom:1px solid var(--line);font-size:13.5px;vertical-align:middle}
tr:last-child td{border-bottom:none}
tr:hover td{background:var(--sunk)}
td.n{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
td.n .bar{display:inline-block;width:70px;vertical-align:middle;margin-right:8px;height:8px}
.dim{color:var(--ink3)}
.name{font-weight:550}
.tag{display:inline-block;padding:2px 8px;border-radius:999px;font-size:12px;font-weight:600;
  white-space:nowrap}
.tag.place{background:var(--placebg);color:var(--place)}
.tag.keep{background:var(--keepbg);color:var(--keep)}
.tag.new{background:var(--newbg);color:var(--new)}
.tag.transfer,.tag.open{background:var(--sunk);color:var(--ink3)}
.props{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:12px}
.prop{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 16px;
  border-top:3px solid var(--new)}
.prop-h{display:flex;gap:8px;align-items:baseline;justify-content:space-between}
.prop-h b{font-size:15.5px}
.prop-h .dim{font-size:12.5px}
.vol{color:var(--new);font-size:12.5px;font-weight:600;margin:4px 0 6px}
.prop p{margin:0;color:var(--ink2);font-size:13.5px}
.plain,.rules{background:var(--card);border:1px solid var(--line);border-radius:12px;
  padding:14px 16px 14px 34px;margin:0;color:var(--ink2)}
.plain li,.rules li{margin:6px 0}
.plain b,.rules b{color:var(--ink)}
.note{background:var(--card);border:1px solid var(--line);border-left:3px solid var(--acc);
  border-radius:12px;padding:14px 16px;color:var(--ink2);margin:12px 0 0;max-width:none}
footer{max-width:1040px;margin:0 auto;padding:8px 24px 48px;color:var(--ink3);font-size:12.5px}
code{background:var(--sunk);border-radius:4px;padding:1px 5px;font-size:12px}
#tip{position:fixed;z-index:9;pointer-events:none;opacity:0;transition:opacity .12s;
  background:var(--ink);color:var(--paper);font-size:12.5px;padding:6px 9px;border-radius:7px;
  white-space:nowrap;transform:translate(-50%,-140%)}
</style>
</head>
<body>
<div id="tip"></div>
"""

TAIL = """<script>
function flip(){
  const root = document.documentElement;
  root.dataset.theme = root.dataset.theme === 'dark' ? 'light' : 'dark';
}
const tip = document.getElementById('tip');
document.addEventListener('mouseover', e => {
  const host = e.target.closest('[data-tip]');
  if (!host) { tip.style.opacity = 0; return; }
  const box = host.getBoundingClientRect();
  tip.textContent = host.dataset.tip;
  tip.style.left = (box.left + box.width / 2) + 'px';
  tip.style.top = box.top + 'px';
  tip.style.opacity = 1;
});
document.addEventListener('mouseout', e => {
  if (!e.relatedTarget || !e.relatedTarget.closest('[data-tip]')) tip.style.opacity = 0;
});
</script>
</body>
</html>
"""


if __name__ == "__main__":
    main()
