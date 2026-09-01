#!/usr/bin/env python3
"""Проверка документации: ссылки, числа, термины, цвета, покрытие.

Ловит классы ошибок, которые возвращаются после каждой правки руками.
Смысловые противоречия не ловит — их находит только человек.

Запуск:  python tools/lint_docs.py
Windows: PYTHONIOENCODING=utf-8 python tools/lint_docs.py
"""
import json, pathlib, re, sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
errors, warnings = [], []

MD = sorted(p for p in ROOT.rglob("*.md") if ".git" not in p.parts)
HTML = sorted(ROOT.glob("docs/ui/*.html"))


def rel(p):
    return p.relative_to(ROOT).as_posix()


def read(p):
    return p.read_text(encoding="utf-8")


def lines(p):
    return read(p).splitlines()


# идентификатор требования: INV-08, FR-PAY-02, SYN-14, NFR-25, SEED-03, SYS-01, TECH-02, UC-11
ID = re.compile(r"\b(INV|SYN|SEED|NFR|SYS|TECH|UC|FR-(?:ACC|CAT|TRX|PAY|BAL|LED|RPT|SYN|SET))-(\d{2})\b")
# определение: строка таблицы, начинающаяся идентификатором, возможно зачёркнутым
DEFN = re.compile(r"^\|\s*~?~?((?:INV|SYN|SEED|NFR|SYS|TECH|FR-(?:ACC|CAT|TRX|PAY|BAL|LED|RPT|SYN|SET))-\d{2})~?~?\s*\|")
UC_DEFN = re.compile(r"^## (UC-\d{2})\b")


# ---- L1. инвентарь: где определён каждый идентификатор -----------------------
# Определения живут только в docs/requirements/ и в use-cases.md. Всё прочее —
# ссылки: таблица в uncovered.md перечисляет требования, но не задаёт их.
DEFN_FILES = [p for p in MD if p.parent.name == "requirements" or p.name in ("use-cases.md", "architecture.md")]
defined, annulled = {}, set()
for p in DEFN_FILES:
    for i, line in enumerate(lines(p), 1):
        for m in (DEFN.match(line), UC_DEFN.match(line)):
            if not m:
                continue
            key = m.group(1)
            if line.lstrip().startswith("| ~~") or "~~" + key + "~~" in line:
                annulled.add(key)
            if key in defined:
                errors.append(f"L1 {rel(p)}:{i}: {key} определён повторно, первый раз — {defined[key]}")
            else:
                defined[key] = f"{rel(p)}:{i}"

# префикс должен жить ровно в одном файле
by_prefix = {}
for key, loc in defined.items():
    by_prefix.setdefault(key.rsplit("-", 1)[0], set()).add(loc.split(":")[0])
for prefix, files in sorted(by_prefix.items()):
    if len(files) > 1:
        errors.append(f"L1 префикс {prefix}-* размазан по файлам: {', '.join(sorted(files))}")

# пропуски в нумерации
groups = {}
for key in defined:
    prefix, num = key.rsplit("-", 1)
    groups.setdefault(prefix, []).append(int(num))
for prefix, nums in sorted(groups.items()):
    missing = [n for n in range(1, max(nums) + 1) if n not in nums]
    if missing:
        errors.append(f"L1 {prefix}-*: пропущены номера {missing} (номера не освобождаются)")


# ---- L2. каждое упоминание идентификатора разыменовывается --------------------
for p in MD + HTML:
    for i, line in enumerate(lines(p), 1):
        for m in ID.finditer(line):
            key = m.group(0)
            if key not in defined:
                errors.append(f"L2 {rel(p)}:{i}: ссылка на несуществующий {key}")


# ---- L3. диапазоны: «INV-01 … INV-26», «FR-PAY-01…06» ------------------------
RANGE = re.compile(r"\b((?:INV|SYN|SEED|NFR|SYS|TECH|UC|FR-(?:ACC|CAT|TRX|PAY|BAL|LED|RPT|SYN|SET))-)(\d{2})\s*(?:—|–|-|…|\.\.\.)\s*(?:\1)?(\d{2})\b")
for p in MD:
    for i, line in enumerate(lines(p), 1):
        for m in RANGE.finditer(line):
            prefix, lo, hi = m.group(1).rstrip("-"), int(m.group(2)), int(m.group(3))
            top = max(groups.get(prefix, [0]))
            if lo == 1 and hi < top and ("все" in line.lower() or "кажд" in line.lower()):
                errors.append(
                    f"L3 {rel(p)}:{i}: диапазон {prefix}-{lo:02d}…{prefix}-{hi:02d} заявлен как полный, а существует до {prefix}-{top:02d}")


# ---- L4. ссылки на файлы существуют ------------------------------------------
LINK = re.compile(r"\[[^\]]*\]\(([^)#]+?)(?:#[^)]*)?\)")
for p in MD:
    for i, line in enumerate(lines(p), 1):
        for m in LINK.finditer(line):
            target = m.group(1)
            if target.startswith(("http://", "https://", "mailto:")):
                continue
            if not (p.parent / target).exists():
                errors.append(f"L4 {rel(p)}:{i}: ссылка на несуществующий путь {target}")
        # упоминание файла прозой, без ссылки
        for m in re.finditer(r"`([\w./-]+\.(?:md|html|json|py))`", line):
            name = m.group(1)
            if not any(q.name == pathlib.Path(name).name for q in ROOT.rglob(pathlib.Path(name).name)):
                errors.append(f"L4 {rel(p)}:{i}: упомянут несуществующий файл {name}")


# ---- L5. экраны ---------------------------------------------------------------
SCREEN = re.compile(r"\b([ABCDE]-\d{2})\b")
mock = ROOT / "docs/ui/mockups.html"
proto = ROOT / "docs/ui/prototype.html"
screens_mock = set(SCREEN.findall(read(mock))) if mock.exists() else set()

for p in MD:
    for i, line in enumerate(lines(p), 1):
        for key in SCREEN.findall(line):
            if key not in screens_mock:
                errors.append(f"L5 {rel(p)}:{i}: ссылка на несуществующий экран {key}")

uc_text = read(ROOT / "docs/use-cases.md") if (ROOT / "docs/use-cases.md").exists() else ""
for key in sorted(screens_mock - set(SCREEN.findall(uc_text))):
    warnings.append(f"L5 экран {key} не упомянут ни в одном сценарии")

# прототип собирается из макетов скриптом; проверяем, что сборка не устарела
sys.path.insert(0, str(ROOT / "tools"))
import build_prototype
fresh, _ = build_prototype.build()
if not proto.exists() or read(proto) != fresh:
    errors.append("L5 docs/ui/prototype.html устарел относительно макетов: запустите python tools/build_prototype.py")


# ---- L6. запрещённые термины ---------------------------------------------------
# Автовывод основ из CONTEXT.md даёт слишком много ложных срабатываний: «обмен»,
# «сумма», «разница», «журнал» законны в своих смыслах. Поэтому список ручной и
# содержит только то, что неверно при любом употреблении.
BANNED = [
    (r"копилк\w*", "«Накопления»"),
    (r"контрагент\w*", "«Место»"),
    (r"родительск\w+\s+категори\w+", "«группа»"),
    (r"категори\w+\s+первого\s+уровня", "«группа»"),
    (r"категори\w+\s+второго\s+уровня", "«подкатегория»"),
    ("payee", "Place"),
    (r"доступн\w+\s+остат\w+", "«доступно к тратам»"),
    (r"истори\w+\s+по\s+счёту", "«лента счёта»"),
    ("exclude_from_available", "excluded_from_totals"),
]
for p_ in MD:
    if p_.name in {"CONTEXT.md", "MEMORY.md"} or "adr" in p_.parts:
        continue
    for i, line in enumerate(lines(p_), 1):
        for rx, better in BANNED:
            m = re.search(rx, line, re.I)
            if m:
                warnings.append(f"L6 {rel(p_)}:{i}: «{m.group(0)}» — по словарю {better}")


# ---- L7. числа из данных ------------------------------------------------------
preset = json.loads(read(ROOT / "data/preset.json"))
icons = json.loads(read(ROOT / "data/icons.json"))["icons"]
n_groups = len(preset["groups"])
n_subs = sum(len(g["subcategories"]) for g in preset["groups"])
n_icons = len(icons)
n_screens = len(screens_mock)

if len(icons) != len(set(icons)):
    dupes = {i for i in icons if icons.count(i) > 1}
    errors.append(f"L10 data/icons.json: дубли ключей {sorted(dupes)}")

CHECKS = [
    (re.compile(r"(\d+)\s+групп"), n_groups, "групп"),
    (re.compile(r"(\d+)\s+подкатегори"), n_subs, "подкатегорий"),
    (re.compile(r"(\d+)\s+значк"), n_icons, "значков"),
    (re.compile(r"(\d+)\s+ключ"), n_icons, "ключей значков"),
    (re.compile(r"(\d+)\s+экран"), n_screens, "экранов"),
]
WORDS = {"двадцать": 20, "двадцать два": 22, "двадцать две": 22, "тринадцать": 13}

for p in MD + HTML:
    for i, line in enumerate(lines(p), 1):
        if "lint:ignore" in line:
            continue
        text = re.sub(r"<[^>]+>", " ", line)
        for rx, expected, what in CHECKS:
            for m in rx.finditer(text):
                if int(m.group(1)) != expected:
                    errors.append(f"L7 {rel(p)}:{i}: «{m.group(0).strip()}» — по данным {expected} {what}")
        for word, value in WORDS.items():
            if re.search(rf"\b{word}\s+экран", text.lower()) and value != n_screens:
                errors.append(f"L7 {rel(p)}:{i}: «{word} экранов» — фактически {n_screens}")


# ---- L8. цвета только внутри блоков палитры -----------------------------------
HEX = re.compile(r"#[0-9a-fA-F]{3,8}\b|\brgba?\(")
for p in HTML:
    inside, depth = False, 0
    for i, line in enumerate(lines(p), 1):
        if re.search(r"(:root|\[data-theme)[^{]*\{", line):
            inside, depth = True, line.count("{") - line.count("}")
            continue
        if inside:
            depth += line.count("{") - line.count("}")
            if depth <= 0:
                inside = False
            continue
        for m in HEX.finditer(line):
            errors.append(f"L8 {rel(p)}:{i}: прямой цвет {m.group(0)} вне блока определения палитры (NFR-24)")


# ---- L9. покрытие требований сценариями ---------------------------------------
covered = set(ID.findall(uc_text))
covered = {f"{a}-{b}" for a, b in covered}
uncovered_file = ROOT / "docs/uncovered.md"
explained = set(re.findall(r"\b(FR-[A-Z]{3}-\d{2})\b", read(uncovered_file))) if uncovered_file.exists() else set()
for key in sorted(k for k in defined if k.startswith("FR-")):
    if key.startswith("FR-SYN") or key in annulled:
        continue
    if key not in covered and key not in explained:
        warnings.append(f"L9 {key} не покрыт ни одним сценарием и не объяснён в docs/uncovered.md")


# ---- вывод --------------------------------------------------------------------
print(f"файлов: {len(MD)} md, {len(HTML)} html · идентификаторов: {len(defined)} · "
      f"экранов: {n_screens} · пресет: {n_groups}/{n_subs}, значков {n_icons}")

for w in warnings:
    print("  ПРЕДУПРЕЖДЕНИЕ: " + w)
for e in errors:
    print("  ОШИБКА: " + e)

if errors:
    print(f"\nошибок: {len(errors)}, предупреждений: {len(warnings)}")
    sys.exit(1)
print(f"\nдокументация согласована. предупреждений: {len(warnings)}")
