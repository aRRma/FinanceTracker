"""Sverjaet vygruzku iz dampa so spiskom, snyatym s ekrana telefona.

Zapusk: python tasks/wallet-import/tools/wallet_verify.py
Ekrannyy spisok - artifacts/wallet-screen-sept.csv (kolonki date;title;account;amount,
summa so znakom, data mestnaya). Sverka otvechaet na tri voprosa srazu: verno li
ponyat znak operacii, v kakih edinicah summa i v kakoy zone zapisana data.
"""
import csv
import io
import os
from collections import Counter, defaultdict
from datetime import datetime, timedelta
from decimal import Decimal

from wallet_dump import DATA

SCREEN = os.path.join(DATA, "wallet-screen-sept.csv")
EXPORT = os.path.join(DATA, "wallet-records.csv")
ACCOUNT = "Карта"  # schet "Karta": ego i snimali s ekrana
OFFSETS = (0, 3, 5)


def read(path):
    if not os.path.exists(path):
        raise SystemExit("net fayla: %s" % path)
    with io.open(path, encoding="utf-8-sig") as f:
        return list(csv.DictReader(f, delimiter=";"))


def fold(rows, offset, first, last):
    """Summy i chislo strok po dnyam pri zadannom sdvige ot UTC."""
    days, count = defaultdict(Decimal), Counter()
    for row in rows:
        if row["account"] != ACCOUNT or not row["occurred_utc"]:
            continue
        when = datetime.strptime(row["occurred_utc"][:19], "%Y-%m-%dT%H:%M:%S") + timedelta(hours=offset)
        day = when.date().isoformat()
        if not (first <= day <= last):
            continue
        value = Decimal(row["amount"])
        if row["kind"] in ("expense", "transfer_out"):
            value = -value
        days[day] += value
        count[day] += 1
    return days, count


def main():
    screen_rows = read(SCREEN)
    export_rows = read(EXPORT)

    screen_days, screen_count = defaultdict(Decimal), Counter()
    for row in screen_rows:
        if row["date"]:
            screen_days[row["date"]] += Decimal(row["amount"])
            screen_count[row["date"]] += 1

    first, last = min(screen_days), max(screen_days)
    print("s ekrana: %d strok, %s .. %s" % (len(screen_rows), first, last))

    best = None
    for offset in OFFSETS:
        days, count = fold(export_rows, offset, first, last)
        hits = sum(1 for d in screen_days if days.get(d) == screen_days[d])
        print("sdvig +%d: sovpalo dney %d iz %d, strok v dampe %d"
              % (offset, hits, len(screen_days), sum(count.values())))
        if best is None or hits > best[0]:
            best = (hits, offset, days, count)

    hits, offset, days, count = best
    print("\nluchshiy sdvig +%d; rashozhdeniya po dnyam:" % offset)
    gaps = 0
    for day in sorted(set(days) | set(screen_days)):
        if days.get(day) != screen_days.get(day):
            gaps += 1
            print("  %s: damp %s (%d strok) / ekran %s (%d strok)"
                  % (day, days.get(day, 0), count.get(day, 0),
                     screen_days.get(day, 0), screen_count.get(day, 0)))
    if gaps == 0:
        print("  rashozhdeniy net")
    print("\nden s rashozhdeniem v konce perioda znachit, chto veb-versiya otstala"
          " ot telefona: povtori snyatie dampa posle sinhronizacii")


if __name__ == "__main__":
    main()
