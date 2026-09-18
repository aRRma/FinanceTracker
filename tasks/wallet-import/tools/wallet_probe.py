"""Opisyvaet damp Wallet: skolko chego, kakie polya u operacii, obrazcy dokumentov.

Zapusk: python tasks/wallet-import/tools/wallet_probe.py
Otchet uhodit v artifacts/wallet-report.txt - v konsoli Windows kirillica
prihodit mozaikoy, a v fayle chitaetsya.
"""
import io
import json
import os
from collections import Counter

from wallet_dump import DATA, load


def main():
    wallet = load()
    lines = []
    say = lines.append

    say("== po tipam (zhivye) ==")
    census = Counter(d.get("reservedModelType", "?") for d in wallet.live)
    for name, count in census.most_common():
        say("%-26s %5d" % (name, count))

    say("")
    say("== polya Record ==")
    fields = Counter()
    for doc in wallet.records:
        fields.update(doc.keys())
    for name, count in fields.most_common():
        say("%-26s %5d" % (name, count))

    say("")
    say("== znacheniya sluzhebnyh poley ==")
    for name in ("type", "recordState", "paymentType", "transfer", "reservedSource"):
        say("%s: %s" % (name, dict(Counter(repr(d.get(name)) for d in wallet.records).most_common(12))))

    say("")
    say("== po godam (data v UTC) ==")
    years = Counter(d.get("recordDate", "")[:4] for d in wallet.records)
    say(json.dumps(dict(sorted(years.items())), ensure_ascii=False))

    say("")
    say("== scheta ==")
    for doc in wallet.accounts.values():
        say(json.dumps({k: v for k, v in doc.items() if not k.startswith("reserved")},
                       ensure_ascii=False, sort_keys=True))

    say("")
    say("== valyuty ==")
    for doc in wallet.currencies.values():
        say(json.dumps({k: v for k, v in doc.items() if not k.startswith("reserved")},
                       ensure_ascii=False, sort_keys=True))

    say("")
    say("== metki ==")
    for doc in wallet.hashtags.values():
        say(json.dumps({k: v for k, v in doc.items() if not k.startswith("reserved")},
                       ensure_ascii=False, sort_keys=True))

    for title, found in (
        ("obrazec rashoda", next((d for d in wallet.records
                                  if not d.get("transferId") and d.get("type") == 1), None)),
        ("obrazec dohoda", next((d for d in wallet.records if d.get("type") == 0), None)),
        ("obrazec perevoda", next((d for d in wallet.records if d.get("transferId")), None)),
        ("obrazec s zametkoy", next((d for d in wallet.records if d.get("note")), None)),
        ("obrazec dolga", next(iter(wallet.by_type("Debt")), None)),
    ):
        say("")
        say("== %s ==" % title)
        say(json.dumps(found, ensure_ascii=False, sort_keys=True, indent=1)
            if found else "net takih")

    path = os.path.join(DATA, "wallet-report.txt")
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("-> %s" % path)


if __name__ == "__main__":
    main()
