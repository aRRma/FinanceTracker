"""Sobiraet iz dampa Wallet ploskie CSV: operacii, kategorii, scheta.

Zapusk: python tasks/wallet-import/tools/wallet_export.py
Chitaet artifacts/wallet-pouch.json ryadom s zadachey, kladet CSV ryadom s nim.
"""
import csv
import io
import os
from collections import Counter, defaultdict

from wallet_dump import DATA, kind_of, load, money


def write(name, header, rows):
    path = os.path.join(DATA, name)
    with io.open(path, "w", encoding="utf-8-sig", newline="") as f:
        out = csv.writer(f, delimiter=";", lineterminator="\n")
        out.writerow(header)
        out.writerows(rows)
    return path


def records_csv(wallet):
    # Perevod lezhit dvumya zapisyami s odnim transferId: spisanie i zachislenie.
    sides = defaultdict(list)
    for rec in wallet.records:
        if rec.get("transferId"):
            sides[rec["transferId"]].append(rec)

    kinds = Counter()
    rows = []
    for rec in wallet.records:
        kind = kind_of(rec)
        kinds[kind] += 1
        group, envelope = wallet.group(rec.get("categoryId"))

        target = ""
        if rec.get("transferId"):
            mate = next((r for r in sides[rec["transferId"]] if r is not rec), None)
            target_id = rec.get("transferAccountId") or (mate or {}).get("accountId", "")
            target = wallet.name(target_id, target_id)

        rows.append([
            rec["_id"].replace("Record_", ""),
            rec.get("recordDate", ""),
            kind,
            "%.2f" % money(rec.get("amount", 0)),
            wallet.currencies.get(rec.get("currencyId"), {}).get("code", "?"),
            wallet.name(rec.get("accountId"), rec.get("accountId", "")),
            target,
            wallet.name(rec.get("categoryId")),
            group,
            envelope,
            (rec.get("note") or "").replace("\n", " ").strip(),
            rec.get("payee", "") or "",
            ",".join(wallet.name(x, x) for x in (rec.get("labels") or [])),
            rec.get("transferId", "") or "",
            rec.get("recordState", ""),
            rec.get("reservedSource", ""),
            rec.get("reservedCreatedAt", ""),
            rec.get("reservedUpdatedAt", ""),
        ])

    path = write("wallet-records.csv", [
        "id", "occurred_utc", "kind", "amount", "currency", "account", "target_account",
        "category", "category_group", "envelope_id", "note", "place", "labels",
        "transfer_id", "state", "source", "created_utc", "updated_utc"], rows)

    print("operaciy: %d -> %s" % (len(rows), os.path.basename(path)))
    print("  po vidam: %s" % dict(kinds))
    print("  storon u perevoda: %s" % dict(Counter(len(v) for v in sides.values())))
    print("  valyuty: %s" % dict(Counter(
        wallet.currencies.get(r.get("currencyId"), {}).get("code", "?") for r in wallet.records)))
    print("  bez imeni kategorii: %d" % sum(1 for r in rows if not r[7]))
    print("  s zametkoy: %d, s mestom: %d, s metkami: %d"
          % (sum(1 for r in rows if r[10]), sum(1 for r in rows if r[11]),
             sum(1 for r in rows if r[12])))
    dates = [r[1][:10] for r in rows if r[1]]
    print("  daty (UTC): %s .. %s" % (min(dates), max(dates)))


def categories_csv(wallet):
    used = Counter(r.get("categoryId") for r in wallet.records)
    rows = []
    for cid, cat in sorted(wallet.categories.items(), key=lambda kv: -used[kv[0]]):
        group, envelope = wallet.group(cid)
        rows.append([
            cid.replace("-Category_", ""), cat.get("name", ""), group, envelope,
            "income" if cat.get("defaultType") == 0 else "expense",
            cat.get("customCategory", False), cat.get("iconName", ""),
            cat.get("color", ""), used[cid], cid in wallet.alive,
        ])
    path = write("wallet-categories.csv", [
        "id", "name", "group", "envelope_id", "default_type", "custom",
        "icon", "color", "used", "alive"], rows)
    print("kategoriy: %d -> %s (bezymyannyh %d)"
          % (len(rows), os.path.basename(path), sum(1 for r in rows if not r[1])))


def accounts_csv(wallet):
    used = Counter(r.get("accountId") for r in wallet.records)
    rows = []
    for aid, acc in wallet.accounts.items():
        rows.append([
            aid.replace("-Account_", ""), acc.get("name", ""), acc.get("accountType", ""),
            wallet.currencies.get(acc.get("currencyId"), {}).get("code", "?"),
            "%.2f" % money(acc.get("initAmount", 0)),
            acc.get("excludeFromStats", False), acc.get("archived", False),
            aid in wallet.alive, used[aid],
        ])
    path = write("wallet-accounts.csv", [
        "id", "name", "type", "currency", "init_amount", "exclude_from_stats",
        "archived", "alive", "records"], rows)
    print("schetov: %d -> %s" % (len(rows), os.path.basename(path)))


def main():
    wallet = load()
    records_csv(wallet)
    categories_csv(wallet)
    accounts_csv(wallet)


if __name__ == "__main__":
    main()
