// Snimaet lokalnuyu bazu veb-versii Wallet (PouchDB v IndexedDB) v odin fayl.
//
// Kuda vstavlyat: web.budgetbakers.com, stranica /records, DevTools -> Console.
// Imya bazy u kazhdogo svoe: _pouch_user_bb-<id polzovatelya>. Skript nahodit ego sam.
// Nichego ne menyaet: tolko chitaet i skachivaet wallet-pouch.json v "Zagruzki".
(async () => {
  const known = await indexedDB.databases();
  const found = known.find(d => (d.name || '').startsWith('_pouch_user_bb-'));
  if (!found) {
    console.error('Baza Wallet ne naydena. Otkroy /records i dozhdis zagruzki spiska.');
    return;
  }

  const db = await new Promise((ok, no) => {
    const r = indexedDB.open(found.name);
    r.onsuccess = () => ok(r.result);
    r.onerror = () => no(r.error);
  });

  const ask = (store, fn) => new Promise((ok, no) => {
    const q = fn(db.transaction(store, 'readonly').objectStore(store));
    q.onsuccess = () => ok(q.result);
    q.onerror = () => no(q.error);
  });

  // by-sequence hranit tela vseh reviziy, document-store - kakaya reviziya zhivaya.
  const docs = await ask('by-sequence', o => o.getAll());
  const meta = await ask('document-store', o => o.getAll());

  const census = {};
  for (const d of docs) {
    const t = d.reservedModelType || '(net tipa)';
    census[t] = (census[t] || 0) + 1;
  }
  console.log('PO TIPAM (s uchetom staryh reviziy):');
  console.table(census);

  const dates = docs.filter(d => d.recordDate).map(d => d.recordDate).sort();
  console.log('operacii s ' + (dates[0] || '?').slice(0, 10) +
              ' po ' + (dates[dates.length - 1] || '?').slice(0, 10));

  const dump = JSON.stringify({
    exportedAt: new Date().toISOString(),
    database: found.name,
    docs,
    heads: meta.map(m => ({ id: m.id, winningRev: m.winningRev, deleted: m.deletedOrLocal }))
  });

  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([dump], { type: 'application/json' }));
  a.download = 'wallet-pouch.json';
  a.click();
  console.log('fayl ~' + (dump.length / 1048576).toFixed(1) + ' MB uhodit v Zagruzki');
})();
