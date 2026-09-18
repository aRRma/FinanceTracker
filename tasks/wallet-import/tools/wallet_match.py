"""Sopostavlyaet kategorii Wallet s nashim startovym naborom i schitaet ih ves.

Zapusk: python tasks/wallet-import/tools/wallet_match.py
Otchet - artifacts/wallet-match.txt (kirillica v konsoli Windows nechitaema).

Sovpadenie ishetsya po imeni: tochnoe posle privedeniya k nizhnemu registru i
zameny "e" s tochkami. Eto tolko podskazka - reshenie ostaetsya za chelovekom.
"""
import io
import json
import os
from collections import Counter, defaultdict
from decimal import Decimal

from wallet_dump import DATA, GROUPS, ROOT, kind_of, load, money

PRESET = os.path.join(ROOT, "data", "preset.json")
OUT = os.path.join(DATA, "wallet-match.txt")

# Kategoriya perevodov Wallet: u nas perevod kategorii ne imeet vovse.
TRANSFER_ENVELOPES = (20, )


def normal(name):
    return (name or "").strip().lower().replace("ё", "е")


def preset():
    with io.open(PRESET, encoding="utf-8") as f:
        data = json.load(f)
    ours = {}
    for group in data["groups"]:
        for sub in group["subcategories"]:
            ours[normal(sub["name"])] = (group["name"], sub["name"], group["kind"])
    return data, ours


def main():
    wallet = load()
    data, ours = preset()

    # Ves kategorii: skolko operaciy i na kakuyu summu; perevody schitaem otdelno.
    count = Counter()
    total = defaultdict(Decimal)
    kinds = defaultdict(Counter)
    notes = defaultdict(Counter)
    places = defaultdict(Counter)
    for rec in wallet.records:
        cid = rec.get("categoryId")
        kind = kind_of(rec)
        count[cid] += 1
        total[cid] += money(rec.get("amount", 0))
        kinds[cid][kind] += 1
        if rec.get("note"):
            notes[cid][rec["note"].strip()[:40]] += 1
        if rec.get("payee"):
            places[cid][rec["payee"].strip()[:30]] += 1

    lines = []
    say = lines.append

    say("KATEGORII WALLET PROTIV NASHEGO STARTOVOGO NABORA")
    say("operaciy vsego: %d" % len(wallet.records))
    say("")

    rows = []
    for cid, used in count.most_common():
        cat = wallet.categories.get(cid, {})
        group, envelope = wallet.group(cid)
        name = cat.get("name", "")
        hit = ours.get(normal(name)) if name else None
        rows.append({
            "id": cid, "name": name, "group": group, "envelope": envelope,
            "used": used, "sum": total[cid], "kinds": kinds[cid],
            "ours": hit, "transfer": envelope // 1000 in TRANSFER_ENVELOPES,
            "income": cat.get("defaultType") == 0,
        })

    # Skolko kategoriy nabiraet 90 procentov operaciy: stolko ih i nuzhno na dele.
    running = 0
    enough = 0
    for row in rows:
        running += row["used"]
        enough += 1
        if running >= len(wallet.records) * 0.9:
            break
    say("90%% operaciy pokryvayut %d kategoriy iz %d" % (enough, len(rows)))
    say("")

    say("== TOP-40 PO CHISLU OPERACIY ==")
    say("%-34s %-22s %6s %14s  %s" % ("kategoriya Wallet", "gruppa Wallet", "oper", "summa", "u nas"))
    for row in rows[:40]:
        found = "-"
        if row["transfer"]:
            found = "perevod, kategoriya ne nuzhna"
        elif row["ours"]:
            found = "%s / %s" % row["ours"][:2]
        say("%-34s %-22s %6d %14s  %s"
            % ((row["name"] or "(bez imeni, envelope %d)" % row["envelope"])[:34],
               (row["group"] or "?")[:22], row["used"], "%.2f" % row["sum"], found))

    say("")
    say("== CHEGO U NAS NET: kategorii bez sovpadeniya, po chislu operaciy ==")
    say("%-34s %-22s %6s %14s  %s" % ("kategoriya Wallet", "gruppa Wallet", "oper", "summa", "vid"))
    missing = [r for r in rows if not r["ours"] and not r["transfer"] and r["used"] > 0]
    for row in missing[:60]:
        vid = "dohod" if row["income"] else "rashod"
        say("%-34s %-22s %6d %14s  %s"
            % ((row["name"] or "(bez imeni, envelope %d)" % row["envelope"])[:34],
               (row["group"] or "?")[:22], row["used"], "%.2f" % row["sum"], vid))
    say("")
    say("vsego bez sovpadeniya: %d kategoriy, %d operaciy"
        % (len(missing), sum(r["used"] for r in missing)))

    say("")
    say("== SOVPALO PO IMENI ==")
    hits = [r for r in rows if r["ours"]]
    for row in hits:
        say("%-34s -> %s / %s  (%d oper)"
            % (row["name"][:34], row["ours"][0], row["ours"][1], row["used"]))
    say("vsego sovpalo: %d kategoriy, %d operaciy"
        % (len(hits), sum(r["used"] for r in hits)))

    say("")
    say("== BEZYMYANNYE KATEGORII: chto v nih lezhit ==")
    for row in rows:
        if row["name"] or not row["used"]:
            continue
        say("")
        say("envelope %d (%s): %d operaciy, %s"
            % (row["envelope"], row["group"] or "?", row["used"], "%.2f" % row["sum"]))
        say("  vidy: %s" % dict(row["kinds"]))
        top_notes = notes[row["id"]].most_common(6)
        top_places = places[row["id"]].most_common(6)
        if top_notes:
            say("  chastye zametki: %s" % "; ".join("%s x%d" % (n, c) for n, c in top_notes))
        if top_places:
            say("  chastye mesta: %s" % "; ".join("%s x%d" % (n, c) for n, c in top_places))

    say("")
    say("== NASH STARTOVYY NABOR ==")
    for group in data["groups"]:
        used_here = sum(r["used"] for r in rows
                        if r["ours"] and r["ours"][0] == group["name"])
        say("%s (%s): %s" % (group["name"], group["kind"],
                             ", ".join(s["name"] for s in group["subcategories"])))
        say("   operaciy Wallet, sevshih syuda po imeni: %d" % used_here)

    say("")
    say("== GRUPPY WALLET PO VESU ==")
    weight = Counter()
    amounts = defaultdict(Decimal)
    for row in rows:
        key = GROUPS.get(row["envelope"] // 1000, "?")
        weight[key] += row["used"]
        amounts[key] += row["sum"]
    for name, used in weight.most_common():
        say("%-24s %6d oper %16s" % (name, used, "%.2f" % amounts[name]))

    with io.open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("-> %s" % OUT)


if __name__ == "__main__":
    main()
