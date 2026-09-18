"""Obshaya chast tuliz Wallet: gde lezhat dannye i kak razobrat damp PouchDB.

Damp - eto vygruzka lokalnoy bazy veb-versii Wallet, snyataya dump_pouch.js.
Vnutri lezhat VSE revizii dokumentov, vklyuchaya starye i udalennye; zhivym
schitaetsya dokument, ch'ya reviziya sovpala s winningRev i ne pomechena udalennoy.
"""
import io
import json
import os
from decimal import Decimal

HERE = os.path.dirname(os.path.abspath(__file__))
TASK = os.path.dirname(HERE)
ROOT = os.path.dirname(os.path.dirname(TASK))

# Lichnye dannye v repozitoriy ne popadayut: lyubaya papka artifacts v .gitignore.
DATA = os.path.join(TASK, "artifacts")
DUMP = os.path.join(DATA, "wallet-pouch.json")

# Gruppy kategoriy Wallet vyvodyatsya iz envelopeId: pervyy razryad - gruppa.
GROUPS = {
    1: "Food & Drinks", 2: "Shopping", 3: "Housing", 4: "Transportation",
    5: "Vehicle", 6: "Life & Entertainment", 7: "Communication, PC",
    8: "Financial expenses", 9: "Investments", 10: "Income", 11: "Others",
    20: "Transfers",
}


class Wallet:
    """Razobrannyy damp: zhivye dokumenty i spravochniki po vsem, vklyuchaya udalennye."""

    def __init__(self, live, every):
        self.live = live
        self.every = every
        self.records = sorted((d for d in live if d.get("reservedModelType") == "Record"),
                              key=lambda d: d.get("recordDate", ""))
        self.categories = self._all("Category")
        self.accounts = self._all("Account")
        self.currencies = self._all("Currency")
        self.hashtags = self._all("HashTag")
        self.alive = {d["_id"] for d in live}
        self.names = {doc_id: doc["name"] for doc_id, doc in every.items() if "name" in doc}

    def _all(self, model):
        return {doc_id: doc for doc_id, doc in self.every.items()
                if doc.get("reservedModelType") == model}

    def by_type(self, model):
        """Zhivye dokumenty odnogo tipa."""
        return [d for d in self.live if d.get("reservedModelType") == model]

    def name(self, doc_id, default=""):
        """Imya scheta, kategorii ili metki; u standartnyh kategoriy imeni net."""
        return self.names.get(doc_id, default)

    def group(self, category_id):
        """Gruppa kategorii po envelopeId."""
        envelope = self.categories.get(category_id, {}).get("envelopeId", 0)
        return GROUPS.get(envelope // 1000, ""), envelope


def money(minor):
    """Summy Wallet lezhat v kopeykah celym chislom."""
    return Decimal(int(minor)) / 100


def load(path=DUMP):
    """Chitaet damp i ostavlyaet tolko zhivye revizii."""
    if not os.path.exists(path):
        raise SystemExit("net dampa: %s\nsnimi ego skriptom dump_pouch.js" % path)

    with io.open(path, encoding="utf-8") as f:
        dump = json.load(f)

    heads = {h["id"]: (h["winningRev"], str(h["deleted"])) for h in dump["heads"]}
    live, every, stale, dead = [], {}, 0, 0
    for doc in dump["docs"]:
        doc_id, _, rev = doc["_doc_id_rev"].partition("::")
        doc["_id"] = doc_id
        every.setdefault(doc_id, doc)
        head = heads.get(doc_id)
        if head is None or rev != head[0]:
            stale += 1
            continue
        if head[1] not in ("0", "False", "false"):
            dead += 1
            continue
        live.append(doc)

    print("damp ot %s: zhivyh %d, staryh reviziy %d, udalennyh %d"
          % (dump.get("exportedAt", "?")[:19], len(live), stale, dead))
    return Wallet(live, every)


def kind_of(record):
    """Vid operacii: type 1 - dengi ushli so scheta, 0 - prishli; svereno s ekranom."""
    if record.get("transferId"):
        return "transfer_out" if record.get("type") == 1 else "transfer_in"
    return "expense" if record.get("type") == 1 else "income"
