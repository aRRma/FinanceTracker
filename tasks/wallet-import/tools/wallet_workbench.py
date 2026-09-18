"""Sobiraet stranicu-verstak: sleva nash nabor kategoriy, sprava derevo Wallet.

Zapusk: python tasks/wallet-import/tools/wallet_workbench.py
Rezultat: artifacts/workbench.html ryadom s zadachey - vne repozitoriya, tam chisla po lichnym dannym.

Stranica pravitsya pryamo v brauzere: dobavit, pereimenovat, udalit, perenesti
podkategoriyu v druguyu gruppu myshkoy. Itog vygruzhaetsya v JSON toy zhe formy, chto
data/preset.json, i im zhe zagruzhaetsya obratno.
"""
import io
import json
import os
from datetime import datetime, timezone

from wallet_dump import DATA, ROOT, load
from wallet_page import collect, group_ru

OUT = os.path.join(DATA, "workbench.html")

# Znachki gruppam Wallet podobrany iz nashego nabora: derevo sprava chitaetsya
# tak zhe, kak nashe sleva, a ne spiskom zagolovkov.
GROUP_ICONS = {
    "\u0415\u0434\u0430 \u0438 \u043d\u0430\u043f\u0438\u0442\u043a\u0438": "tools-kitchen-2",
    "\u0422\u0440\u0430\u043d\u0441\u043f\u043e\u0440\u0442": "train",
    "\u041f\u043e\u043a\u0443\u043f\u043a\u0438": "shopping-bag",
    "\u041f\u0435\u0440\u0435\u0432\u043e\u0434\u044b": "wallet",
    "\u0424\u0438\u043d\u0430\u043d\u0441\u043e\u0432\u044b\u0435 \u0440\u0430\u0441\u0445\u043e\u0434\u044b": "percentage",
    "\u0416\u0438\u0437\u043d\u044c \u0438 \u0440\u0430\u0437\u0432\u043b\u0435\u0447\u0435\u043d\u0438\u044f": "mood-happy",
    "\u0414\u043e\u0445\u043e\u0434": "cash",
    "\u0421\u0432\u044f\u0437\u044c \u0438 \u0442\u0435\u0445\u043d\u0438\u043a\u0430": "wifi",
    "\u0422\u0440\u0430\u043d\u0441\u043f\u043e\u0440\u0442\u043d\u043e\u0435 \u0441\u0440\u0435\u0434\u0441\u0442\u0432\u043e": "steering-wheel",
    "\u0416\u0438\u043b\u044c\u0451": "home",
    "\u0412\u043b\u043e\u0436\u0435\u043d\u0438\u044f": "trending-up",
    "\u041f\u0440\u043e\u0447\u0435\u0435": "dots",
}


def read(*parts):
    with io.open(os.path.join(ROOT, *parts), encoding="utf-8") as f:
        return json.load(f)


def main():
    preset = read("data", "preset.json")
    icons = read("data", "icons.json")
    paths = read("data", "icon-paths.json")

    wallet = load()
    rows = collect(wallet)

    payload = {
        "generatedAt": datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M"),
        "preset": preset,
        "icons": icons["icons"],
        "fallbackIcon": icons["fallback"],
        "iconPaths": paths["icons"],
        "groupIcons": GROUP_ICONS,
        "wallet": [
            {
                "name": row["title"],
                "group": group_ru(row["group"]),
                "count": row["count"],
                "sum": float(row["sum"]),
                "decision": row["decision"] or "open",
                "target": row["target"],
                "income": bool(row["kinds"].get("income")) and not row["kinds"].get("expense"),
            }
            for row in rows if row["count"]
        ],
    }

    html = TEMPLATE.replace("__PAYLOAD__", json.dumps(payload, ensure_ascii=False))
    with io.open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(html)

    print("-> %s" % OUT)
    print("kategoriy Wallet: %d, grupp u nas: %d" % (len(payload["wallet"]), len(preset["groups"])))


TEMPLATE = r"""<!doctype html>
<html lang="ru" data-theme="light">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Набор категорий: верстак</title>
<style>
:root{
  --paper:#EDEFF1; --card:#FFFFFF; --sunk:#F4F6F7;
  --ink:#16191C; --ink2:#4C535A; --ink3:#676D74;
  --line:#E3E6E9; --line2:#CBD1D6;
  --acc:#0F5C4C; --accbg:#E2EFEB;
  --keep:#00806A; --place:#2563C9; --new:#B45309; --open:#676D74;
  --keepbg:#E2EFEB; --placebg:#E4ECF9; --newbg:#F8EDE2;
  --neg:#963232; --negbg:#F7EAEA;
  --sans:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif;
}
[data-theme="dark"]{
  --paper:#101315; --card:#1C2124; --sunk:#252B2F;
  --ink:#E7EAEC; --ink2:#A8AFB5; --ink3:#8A9299;
  --line:#2F373C; --line2:#3A4147;
  --acc:#48A78E; --accbg:#1B342D;
  --keep:#2A9C83; --place:#5590E0; --new:#B9862F; --open:#8A9299;
  --keepbg:#1B342D; --placebg:#1B2739; --newbg:#332818;
  --neg:#D48A8A; --negbg:#372525;
}
*{box-sizing:border-box}
html,body{height:100%}
body{margin:0;background:var(--paper);color:var(--ink);font:14.5px/1.5 var(--sans);
  -webkit-font-smoothing:antialiased;display:flex;flex-direction:column}
button{font:inherit;cursor:pointer}
.top{padding:14px 20px 0;display:flex;gap:16px;align-items:flex-start;justify-content:space-between;flex-wrap:wrap}
h1{font-size:20px;margin:0}
.sub{color:var(--ink3);font-size:12.5px;margin:2px 0 0}
.tools{display:flex;gap:8px;flex-wrap:wrap}
.btn{background:var(--card);color:var(--ink2);border:1px solid var(--line2);border-radius:8px;
  padding:7px 11px;font-size:13px}
.btn:hover{border-color:var(--ink3);color:var(--ink)}
.btn.primary{background:var(--acc);border-color:var(--acc);color:#fff}
.btn.primary:hover{opacity:.9;color:#fff}
.stats{padding:10px 20px 0;display:flex;gap:10px;flex-wrap:wrap;align-items:center}
.stat{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:7px 12px;font-size:13px}
.stat b{font-size:15px}
.stat i{color:var(--ink3);font-style:normal;margin-left:5px;font-size:12px}
.meter{flex:1;min-width:200px;height:10px;background:var(--sunk);border:1px solid var(--line);
  border-radius:6px;overflow:hidden;display:flex}
.meter span{display:block;height:100%}
.split{flex:1;display:grid;grid-template-columns:1fr 1fr;gap:14px;padding:12px 20px 20px;min-height:0}
.panel{background:var(--card);border:1px solid var(--line);border-radius:12px;display:flex;
  flex-direction:column;min-height:0;overflow:hidden}
.phead{padding:11px 14px;border-bottom:1px solid var(--line);display:flex;gap:10px;
  align-items:center;justify-content:space-between;background:var(--sunk)}
.phead h2{font-size:14px;margin:0}
.phead .dim{color:var(--ink3);font-size:12.5px;font-weight:400}
.plist{overflow:auto;padding:8px;flex:1}
input[type=text],input[type=search],select{background:var(--card);color:var(--ink);
  border:1px solid var(--line2);border-radius:8px;padding:6px 9px;font:inherit;font-size:13px}
input:focus,select:focus{outline:2px solid var(--acc);outline-offset:-1px}
.group{border:1px solid var(--line);border-radius:10px;margin-bottom:8px;background:var(--card)}
.group.over{outline:2px dashed var(--acc);outline-offset:2px}
.grow{display:flex;align-items:center;gap:9px;padding:9px 11px}
.grow.sub{padding:7px 11px 7px 34px;border-top:1px solid var(--line)}
.grow:hover .acts{opacity:1}
.ic{width:20px;height:20px;flex:none;color:var(--ink2)}
.grow.sub .ic{width:17px;height:17px}
.nm{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.grow.sub .nm{font-size:13.5px;color:var(--ink2)}
.cnt{font-size:12px;color:var(--ink3);font-variant-numeric:tabular-nums;white-space:nowrap}
.cnt.zero{opacity:.45}
.acts{display:flex;gap:3px;opacity:0;transition:opacity .12s}
.icon-btn{background:none;border:none;color:var(--ink3);padding:3px 5px;border-radius:6px;font-size:13px;line-height:1}
.icon-btn:hover{background:var(--sunk);color:var(--ink)}
.icon-btn.danger:hover{background:var(--negbg);color:var(--neg)}
.tag{font-size:11px;padding:1px 7px;border-radius:999px;font-weight:600;white-space:nowrap}
.tag.expense{background:var(--negbg);color:var(--neg)}
.tag.income{background:var(--keepbg);color:var(--keep)}
.tag.other,.tag.service{background:var(--sunk);color:var(--ink3)}
.tag.place{background:var(--placebg);color:var(--place)}
.tag.keep{background:var(--keepbg);color:var(--keep)}
.tag.new{background:var(--newbg);color:var(--new)}
.tag.open{background:var(--sunk);color:var(--open)}
.tag.transfer{background:var(--sunk);color:var(--ink3)}
.wgroup{border:1px solid var(--line);border-radius:10px;margin-bottom:8px;background:var(--card)}
.whead{display:flex;align-items:center;gap:9px;padding:9px 11px;cursor:pointer;user-select:none}
.whead:hover{background:var(--sunk)}
.chev{width:14px;height:14px;flex:none;color:var(--ink3);transition:transform .15s}
.wgroup.shut .chev{transform:rotate(-90deg)}
.wgroup.shut .wbody{display:none}
.wrow{display:flex;align-items:center;gap:9px;padding:7px 11px 7px 34px;border-top:1px solid var(--line)}
.wrow:hover{background:var(--sunk)}
.wrow:hover .acts{opacity:1}
.wrow.done .nm{color:var(--ink3)}
.wrow .nm{font-size:13.5px;color:var(--ink2)}
.dot{width:5px;height:5px;border-radius:50%;background:var(--line2);flex:none;margin-left:-13px}
.wrow.done .dot{background:var(--keep)}
.wrow .tgt{font-size:12px;color:var(--ink3);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:38%}
.filters{display:flex;gap:6px;padding:8px 10px;border-bottom:1px solid var(--line);flex-wrap:wrap;align-items:center}
.chip{background:var(--card);border:1px solid var(--line2);color:var(--ink3);border-radius:999px;
  padding:4px 10px;font-size:12.5px}
.chip.on{background:var(--accbg);border-color:var(--acc);color:var(--acc);font-weight:600}
.add{width:100%;border:1px dashed var(--line2);background:none;color:var(--ink3);border-radius:10px;
  padding:9px;font-size:13px}
.group.places{border-style:dashed}
.grow.place{padding:7px 11px 7px 34px;border-top:1px solid var(--line)}
.grow.place .nm{font-size:13.5px;color:var(--ink2)}
.grow.place:hover .acts{opacity:1}
.tgt2{font-size:12px;color:var(--ink3);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:45%}
@keyframes flash{0%{background:var(--accbg)}100%{background:transparent}}
.flash{animation:flash 1.4s ease-out}
.add:hover{border-color:var(--acc);color:var(--acc)}
.veil{position:fixed;inset:0;background:rgba(0,0,0,.42);display:none;align-items:center;
  justify-content:center;z-index:20;padding:20px}
.veil.on{display:flex}
.modal{background:var(--card);border-radius:14px;padding:18px;width:min(560px,100%);
  max-height:88vh;overflow:auto;border:1px solid var(--line)}
.modal h3{margin:0 0 14px;font-size:16px}
.field{margin-bottom:12px}
.field label{display:block;font-size:12px;color:var(--ink3);margin-bottom:4px}
.field input,.field select{width:100%}
.hint{font-size:11.5px;color:var(--ink3);margin-top:4px}
.icons{display:grid;grid-template-columns:repeat(auto-fill,minmax(40px,1fr));gap:5px;
  max-height:210px;overflow:auto;padding:6px;background:var(--sunk);border-radius:9px}
.icons button{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:7px;
  display:flex;align-items:center;justify-content:center;color:var(--ink2)}
.icons button.on{border-color:var(--acc);background:var(--accbg);color:var(--acc)}
.row-end{display:flex;gap:8px;justify-content:flex-end;margin-top:16px}
.modes{display:flex;gap:6px;flex-wrap:wrap}
.modes .chip{padding:6px 12px;font-size:13px}
.wrow{cursor:pointer}
footer{padding:0 20px 14px;color:var(--ink3);font-size:12px}
code{background:var(--sunk);border-radius:4px;padding:1px 5px;font-size:11.5px}
</style>
</head>
<body>
<div class="top">
  <div>
    <h1>Набор категорий: верстак</h1>
    <p class="sub">Слева — наш стартовый набор, его можно править. Справа — категории Wallet с числом
      операций. Итог выгружается в JSON той же формы, что файл стартового набора.</p>
  </div>
  <div class="tools">
    <button class="btn" onclick="flip()">Тема</button>
    <button class="btn" onclick="reset()">Сбросить</button>
    <label class="btn" style="display:inline-flex;align-items:center">Импорт JSON
      <input type="file" accept=".json,application/json" style="display:none" onchange="importFile(event)">
    </label>
    <button class="btn primary" onclick="exportJson()">Экспорт JSON</button>
  </div>
</div>

<div class="stats" id="stats"></div>

<div class="split">
  <section class="panel">
    <div class="phead">
      <h2>Наш набор <span class="dim" id="leftCount"></span></h2>
      <span class="dim">число справа — операций Wallet, которые сюда лягут · подкатегорию можно перетащить</span>
    </div>
    <div class="plist" id="left"></div>
  </section>

  <section class="panel">
    <div class="phead">
      <h2>Категории Wallet <span class="dim" id="rightCount"></span></h2>
      <span class="dim">строка открывает выбор, куда её переносить · число справа — операций в Wallet</span>
    </div>
    <div class="filters">
      <input type="search" id="q" placeholder="поиск" oninput="render()" style="flex:1;min-width:120px">
      <button class="chip on" data-f="all" onclick="setFilter(this)">все</button>
      <button class="chip" data-f="todo" onclick="setFilter(this)">не покрытые</button>
      <button class="chip" data-f="new" onclick="setFilter(this)">новые</button>
      <button class="chip" data-f="place" onclick="setFilter(this)">места</button>
      <button class="chip" onclick="foldAll()" title="Свернуть или развернуть все группы">⌄⌃</button>
    </div>
    <div class="plist" id="right"></div>
  </section>
</div>

<footer id="foot"></footer>

<div class="veil" id="veil" onclick="if(event.target===this)closeEditor()">
  <div class="modal" id="modal"></div>
</div>

<script id="seed" type="application/json">__PAYLOAD__</script>
<script>
const SEED = JSON.parse(document.getElementById('seed').textContent);
const STORE = 'finance.categories.workbench';
const RU = 'абвгдеёжзийклмнопрстуфхцчшщъыьэюя';
const LAT = ['a','b','v','g','d','e','e','zh','z','i','y','k','l','m','n','o','p','r','s','t',
             'u','f','h','c','ch','sh','sch','','y','','e','yu','ya'];
let state, filter = 'all', seq = 1, collapsed = new Set();

/* ---------- модель ---------- */
function fromPreset(preset){
  return {
    presetVersion: preset.presetVersion,
    namespace: preset.namespace,
    seededAtUtc: preset.seededAtUtc,
    note: preset.note,
    groups: preset.groups.map(g => ({
      uid: 'g' + (seq++), key: g.key, name: g.name, kind: g.kind, icon: g.icon,
      role: g.role, id: g.id,
      subs: g.subcategories.map(s => ({
        uid: 's' + (seq++), key: s.key, name: s.name, icon: s.icon, role: s.role, id: s.id
      }))
    })),
    map: buildMap(preset)
  };
}

/* Решение по каждой категории Wallet: куда её переносить. Одна таблица на всё —
   справочник мест выводится из неё же, иначе имя места разошлось бы с решением. */
function buildMap(preset){
  const map = {};
  for (const rule of (preset.mapping || [])) {
    if (!rule || !rule.wallet) continue;
    map[rule.wallet] = { mode: rule.mode || 'sub', target: rule.target || '', place: rule.place || '' };
  }
  for (const p of (preset.places || [])) {
    for (const source of (p.sources || [])) {
      map[source] = { mode: 'place', target: p.target || '', place: p.name };
    }
  }
  return map;
}
function save(){ localStorage.setItem(STORE, JSON.stringify(state)); }
function load(){
  const raw = localStorage.getItem(STORE);
  if (raw) {
    try {
      const saved = JSON.parse(raw);
      if (!saved.map) {
        saved.map = {};
        for (const p of (saved.places || [])) {
          for (const source of (p.sources || [])) {
            saved.map[source] = { mode: 'place', target: p.target || '', place: p.name };
          }
        }
      }
      delete saved.places;
      return saved;
    } catch (e) { /* испорчено — берём набор */ }
  }
  return fromPreset(SEED.preset);
}
function reset(){
  if (!confirm('Вернуть набор к тому, что лежит в файле стартового набора?')) return;
  localStorage.removeItem(STORE);
  state = fromPreset(SEED.preset);
  render();
}

function pl(n, one, few, many){
  const t = n % 10, h = n % 100;
  if (h >= 11 && h <= 14) return many;
  if (t === 1) return one;
  if (t >= 2 && t <= 4) return few;
  return many;
}

function slug(name){
  let out = '';
  for (const ch of (name || '').toLowerCase()) {
    const i = RU.indexOf(ch);
    if (i >= 0) out += LAT[i];
    else if (/[a-z0-9]/.test(ch)) out += ch;
    else if (/[\s\-_/]/.test(ch)) out += '-';
  }
  return out.replace(/-+/g, '-').replace(/^-|-$/g, '') || 'category';
}

/* ---------- значки ---------- */
function icon(key, cls){
  const d = SEED.iconPaths[key] || SEED.iconPaths[SEED.fallbackIcon] || '';
  return '<svg class="' + (cls || 'ic') + '" viewBox="0 0 24 24" fill="none" stroke="currentColor"' +
    ' stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="' + d + '"/></svg>';
}
function esc(text){
  return String(text).replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
}

/* ---------- сколько операций Wallet ложится на пару «группа / подкатегория» ---------- */
function ruleOf(w){
  const custom = state.map[w.name];
  if (custom) return custom;
  if (w.decision === 'transfer') return { mode: 'transfer', target: '', place: '' };
  if (w.decision === 'place') return { mode: 'place', target: w.target || '', place: '' };
  if (w.decision === 'open') return { mode: 'open', target: '', place: '' };
  return { mode: 'sub', target: w.target || '', place: '' };
}
function isMine(rule){ return rule.mode === 'sub' || rule.mode === 'place'; }

function walletIndex(){
  const map = new Map();
  for (const w of SEED.wallet) {
    const rule = ruleOf(w);
    if (!isMine(rule) || !rule.target) continue;
    const box = map.get(rule.target) || { count: 0, names: [] };
    box.count += w.count; box.names.push(w.name);
    map.set(rule.target, box);
  }
  return map;
}
// Пересобирается в начале каждой отрисовки: зависит от принятых решений,
// а они живут в state, которое поднимается ниже
let WALLET = new Map();
const TOTAL = SEED.wallet.reduce((sum, w) => sum + w.count, 0);
const TRANSFERS = SEED.wallet.filter(w => w.decision === 'transfer')
  .reduce((sum, w) => sum + w.count, 0);

function pairOf(group, sub){ return group.name + ' / ' + sub.name; }
function hitsOf(group, sub){ return WALLET.get(pairOf(group, sub)); }
function existingPairs(){
  const set = new Set();
  for (const g of state.groups) for (const s of g.subs) set.add(pairOf(g, s));
  return set;
}

/* ---------- отрисовка ---------- */
function render(){
  WALLET = walletIndex();
  const left = document.getElementById('left');
  left.innerHTML = state.groups.map(renderGroup).join('') +
    '<button class="add" onclick="editGroup(null)">+ Группа</button>' +
    renderPlaces();

  const pairs = existingPairs();
  const q = document.getElementById('q').value.trim().toLowerCase();
  const buckets = new Map();
  for (const w of SEED.wallet) {
    const rule = ruleOf(w);
    if (q && !(w.name.toLowerCase().includes(q) || (rule.target || '').toLowerCase().includes(q) ||
      (rule.place || '').toLowerCase().includes(q))) continue;
    const covered = rule.mode === 'transfer' || rule.mode === 'skip' ||
      (!!rule.target && pairs.has(rule.target));
    if (filter === 'todo' && covered) continue;
    if (filter === 'new' && w.decision !== 'new') continue;
    if (filter === 'place' && rule.mode !== 'place') continue;
    (buckets.get(w.group) || buckets.set(w.group, []).get(w.group)).push({ w, rule, covered });
  }
  const right = document.getElementById('right');
  const cards = [...buckets.entries()]
    .map(([name, list]) => ({ name, list, total: list.reduce((n, x) => n + x.w.count, 0) }))
    .sort((a, b) => b.total - a.total);
  right.innerHTML = cards.map(card => renderWalletGroup(card, !!q)).join('') ||
    '<p class="sub" style="padding:10px">Ничего не нашлось</p>';

  const subs = state.groups.reduce((n, g) => n + g.subs.length, 0);
  const np = places().length;
  document.getElementById('leftCount').textContent =
    state.groups.length + ' ' + pl(state.groups.length, 'группа', 'группы', 'групп') + ' · ' +
    subs + ' ' + pl(subs, 'подкатегория', 'подкатегории', 'подкатегорий') + ' · ' +
    np + ' ' + pl(np, 'место', 'места', 'мест');
  document.getElementById('rightCount').textContent = SEED.wallet.length + ' категорий';
  renderStats(pairs);
  save();
}

function renderGroup(g){
  const kindTag = '<span class="tag ' + g.kind + '">' + (g.kind === 'income' ? 'доход' : 'расход') + '</span>';
  const total = g.subs.reduce((n, s) => n + ((hitsOf(g, s) || {}).count || 0), 0);
  const head =
    '<div class="grow" ondragover="dragOver(event,\'' + g.uid + '\')" ondrop="drop(event,\'' + g.uid + '\')">' +
      icon(g.icon) +
      '<span class="nm"><b>' + esc(g.name) + '</b></span>' + kindTag +
      '<span class="cnt' + (total ? '' : ' zero') + '">' + total + '</span>' +
      '<span class="acts">' +
        '<button class="icon-btn" title="Подкатегория" onclick="editSub(\'' + g.uid + '\',null)">＋</button>' +
        '<button class="icon-btn" title="Правка" onclick="editGroup(\'' + g.uid + '\')">✎</button>' +
        (g.role === 'service' ? '' :
          '<button class="icon-btn danger" title="Удалить" onclick="delGroup(\'' + g.uid + '\')">✕</button>') +
      '</span>' +
    '</div>';
  const subs = g.subs.map(s => {
    const hit = hitsOf(g, s);
    const tag = s.role === 'other' ? '<span class="tag other">приёмник</span>'
      : s.role === 'service' ? '<span class="tag service">служебная</span>' : '';
    const tip = hit ? ' title="Из Wallet: ' + esc(hit.names.join(', ')) + '"' : '';
    return '<div class="grow sub" data-pair="' + esc(pairOf(g, s)) + '" draggable="true"' +
      ' ondragstart="dragStart(event,\'' + g.uid + '\',\'' + s.uid + '\')"' + tip + '>' +
      icon(s.icon) + '<span class="nm">' + esc(s.name) + '</span>' + tag +
      '<span class="cnt' + (hit ? '' : ' zero') + '">' + (hit ? hit.count : 0) + '</span>' +
      '<span class="acts">' +
        '<button class="icon-btn" title="Правка" onclick="editSub(\'' + g.uid + '\',\'' + s.uid + '\')">✎</button>' +
        (s.role === 'normal' ?
          '<button class="icon-btn danger" title="Удалить" onclick="delSub(\'' + g.uid + '\',\'' + s.uid + '\')">✕</button>' : '') +
      '</span></div>';
  }).join('');
  return '<div class="group" id="' + g.uid + '">' + head + subs + '</div>';
}

function places(){
  const byName = new Map();
  for (const w of SEED.wallet) {
    const rule = ruleOf(w);
    if (rule.mode !== 'place' || !rule.place) continue;
    const box = byName.get(rule.place) ||
      { name: rule.place, target: rule.target, count: 0, sources: [] };
    box.count += w.count;
    box.sources.push(w.name);
    byName.set(rule.place, box);
  }
  return [...byName.values()].sort((a, b) => b.count - a.count);
}

function renderPlaces(){
  const list = places();
  const rows = list.map(p =>
    '<div class="grow place">' + icon('tag') +
      '<span class="nm">' + esc(p.name) + '</span>' +
      '<span class="tgt2">' + esc(p.target) + '</span>' +
      '<span class="cnt" title="операций Wallet за этим местом">' + p.count + '</span>' +
      '<span class="acts">' +
        '<button class="icon-btn" title="Переименовать" onclick="renamePlace(\'' + esc(p.name) + '\')">✎</button>' +
        '<button class="icon-btn danger" title="Убрать из справочника" onclick="dropPlace(\'' + esc(p.name) + '\')">✕</button>' +
      '</span></div>').join('');
  const total = list.reduce((n, p) => n + p.count, 0);
  return '<div class="group places" style="margin-top:12px">' +
    '<div class="grow">' + icon('tag') +
      '<span class="nm"><b>Места</b></span>' +
      '<span class="cnt">' + list.length + ' ' +
        pl(list.length, 'место', 'места', 'мест') + ' в справочнике</span>' +
      '<span class="cnt' + (total ? '' : ' zero') + '">' + total + '</span>' +
    '</div>' +
    (rows || '<div class="grow place"><span class="nm dim">Пусто: откройте строку справа и выберите «Место»</span></div>') +
    '</div>';
}

function renderWalletGroup(card, searching){
  const shut = !searching && collapsed.has(card.name);
  const icon_ = SEED.groupIcons[card.name] || 'question-mark';
  const covered = card.list.filter(x => x.covered).length;
  const head =
    '<div class="whead" onclick="toggleGroup(\'' + esc(card.name) + '\')">' +
      '<svg class="chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"' +
        ' stroke-linecap="round" stroke-linejoin="round"><path d="M6 9l6 6l6 -6"/></svg>' +
      icon(icon_) +
      '<span class="nm"><b>' + esc(card.name) + '</b></span>' +
      '<span class="cnt">' + covered + ' из ' + card.list.length + '</span>' +
      '<span class="cnt">' + card.total + '</span>' +
    '</div>';
  const rows = card.list
    .slice()
    .sort((a, b) => b.w.count - a.w.count)
    .map(renderWalletRow).join('');
  return '<div class="wgroup' + (shut ? ' shut' : '') + '">' + head +
    '<div class="wbody">' + rows + '</div></div>';
}

function renderWalletRow(item){
  const w = item.w;
  const rule = item.rule;
  const index = SEED.wallet.indexOf(w);
  const custom = !!state.map[w.name];

  const tags = { sub: 'в набор', place: 'место', transfer: 'перевод', skip: 'не переносим', open: 'решить' };
  const tagClass = { sub: 'keep', place: 'place', transfer: 'transfer', skip: 'open', open: 'open' };
  const tagText = (rule.mode === 'place' && rule.place ? 'место ✓' : tags[rule.mode]) +
    (custom ? ' ·' : '');

  let act = '';
  if (rule.mode === 'sub' && item.covered) {
    act = '<button class="icon-btn" title="Показать в наборе слева" ' +
      'onclick="event.stopPropagation();reveal(\'' + esc(rule.target) + '\')">⤢</button>';
  } else if (rule.mode === 'sub' && rule.target) {
    act = '<button class="icon-btn" title="Завести такую подкатегорию в наборе" ' +
      'onclick="event.stopPropagation();fromWallet(' + index + ')">→</button>';
  }
  act += '<button class="icon-btn" title="Куда переносить" ' +
    'onclick="event.stopPropagation();openTarget(' + index + ')">⋯</button>';

  const what = w.name.startsWith('без имени')
    ? 'Стандартная категория Wallet без своего названия: оно живёт в переводах приложения, ' +
      'в документе его нет. Номер конверта говорит, что это за категория.'
    : w.name;
  const where = rule.mode === 'transfer' ? 'Перевод: категория не нужна вовсе'
    : rule.mode === 'skip' ? 'Решено не переносить'
    : rule.mode === 'place' ? 'Место «' + (rule.place || w.name) + '», операции лягут в «' + rule.target + '»'
    : rule.target ? 'Операции лягут в «' + rule.target + '»' +
        (item.covered ? ' — эта подкатегория в наборе уже есть' : ' — такой подкатегории в наборе пока нет')
    : 'Решение ещё не принято';

  const shown = rule.mode === 'place' && rule.place ? rule.place + ' → ' + rule.target : rule.target;

  return '<div class="wrow' + (item.covered ? ' done' : '') + '" onclick="openTarget(' + index + ')" title="' +
      esc(what + '\n' + where + (custom ? '\nВыбор изменён вручную' : '') + '\n' + w.count + ' ' +
        pl(w.count, 'операция', 'операции', 'операций') + ' в Wallet') + '">' +
    '<span class="dot"></span>' +
    '<span class="nm">' + esc(w.name) + '</span>' +
    '<span class="tag ' + tagClass[rule.mode] + '">' + tagText + '</span>' +
    '<span class="tgt">' + esc(rule.mode === 'open' ? '' : (shown || '')) + '</span>' +
    '<span class="cnt" title="операций в этой категории Wallet">' + w.count + '</span>' +
    '<span class="acts">' + act + '</span></div>';
}

function reveal(pair){
  const node = document.querySelector('[data-pair="' + pair.replace(/"/g, '&quot;') + '"]');
  if (!node) return;
  node.scrollIntoView({ block: 'center', behavior: 'smooth' });
  node.classList.remove('flash');
  void node.offsetWidth;
  node.classList.add('flash');
}

function toggleGroup(name){
  if (collapsed.has(name)) collapsed.delete(name); else collapsed.add(name);
  render();
}
function foldAll(){
  const all = new Set(SEED.wallet.map(w => w.group));
  if (collapsed.size >= all.size) collapsed.clear(); else collapsed = all;
  render();
}

function renderStats(pairs){
  let covered = 0, open = 0, aside = 0;
  for (const w of SEED.wallet) {
    const rule = ruleOf(w);
    if (rule.mode === 'transfer' || rule.mode === 'skip') { aside += w.count; continue; }
    if (rule.target && pairs.has(rule.target)) covered += w.count; else open += w.count;
  }
  const pc = n => (n / TOTAL * 100).toFixed(1) + '%';
  document.getElementById('stats').innerHTML =
    '<div class="stat"><b>' + covered + '</b><i>операций Wallet ложится на набор</i></div>' +
    '<div class="stat"><b>' + open + '</b><i>пока некуда</i></div>' +
    '<div class="stat"><b>' + aside + '</b><i>переводы и то, что не переносим</i></div>' +
    '<div class="meter" title="' + pc(covered) + ' покрыто">' +
      '<span style="width:' + (covered / TOTAL * 100) + '%;background:var(--keep)"></span>' +
      '<span style="width:' + (aside / TOTAL * 100) + '%;background:var(--line2)"></span>' +
      '<span style="width:' + (open / TOTAL * 100) + '%;background:var(--new)"></span>' +
    '</div>';
  document.getElementById('foot').innerHTML =
    'Счёт операций считается по паре «группа / подкатегория»: переименование оторвёт его от категории Wallet — это нормально, ' +
    'соответствия всё равно задаются отдельной таблицей. Выгрузка от ' + SEED.generatedAt +
    ' · страница собрана скриптом <code>tasks/wallet-import/tools/wallet_workbench.py</code>';
}

/* ---------- правка ---------- */
function groupBy(uid){ return state.groups.find(g => g.uid === uid); }

function editGroup(uid){
  const g = uid ? groupBy(uid) : null;
  openEditor({
    title: g ? 'Группа' : 'Новая группа',
    name: g ? g.name : '',
    key: g ? g.key : '',
    icon: g ? g.icon : 'basket',
    kind: g ? g.kind : 'expense',
    withKind: true,
    onSave: (data) => {
      if (g) { g.name = data.name; g.icon = data.icon; g.kind = data.kind; g.key = data.key; }
      else state.groups.push({ uid: 'g' + (seq++), key: data.key, name: data.name, kind: data.kind,
        icon: data.icon, role: 'normal', subs: [
          { uid: 's' + (seq++), key: data.key + '.other', name: 'Прочее', icon: 'dots', role: 'other' }
        ] });
    }
  });
}

function editSub(gid, uid, preset){
  const g = groupBy(gid);
  const s = uid ? g.subs.find(x => x.uid === uid) : null;
  openEditor({
    title: s ? 'Подкатегория' : 'Новая подкатегория в «' + g.name + '»',
    name: s ? s.name : (preset ? preset.name : ''),
    key: s ? s.key : '',
    icon: s ? s.icon : (preset ? preset.icon : g.icon),
    groupUid: gid,
    withGroup: !s,
    onSave: (data) => {
      const target = data.groupUid ? groupBy(data.groupUid) : g;
      if (s) { s.name = data.name; s.icon = data.icon; s.key = data.key; }
      else {
        const fresh = { uid: 's' + (seq++), key: data.key, name: data.name,
          icon: data.icon, role: 'normal' };
        const receiver = target.subs.findIndex(x => x.role !== 'normal');
        if (receiver < 0) target.subs.push(fresh); else target.subs.splice(receiver, 0, fresh);
      }
    }
  });
}

function fromWallet(index){
  const w = SEED.wallet[index];
  const target = (w.target || '').split(' / ');
  const group = state.groups.find(g => g.name === target[0]) ||
    state.groups.find(g => g.kind === (w.income ? 'income' : 'expense')) || state.groups[0];
  if (!group) { alert('Сначала заведите хотя бы одну группу'); return; }
  editSub(group.uid, null, { name: target[1] || w.name, icon: group.icon });
}

function openTarget(index){
  const w = SEED.wallet[index];
  const rule = ruleOf(w);
  const mode = rule.mode === 'transfer' ? 'skip' : rule.mode === 'open' ? 'sub' : rule.mode;
  const pairs = state.groups.flatMap(g => g.subs.map(s => pairOf(g, s)));
  const known = pairs.includes(rule.target);
  const options = (known || !rule.target ? pairs : [rule.target].concat(pairs))
    .map(pair => '<option value="' + esc(pair) + '"' + (pair === rule.target ? ' selected' : '') + '>' +
      esc(pair) + (pair === rule.target && !known ? '  — такой подкатегории в наборе пока нет' : '') +
      '</option>').join('');

  document.getElementById('modal').innerHTML =
    '<h3>Куда переносить «' + esc(w.name) + '»</h3>' +
    '<p class="hint" style="margin:-8px 0 14px">' + w.count + ' ' +
      pl(w.count, 'операция', 'операции', 'операций') + ' · в Wallet лежит в группе «' +
      esc(w.group) + '»' + (state.map[w.name] ? ' · выбор изменён вручную' : ' · предложение по умолчанию') +
      '</p>' +
    '<div class="field"><label>Чем эта категория станет у нас</label>' +
      '<div class="modes">' +
        ['sub', 'place', 'skip'].map(m =>
          '<button type="button" class="chip' + (m === mode ? ' on' : '') + '" data-mode="' + m + '"' +
          ' onclick="pickMode(this)">' +
          { sub: 'Подкатегорией', place: 'Местом', skip: 'Не переносим' }[m] + '</button>').join('') +
      '</div></div>' +
    '<div class="field" id="fPlaceBox"><label>Название места — с ним оно попадёт в справочник</label>' +
      '<input type="text" id="tPlace" value="' + esc(rule.place || w.name) + '"></div>' +
    '<div class="field" id="fTargetBox"><label>Операции лягут в подкатегорию</label>' +
      '<select id="tTarget">' + options + '</select>' +
      '<p class="hint">Нужной подкатегории в списке нет? Заведите её слева кнопкой «＋» у группы.</p></div>' +
    '<div class="row-end">' +
      (state.map[w.name] ? '<button class="btn" onclick="resetTarget(' + index + ')">Вернуть предложенное</button>' : '') +
      '<button class="btn" onclick="closeEditor()">Отмена</button>' +
      '<button class="btn primary" onclick="commitTarget(' + index + ')">Сохранить</button>' +
    '</div>';
  document.getElementById('veil').classList.add('on');
  window.__editor = { targeting: true, index, mode };
  syncModes();
}

function pickMode(btn){
  window.__editor.mode = btn.dataset.mode;
  document.querySelectorAll('.modes .chip').forEach(c => c.classList.toggle('on', c === btn));
  syncModes();
}

function syncModes(){
  const mode = window.__editor.mode;
  document.getElementById('fPlaceBox').style.display = mode === 'place' ? '' : 'none';
  document.getElementById('fTargetBox').style.display = mode === 'skip' ? 'none' : '';
}

function commitTarget(index){
  const w = SEED.wallet[index];
  const mode = window.__editor.mode;
  const target = mode === 'skip' ? '' : (document.getElementById('tTarget').value || '');
  const place = mode === 'place' ? document.getElementById('tPlace').value.trim() : '';
  if (mode === 'place' && !place) { alert('Название места пустое'); return; }
  if (mode !== 'skip' && !target) { alert('Выберите подкатегорию'); return; }
  state.map[w.name] = { mode, target, place };
  closeEditor();
  render();
}

function resetTarget(index){
  delete state.map[SEED.wallet[index].name];
  closeEditor();
  render();
}

function renamePlace(name){
  const fresh = prompt('Название места', name);
  if (fresh === null) return;
  const trimmed = fresh.trim();
  if (!trimmed) return;
  for (const rule of Object.values(state.map)) {
    if (rule.mode === 'place' && rule.place === name) rule.place = trimmed;
  }
  render();
}

function dropPlace(name){
  if (!confirm('Убрать место «' + name + '» из справочника? Операции останутся в своей подкатегории.')) return;
  for (const rule of Object.values(state.map)) {
    if (rule.mode === 'place' && rule.place === name) { rule.mode = 'sub'; rule.place = ''; }
  }
  render();
}

function delGroup(uid){
  const g = groupBy(uid);
  const kept = g.subs.filter(s => s.role === 'normal').length;
  if (!confirm('Удалить группу «' + g.name + '»' + (kept ? ' и ' + kept + ' подкатегорий?' : '?'))) return;
  state.groups = state.groups.filter(x => x.uid !== uid);
  render();
}
function delSub(gid, uid){
  const g = groupBy(gid);
  const s = g.subs.find(x => x.uid === uid);
  if (!confirm('Удалить подкатегорию «' + s.name + '»?')) return;
  g.subs = g.subs.filter(x => x.uid !== uid);
  render();
}

function openEditor(opts){
  const keyHint = opts.key ? '' : ' (выведен из названия, правится)';
  const groups = state.groups.map(g =>
    '<option value="' + g.uid + '"' + (g.uid === opts.groupUid ? ' selected' : '') + '>' +
    esc(g.name) + '</option>').join('');
  document.getElementById('modal').innerHTML =
    '<h3>' + esc(opts.title) + '</h3>' +
    '<div class="field"><label>Название</label><input type="text" id="fName" value="' +
      esc(opts.name) + '" oninput="syncKey()"></div>' +
    (opts.withGroup ? '<div class="field"><label>Группа</label><select id="fGroup">' + groups + '</select></div>' : '') +
    (opts.withKind ? '<div class="field"><label>Вид</label><select id="fKind">' +
      '<option value="expense"' + (opts.kind === 'expense' ? ' selected' : '') + '>Расход</option>' +
      '<option value="income"' + (opts.kind === 'income' ? ' selected' : '') + '>Доход</option>' +
      '</select></div>' : '') +
    '<div class="field"><label>Ключ' + keyHint + '</label><input type="text" id="fKey" value="' +
      esc(opts.key) + '"><p class="hint">Ключ неизменен после выпуска: из него выводится идентификатор категории.</p></div>' +
    '<div class="field"><label>Значок</label><div class="icons" id="fIcons">' +
      SEED.icons.map(k => '<button type="button" data-icon="' + k + '" title="' + k + '"' +
        (k === opts.icon ? ' class="on"' : '') + ' onclick="pickIcon(this)">' + icon(k) + '</button>').join('') +
      '</div></div>' +
    '<div class="row-end"><button class="btn" onclick="closeEditor()">Отмена</button>' +
      '<button class="btn primary" onclick="commit()">Сохранить</button></div>';
  document.getElementById('veil').classList.add('on');
  document.getElementById('fName').focus();
  window.__editor = opts;
  window.__icon = opts.icon;
  if (!opts.key) syncKey();
}
function pickIcon(btn){
  document.querySelectorAll('#fIcons .on').forEach(b => b.classList.remove('on'));
  btn.classList.add('on');
  window.__icon = btn.dataset.icon;
}
function syncKey(){
  const opts = window.__editor;
  if (!opts || opts.key) return;
  const name = document.getElementById('fName').value;
  const sel = document.getElementById('fGroup');
  const gid = sel ? sel.value : opts.groupUid;
  const parent = gid ? groupBy(gid) : null;
  document.getElementById('fKey').value = parent ? parent.key + '.' + slug(name) : slug(name);
}
function commit(){
  const opts = window.__editor;
  const name = document.getElementById('fName').value.trim();
  if (!name) { alert('Название пустое'); return; }
  const sel = document.getElementById('fGroup');
  const kindSel = document.getElementById('fKind');
  opts.onSave({
    name,
    key: document.getElementById('fKey').value.trim() || slug(name),
    icon: window.__icon,
    kind: kindSel ? kindSel.value : undefined,
    groupUid: sel ? sel.value : opts.groupUid
  });
  closeEditor();
  render();
}
function closeEditor(){ document.getElementById('veil').classList.remove('on'); window.__editor = null; }
document.addEventListener('keydown', e => {
  if (!window.__editor) return;
  if (e.key === 'Escape') closeEditor();
  if (e.key === 'Enter' && e.target.tagName !== 'BUTTON') {
    e.preventDefault();
    if (window.__editor.targeting) commitTarget(window.__editor.index); else commit();
  }
});

/* ---------- перетаскивание ---------- */
let dragged = null;
function dragStart(e, gid, uid){ dragged = { gid, uid }; e.dataTransfer.effectAllowed = 'move'; }
function dragOver(e, gid){
  if (!dragged || dragged.gid === gid) return;
  e.preventDefault();
  document.getElementById(gid).classList.add('over');
}
function drop(e, gid){
  e.preventDefault();
  document.querySelectorAll('.group.over').forEach(x => x.classList.remove('over'));
  if (!dragged || dragged.gid === gid) return;
  const from = groupBy(dragged.gid), to = groupBy(gid);
  const sub = from.subs.find(s => s.uid === dragged.uid);
  if (!sub || sub.role !== 'normal') { alert('Приёмник и служебная подкатегория не переносятся'); return; }
  if (from.kind !== to.kind) { alert('Перенос только внутри своего вида: расход к расходу, доход к доходу'); return; }
  from.subs = from.subs.filter(s => s.uid !== dragged.uid);
  sub.key = to.key + '.' + slug(sub.name);
  to.subs.push(sub);
  dragged = null;
  render();
}
document.addEventListener('dragend', () =>
  document.querySelectorAll('.group.over').forEach(x => x.classList.remove('over')));

/* ---------- обмен файлами ---------- */
function exportJson(){
  const out = {
    presetVersion: state.presetVersion,
    namespace: state.namespace,
    seededAtUtc: state.seededAtUtc,
    note: state.note,
    groups: state.groups.map(g => ({
      key: g.key, name: g.name, kind: g.kind, icon: g.icon, role: g.role,
      subcategories: g.subs.map(s => ({ key: s.key, name: s.name, icon: s.icon, role: s.role }))
    })),
    places: places().map(p => ({ name: p.name, target: p.target, sources: p.sources })),
    mapping: SEED.wallet.map(w => {
      const rule = ruleOf(w);
      return { wallet: w.name, walletGroup: w.group, count: w.count,
        mode: rule.mode, target: rule.target, place: rule.place || '' };
    })
  };
  const blob = new Blob([JSON.stringify(out, null, 2)], { type: 'application/json' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = 'categories.json';
  a.click();
}
function importFile(event){
  const file = event.target.files[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => {
    try {
      const data = JSON.parse(reader.result);
      if (!data.groups) throw new Error('нет списка групп');
      state = fromPreset(data);
      render();
    } catch (err) { alert('Файл не читается: ' + err.message); }
  };
  reader.readAsText(file);
  event.target.value = '';
}

function setFilter(btn){
  if (!btn.dataset.f) return;
  filter = btn.dataset.f;
  document.querySelectorAll('.chip').forEach(c => c.classList.toggle('on', c === btn));
  render();
}
function flip(){
  const root = document.documentElement;
  root.dataset.theme = root.dataset.theme === 'dark' ? 'light' : 'dark';
}

state = load();
render();
</script>
</body>
</html>
"""


if __name__ == "__main__":
    main()
