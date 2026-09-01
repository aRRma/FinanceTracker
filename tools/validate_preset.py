#!/usr/bin/env python3
"""Сверка data/preset.json с правилами из docs/requirements.md."""
import json, sys, uuid, pathlib

root = pathlib.Path(__file__).resolve().parent.parent
p = json.loads((root / "data/preset.json").read_text(encoding="utf-8"))
icons = set(json.loads((root / "data/icons.json").read_text(encoding="utf-8"))["icons"])
err = []

ns = uuid.UUID(p["namespace"])
keys = set()
for g in p["groups"]:
    if g["id"] != str(uuid.uuid5(ns, g["key"])):
        err.append(f"SEED-02: id группы {g['key']} не выводится из ключа")
    if g["icon"] not in icons:
        err.append(f"SEED-07: значок {g['icon']} группы {g['key']} вне набора")
    subs = g["subcategories"]
    if not subs:
        err.append(f"группа {g['key']} без подкатегорий")
    has_other = any(s["key"].endswith(".other") for s in subs)
    if not has_other and not g["key"].startswith("service"):
        err.append(f"INV-18: группа {g['key']} без «Прочее»")
    for s in subs:
        if s["key"] in keys:
            err.append(f"дубль ключа {s['key']}")
        keys.add(s["key"])
        if not s["key"].startswith(g["key"] + "."):
            err.append(f"ключ {s['key']} не соответствует группе {g['key']}")
        if s["id"] != str(uuid.uuid5(ns, s["key"])):
            err.append(f"SEED-02: id {s['key']} не выводится из ключа")
        if s["icon"] not in icons:
            err.append(f"SEED-07: значок {s['icon']} у {s['key']} вне набора")

if p["seededAtUtc"] > "2020":
    err.append("SEED-03: метка засева должна быть заведомо давней")

print(f"групп {len(p['groups'])}, подкатегорий {len(keys)}, значков {len(icons)}")
if err:
    print("\n".join("  ОШИБКА: " + e for e in err)); sys.exit(1)
print("пресет корректен")
