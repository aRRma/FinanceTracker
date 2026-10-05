// Общий рендерер документов вариантов: одни данные, одни экраны, разный «облик» счёта.
// Документ задаёт облик (look) — как выглядит счёт в списках, в строке ленты и в карточке, —
// и размечает места под экраны атрибутами data-*; всё остальное собирает этот файл.
// Палитра цветов счёта живёт только здесь: из неё пишутся переменные CSS, таблица токенов
// с контрастом и пары, которые сливаются при дальтонизме.

(() => {
  // ---------- Палитра ----------
  let PALETTE = [
    // Порядок — очередь, в которой новый счёт получает цвет сам. Первые четыре попарно различимы
    // и при дейтеранопии, и при протанопии (ΔE не меньше 14 после симуляции); дальше пары
    // сливаются, и различать помогает название или метка. Красный — цвет расхода — последним
    { key: "blue", name: "синий", light: "#1F5FCC", dark: "#93B5FF" },
    { key: "amber", name: "янтарный", light: "#8A6200", dark: "#E9C463" },
    { key: "pink", name: "розовый", light: "#B8185A", dark: "#FF8FBA" },
    { key: "cyan", name: "бирюзовый", light: "#00727D", dark: "#62D2DC" },
    { key: "green", name: "зелёный", light: "#2B7A32", dark: "#7DD88F" },
    { key: "violet", name: "фиолетовый", light: "#6A45C7", dark: "#B9A0FF" },
    { key: "orange", name: "оранжевый", light: "#B04C00", dark: "#FFAE70" },
    { key: "graphite", name: "графитовый", light: "#5D6873", dark: "#A9B3BC" },
    { key: "brown", name: "коричневый", light: "#87573A", dark: "#D9AC8C" },
    { key: "crimson", name: "красный", light: "#B3261E", dark: "#FF907F" },
  ];
  // Палитра «Предельная насыщенность» — выбор пользователя: самый насыщенный цвет каждого тона,
  // чуть внутри границы sRGB (tools/palette.cs, режим list). Очередь собрана по парам из режима
  // hex, розовый — в конце по просьбе пользователя. Первые три разведены и обычным зрением
  // (худшая пара синий — голубой 14,3), и при дальтонизме; четвёртый, зелёный, при дальтонизме
  // близок к оранжевому (5,0 при минимуме 6) — без розового лучшего четвёртого нет, их различает
  // значок. Красный — цвет расхода — предпоследним. on — значок на заливке: белый, где он
  // держит контраст не ниже 3, иначе тёмный; выбор один на обе темы
  const VIVID = [
    { key: "blue", name: "синий", light: "#1F7BF5", dark: "#3082F6", on: "light" },
    { key: "orange", name: "оранжевый", light: "#E87F26", dark: "#DB7823", on: "dark" },
    { key: "sky", name: "голубой", light: "#2CB0CA", dark: "#29A6BF", on: "dark" },
    { key: "green", name: "зелёный", light: "#2DBC55", dark: "#29B150", on: "dark" },
    { key: "violet", name: "фиолетовый", light: "#994DF5", dark: "#9E59F6", on: "light" },
    { key: "magenta", name: "пурпурный", light: "#F730F0", dark: "#EC2DE4", on: "light" },
    { key: "red", name: "красный", light: "#F72C2E", dark: "#F72C2E", on: "light" },
    { key: "pink", name: "розовый", light: "#F62B97", dark: "#F62B97", on: "light" },
  ];
  const THEME = {
    light: { card: "#FFFFFF", paper: "#EDEFF1", sunk: "#F4F6F7", tint: 0.16 },
    dark: { card: "#1C2124", paper: "#101315", sunk: "#252B2F", tint: 0.28 },
  };
  const title = s => s[0].toUpperCase() + s.slice(1);
  const token = k => "Account" + title(k);

  const rgb = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));
  const hex = a => "#" + a.map(v => Math.round(Math.min(255, Math.max(0, v))).toString(16).padStart(2, "0")).join("").toUpperCase();
  const mix = (a, b, s) => hex(rgb(a).map((v, i) => v * s + rgb(b)[i] * (1 - s)));
  const lin = c => (c /= 255) <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  const unlin = c => 255 * (c <= 0.0031308 ? 12.92 * c : 1.055 * c ** (1 / 2.4) - 0.055);
  const lum = h => { const [r, g, b] = rgb(h).map(lin); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
  const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
  const tintOf = (c, theme) => mix(c, THEME[theme].card, THEME[theme].tint);

  // Симуляция дальтонизма (Machado, Oliveira, Fernandes 2009, полная степень) и ΔE в Lab
  const SIM = {
    deutan: [[0.367322, 0.860646, -0.227968], [0.280085, 0.672501, 0.047413], [-0.01182, 0.04294, 0.968881]],
    protan: [[0.152286, 1.052583, -0.204868], [0.114503, 0.786281, 0.099216], [-0.003882, -0.048116, 1.051998]],
  };
  const simulate = (h, m) => { const v = rgb(h).map(lin); return hex(m.map(r => unlin(r[0] * v[0] + r[1] * v[1] + r[2] * v[2]))); };
  const lab = h => {
    const [r, g, b] = rgb(h).map(lin);
    const xyz = [(r * 0.4124 + g * 0.3576 + b * 0.1805) / 0.95047, r * 0.2126 + g * 0.7152 + b * 0.0722, (r * 0.0193 + g * 0.1192 + b * 0.9505) / 1.08883];
    const f = t => t > 0.008856 ? Math.cbrt(t) : 7.787 * t + 16 / 116;
    const [fx, fy, fz] = xyz.map(f);
    return [116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)];
  };
  const dE = (a, b) => { const x = lab(a), y = lab(b); return Math.hypot(x[0] - y[0], x[1] - y[1], x[2] - y[2]); };

  // ΔE по методу скилла dataviz: евклидово расстояние в OKLab ×100; пороги — 15 обычным
  // зрением, 8 при дальтонизме (6 — минимум при втором признаке)
  const oklab = v => {
    const l = Math.cbrt(0.4122214708 * v[0] + 0.5363325363 * v[1] + 0.0514459929 * v[2]);
    const m = Math.cbrt(0.2119034982 * v[0] + 0.6806995451 * v[1] + 0.1073969566 * v[2]);
    const s = Math.cbrt(0.0883024619 * v[0] + 0.2817188376 * v[1] + 0.6299787005 * v[2]);
    return [0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s, 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s, 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s];
  };
  const okE = (a, b, m) => {
    const p = h => { const v = rgb(h).map(lin); return m ? m.map(r => Math.min(1, Math.max(0, r[0] * v[0] + r[1] * v[1] + r[2] * v[2]))) : v; };
    const x = oklab(p(a)), y = oklab(p(b));
    return 100 * Math.hypot(x[0] - y[0], x[1] - y[1], x[2] - y[2]);
  };

  // Цвет значка на сплошной заливке: белый на тёмных и средних ступенях, тёмный на светлых —
  // одинаково в обеих темах, поэтому это два постоянных токена, а не по паре на цвет
  const GLYPH = { light: "#FFFFFF", dark: "#16191C" };

  function injectPalette() {
    const vars = theme => PALETTE.map(p => `--a-${p.key}:${p[theme]};--a-${p.key}-bg:${tintOf(p[theme], theme)};${p.on ? `--a-${p.key}-on:${GLYPH[p.on]};` : ""}`).join("");
    const cls = PALETTE.map(p => `.ac-${p.key}{--c:var(--a-${p.key});--cb:var(--a-${p.key}-bg)${p.on ? `;--on:var(--a-${p.key}-on)` : ""}}`).join("\n");
    document.getElementById("kit-palette")?.remove();
    const style = Object.assign(document.createElement("style"), { id: "kit-palette" });
    style.textContent = `:root,[data-theme="light"]{${vars("light")}}\n[data-theme="dark"]{${vars("dark")}}\n${cls}`;
    document.head.append(style);
  }

  // ---------- Данные: те же счета и операции, что в макетах приложения ----------
  // Цвет счёта — место в очереди палитры (q): так их раздаст миграция без настройки — по порядку
  // счетов в справочнике. Сам цвет подставляет render из палитры документа
  const A = {
    cash: { name: "Наличные", type: "cash", q: 0, label: "", glyph: "cash", bal: "12 400,00 ₽", cur: "RUB" },
    main: { name: "Карта основная", type: "card", q: 1, label: "4417", glyph: "credit-card", bal: "71 920,50 ₽", cur: "RUB" },
    credit: { name: "Карта кредитная", type: "card", q: 2, label: "0932", glyph: "percentage", bal: "-1 890,00 ₽", neg: true, cur: "RUB" },
    eur: { name: "Карта евро", type: "card", q: 3, label: "8120", glyph: "plane", bal: "1 240,00 €", cur: "EUR" },
    trip: { name: "Вклад на отпуск", type: "card", hidden: true, q: 4, label: "", glyph: "beach", bal: "250 000,00 ₽", cur: "RUB" },
    usd: { name: "Доллары дома", type: "cash", hidden: true, q: 5, label: "", glyph: "home", bal: "1 460,00 $", cur: "USD" },
    old: { name: "Карта старая", type: "card", closed: true, q: 6, label: "7781", glyph: "credit-card", bal: "0,00 ₽", cur: "RUB" },
  };
  // Значок типа — то, что сейчас отдаёт AccountIcon.For: скрытый сильнее типа
  const typeGlyph = a => a.hidden ? "building-bank" : a.type === "cash" ? "cash" : "credit-card";

  const FEED = [
    { day: "25 августа", total: "-8 650,00 ₽" },
    { t: "Продукты", s: "Пятёрочка", acc: "main", amt: "-1 250,00 ₽", k: "exp", icon: "shopping-cart" },
    { t: "Перевод", acc: "main", to: "trip", amt: "-5 000,00 ₽", k: "trf", icon: "swap" },
    { t: "Топливо", s: "Лукойл", acc: "credit", amt: "-2 400,00 ₽", k: "exp", icon: "gas-station" },
    { day: "24 августа", total: "+89 540,00 ₽", pos: true },
    { t: "Зарплата", s: "Доход", acc: "main", amt: "+90 000,00 ₽", k: "inc", icon: "cash", n: "Аванс" },
    { t: "Метро, автобус", s: "Транспорт", acc: "cash", amt: "-140,00 ₽", k: "exp", icon: "bus" },
    { t: "Кофе", s: "Еда", acc: "credit", amt: "-320,00 ₽", k: "exp", icon: "coffee" },
    { day: "23 августа", total: "-610,00 ₽" },
    { t: "Кафе и рестораны", s: "Еда", acc: "eur", amt: "-48,00 €", k: "exp", icon: "tools-kitchen-2" },
    { t: "Аптека", s: "Покупки", acc: "cash", amt: "-610,00 ₽", k: "exp", icon: "pill" },
  ];
  const ACCOUNT_FEED = [
    { day: "25 августа", total: "-6 250,00 ₽" },
    { t: "Продукты", s: "Пятёрочка", acc: "main", amt: "-1 250,00 ₽", k: "exp", icon: "shopping-cart" },
    { t: "Перевод", acc: "main", to: "trip", amt: "-5 000,00 ₽", k: "trf", icon: "swap" },
    { day: "24 августа", total: "+89 680,00 ₽", pos: true },
    { t: "Зарплата", s: "Доход", acc: "main", amt: "+90 000,00 ₽", k: "inc", icon: "cash", n: "Аванс" },
    { t: "Кофе", s: "Еда", acc: "main", amt: "-320,00 ₽", k: "exp", icon: "coffee" },
    { day: "22 августа", total: "-1 935,00 ₽" },
    { t: "Аптека", s: "Покупки", acc: "main", amt: "-1 890,00 ₽", k: "exp", icon: "pill" },
    { t: "Разница", s: "Служебное", acc: "main", amt: "-45,00 ₽", k: "exp", icon: "adjustments", n: "Пересчёт наличных" },
  ];
  // Двенадцать счетов: цвет раздаётся сам по очереди палитры; когда она кончается, цвета идут по второму кругу
  const MANY = ["Наличные", "Сбер зарплатная", "Т-Банк", "Альфа кредитка", "ВТБ ипотека", "Озон карта",
    "Яндекс Пэй", "Карта жилья", "Газпромбанк", "Почта Банк", "Райффайзен", "МТС Деньги"].map((name, i) => ({
    name, type: i === 0 ? "cash" : "card", q: i,
    label: i === 0 ? "" : String(1000 + ((i * 2731) % 9000)).slice(-4), glyph: ["cash", "credit-card", "wallet", "percentage", "home", "shopping-bag", "device-mobile", "key", "building-bank", "gift", "coins", "trending-up"][i],
    bal: ["12 400,00 ₽", "71 920,50 ₽", "18 300,00 ₽", "-1 890,00 ₽", "4 100,00 ₽", "960,00 ₽",
      "2 450,00 ₽", "33 000,00 ₽", "7 800,00 ₽", "150,00 ₽", "1 064 210,00 ₽", "640,00 ₽"][i],
    neg: i === 3,
  }));
  const LONG = { name: "Кредитная карта для поездок за границу", type: "card", q: 5, label: "5520", glyph: "plane", bal: "-23 410,00 ₽", neg: true, cur: "RUB" };

  // ---------- Мелкие части разметки ----------
  const ic = (k, cls = "") => `<svg class="ic ${cls}"><use href="#i-${k}"/></svg>`;
  const hic = (k, cls = "") => `<svg class="ic ${cls}"><use href="#h-${k}"/></svg>`;
  const nav = on => `<nav class="nav">${[["balances", "Балансы"], ["feed", "Операции"], ["report", "Отчёт"], ["more", "Ещё"]]
    .map(([k, n]) => `<a${k === on ? ' class="on"' : ""}>${hic("tab-" + k)}<span>${n}</span></a>`).join("")}</nav>`;
  const back = `<svg class="ic lg act"><use href="#h-back"/></svg>`;
  const keypad = `<div class="kp"><div class="nums"><b>7</b><b>8</b><b>9</b><b>4</b><b>5</b><b>6</b><b>1</b><b>2</b><b>3</b><b>,</b><b>0</b><b>${ic("backspace")}</b></div><div class="ops"><b>÷</b><b>×</b><b>−</b><b>+</b><b>=</b></div></div>`;
  const kinds = on => `<div class="kinds">${[["exp", "arrow-down-left", "Расход"], ["inc", "arrow-up-right", "Доход"], ["trf", "swap", "Перевод"]]
    .map(([k, i, n]) => `<b class="${k}${k === on ? " on" : ""}">${ic(i, "sm")}${n}</b>`).join("")}</div>`;
  const amountClass = tx => tx.k === "exp" ? "neg" : tx.k === "inc" ? "pos" : "";
  const swatches = (selected, rings = false) => `<div class="swatches${rings ? " rings" : ""}"${PALETTE.length % 4 === 0 ? ' style="grid-template-columns:repeat(4,1fr)"' : ""}>${PALETTE
    .map(p => `<i class="ac-${p.key}${p.key === selected ? " on" : ""}">${p.key === selected ? ic("check") : ""}</i>`).join("")}</div>`;
  // Набор значков счёта: силуэты, различимые на 16 dp. Названия — из Texts/IconNames.resx
  // приложения (там уже есть все, кроме credit-card — для него запись новая), строчными для подписи
  const GLYPH_NAMES = { "credit-card": "карта", cash: "банкнота", "building-bank": "банк", wallet: "кошелёк", coins: "монеты", percentage: "процент", "device-mobile": "телефон", plane: "самолёт", beach: "пляж", home: "дом", gift: "подарок", shield: "щит", key: "ключ", car: "автомобиль", school: "академическая шапочка", "trending-up": "график роста", "heart-handshake": "рукопожатие с сердцем", users: "люди" };
  const ACCOUNT_GLYPHS = Object.keys(GLYPH_NAMES);
  const glyphPicker = selected => `<div class="icopick">${ACCOUNT_GLYPHS
    .map(g => `<span${g === selected ? ' class="on"' : ""}>${ic(g)}</span>`).join("")}</div>`;

  // Облик по умолчанию: как сейчас. Документ переопределяет нужные части
  const BASE = {
    n: "0",
    mark: (a, size = "") => ic(typeGlyph(a)),
    inline: (a, transfer) => a.name,
    lead: (tx, ctx) => ic(tx.icon),
    right: (tx, ctx) => `<span class="num ${amountClass(tx)}">${tx.amt}</span>`,
    cardTop: a => `<div class="preview">${L().mark(a, "lg")}<div class="g"><div class="t">${a.name}</div><div class="s">${a.type === "cash" ? "Наличные" : "Карта"} · ${a.cur}</div></div></div><div class="dv"></div>`,
    editor: a => "",
    caps: {},
  };
  let look = BASE;
  const L = () => look;

  function sub(tx, ctx) {
    if (ctx.inAccount) return tx.k === "trf" ? "Перевод" : tx.s;
    if (tx.k === "trf") return `${L().inline(A[tx.acc], true)} → ${L().inline(A[tx.to], true)}`;
    return [tx.s, L().inline(A[tx.acc], false)].filter(Boolean).join(" · ");
  }
  function row(tx, ctx = {}) {
    if (tx.day) return `<div class="date"><span>${tx.day}</span><span class="${tx.pos ? "pos" : ""}">${tx.total}</span></div>`;
    const sel = ctx.selected?.includes(tx.t);
    const head = ctx.inAccount && tx.k === "trf" ? A[tx.to].name : tx.t;
    const lead = sel ? ic("circle-check") : L().lead(tx, ctx);
    return `<div class="row fr${sel ? " sel" : ""}">${lead}<div class="g"><div><div class="t">${head}</div><div class="s">${sub(tx, ctx)}</div>${tx.n ? `<div class="n">${tx.n}</div>` : ""}</div>${L().right(tx, ctx)}</div></div>`;
  }
  const accRow = (a, extra = "") => `<div class="row">${L().mark(a)}<div class="g"><div class="t${a.hidden ? " dim" : ""}">${a.name}</div>${extra}</div><span class="num${a.neg ? " neg" : ""}">${a.bal}</span></div>`;
  const field = (k, a) => `<div class="fld">${L().mark(a)}<span><span class="k">${k}</span><span class="v">${a.name}</span></span></div>`;

  // ---------- Экраны ----------
  const SCREENS = {
    balances: { title: "Балансы", html: () => `
      <div class="hd"><h2>Балансы</h2></div>
      <div class="body">
        <div class="lbl"><span>Рубли</span><span>Доступно</span></div>
        <div class="total">82 430,50 ₽</div>
        <div class="card">${accRow(A.cash)}${accRow(A.main)}${accRow(A.credit)}</div>
        <div class="lbl"><span>Накопления</span></div><div class="note" style="margin-top:-4px">Скрыты</div>
        <div class="card">${accRow(A.trip)}</div>
        <div class="lbl" style="margin-top:4px"><span>Евро</span><span>Доступно</span></div>
        <div class="total">1 240,00 €</div>
        <div class="card">${accRow(A.eur)}</div>
        <div class="lbl" style="margin-top:4px"><span>Доллары</span></div>
        <div class="lbl"><span>Накопления</span></div><div class="note" style="margin-top:-4px">Скрыты</div>
        <div class="card">${accRow(A.usd)}</div>
      </div>
      <div class="fab ext">+&nbsp;&nbsp;Операция</div>${nav("balances")}` },
    feed: { title: "Общая лента", html: () => `
      <div class="hd"><h2>Операции</h2></div>${FEED.map(t => row(t)).join("")}
      <div class="fab">+</div>${nav("feed")}` },
    account: { title: "Лента счёта", html: () => `
      <div class="hd">${back}<span style="display:flex;align-items:center;gap:10px;flex:1;min-width:0;margin-left:-8px">${L().mark(A.main, "lg")}<h2>Карта основная</h2></span>${ic("pencil")}</div>
      <div class="balrow"><span>Баланс</span><b>71 920,50 ₽</b></div>${ACCOUNT_FEED.map(t => row(t, { inAccount: true })).join("")}
      <div class="fab">+</div>${nav("balances")}` },
    form: { title: "Форма операции", html: () => `
      <div class="hd">${back}<h2>Новая операция</h2><b class="save">Сохранить</b></div>
      <div class="body">
        <div class="card">${kinds("exp")}<div class="amt"><span class="expr"></span><span class="v exp">-1 590 ₽</span></div>
          <div class="cells">${field("Счёт", A.credit)}<div class="fld req"><span class="ico">${ic("tag")}</span><span><span class="k">Категория</span><span class="v ask">Выбрать</span></span></div></div></div>
        <div class="card">
          <div class="row">${ic("calendar")}<span class="t" style="flex:1">Сегодня</span><div class="chips"><b class="chip on">Сегодня</b><b class="chip">Вчера</b><b class="chip">…</b></div></div>
          <div class="row">${ic("map-pin")}<span class="k">Место</span><span class="v none">Необязательно</span>${ic("chevron-right", "mut")}</div>
          <div class="row">${ic("pencil")}<span class="entry ph">Заметка — необязательно</span></div>
        </div>
      </div>${keypad}` },
    transfer: { title: "Перевод", html: () => `
      <div class="hd">${back}<h2>Новая операция</h2><b class="save">Сохранить</b></div>
      <div class="body">
        <div class="card">${kinds("trf")}<div class="amt"><span class="expr"></span><span class="v">5 000 ₽</span></div>
          <div class="cells">${field("Откуда", A.main)}${field("Куда", A.trip)}</div></div>
        <div class="card">
          <div class="row">${ic("calendar")}<span class="t" style="flex:1">Сегодня</span><div class="chips"><b class="chip on">Сегодня</b><b class="chip">Вчера</b><b class="chip">…</b></div></div>
          <div class="row">${ic("pencil")}<span class="entry ph">Заметка — необязательно</span></div>
        </div>
      </div>${keypad}` },
    picker: { title: "Выбор счёта", html: () => `
      <div class="hd">${back}<h2>Счёт</h2></div>
      <div class="list">
        <div class="row">${ic("plus", "acc")}<div class="g"><div class="t accent">Новый счёт</div></div></div>
        <div class="lbl" style="padding:10px 11px 6px;margin:0"><span>Рубли</span></div>
        ${accRow(A.cash)}
        <div class="row sel">${L().mark(A.main)}<div class="g"><div class="t">${A.main.name}</div></div><span class="num">${A.main.bal}</span>${ic("check", "acc")}</div>
        ${accRow(A.credit)}${accRow(A.trip, '<div class="s">Накопления</div>')}
        <div class="lbl" style="padding:10px 11px 6px;margin:0"><span>Евро</span></div>${accRow(A.eur)}
      </div>` },
    catalog: { title: "Справочник счетов", html: () => {
      const r = (a, lock) => `<div class="row">${ic(lock ? "lock" : "grip-vertical", "mut")}${L().mark(a)}<div class="g"><div class="t">${a.name}</div><div class="s">${a.type === "cash" ? "Наличные" : "Карта"} · ${a.cur}</div></div><span class="num${a.neg ? " neg" : ""}">${a.bal}</span></div>`;
      return `
      <div class="hd">${back}<h2>Счета</h2><span class="tool">+</span></div>
      <div class="body">
        <div class="lbl"><span>Доступно к тратам</span></div><div class="card">${r(A.cash)}${r(A.main)}${r(A.credit)}${r(A.eur)}</div>
        <div class="lbl"><span>Накопления</span></div><div class="card">${r(A.trip)}${r(A.usd)}</div>
        <div class="lbl"><span>Заблокированные</span></div><div class="card">${r(A.old, true)}</div>
      </div>${nav("more")}`; } },
    card: { title: "Карточка счёта", html: () => `
      <div class="hd">${back}<h2>Счёт</h2><b class="del">Удалить</b><b class="save">Сохранить</b></div>
      <div class="body">
        <div class="card form">${L().cardTop(A.credit)}
          <div class="fld"><span class="k">Название</span><span class="v">${A.credit.name}</span></div>
          ${L().editor(A.credit)}
          <div class="fld"><span class="k">Тип</span><span class="v">Карта</span></div>
          <div class="fld"><span class="k">Валюта</span><span class="v lock">Рубль ₽</span></div>
          <div class="fld"><span class="k">Начальный остаток</span><span class="v neg">-15 000,00 ₽</span></div>
        </div>
        <div class="card form"><div class="swrow"><span>Скрытый</span><span class="sw2"><i></i></span></div><div class="note">Счёт не входит в итоги и отчёт</div></div>
      </div>` },
    sign: { title: "Цвет и значок", html: () => `
      <div class="hd">${back}<h2>Цвет и значок</h2></div>
      <div class="body">${L().sign(A.credit)}</div>` },
    select: { title: "Выделение в ленте", html: () => `
      <div class="hd">${ic("x", "lg")}<h2>Выбрано: 2</h2><b class="del">Удалить</b></div>
      ${FEED.slice(0, 8).map(t => row(t, { selected: ["Продукты", "Кофе"] })).join("")}${nav("feed")}` },
    many: { title: "Двенадцать счетов", short: "mid", html: () => `
      <div class="hd"><h2>Балансы</h2></div>
      <div class="body"><div class="lbl"><span>Рубли</span><span>Доступно</span></div><div class="total">1 214 040,50 ₽</div>
      <div class="card">${MANY.map(a => accRow(a)).join("")}</div></div>` },
    long: { title: "Длинные названия", short: "short", html: () => `
      <div class="hd"><h2>Операции</h2></div>
      <div class="date"><span>25 августа</span><span>-4 980,00 ₽</span></div>
      ${(() => { A.long = LONG; const rows = [
        { t: "Отели и жильё", s: "Booking", acc: "long", amt: "-4 200,00 ₽", k: "exp", icon: "home" },
        { t: "Перевод", acc: "long", to: "main", amt: "-780,00 ₽", k: "trf", icon: "swap" },
        { t: "Кафе и рестораны", s: "Ресторан у самого моря", acc: "long", amt: "-780,00 ₽", k: "exp", icon: "tools-kitchen-2" }];
        return rows.map(t => row(t)).join(""); })()}
      <div class="body" style="padding-top:10px"><div class="card">${accRow(LONG)}</div></div>` },
    deutan: { title: "Дейтеранопия", short: "short", sim: "sim-deutan", html: () => `<div class="hd"><h2>Операции</h2></div>${FEED.slice(0, 8).map(t => row(t)).join("")}` },
    protan: { title: "Протанопия", short: "short", sim: "sim-protan", html: () => `<div class="hd"><h2>Операции</h2></div>${FEED.slice(0, 8).map(t => row(t)).join("")}` },
    gray: { title: "Без цвета", short: "short", sim: "sim-gray", html: () => `<div class="hd"><h2>Операции</h2></div>${FEED.slice(0, 8).map(t => row(t)).join("")}` },
    wallet: { title: "Балансы плитками", html: () => {
      const t = a => `<div class="bt ac-${a.color}"><span class="mc lg">${a.label || ic(typeGlyph(a))}</span><span class="t">${a.name}</span><span class="num${a.neg ? " neg" : ""}">${a.bal}</span></div>`;
      return `
      <div class="hd"><h2>Балансы</h2></div>
      <div class="body">
        <div class="lbl"><span>Рубли</span><span>Доступно</span></div><div class="total">82 430,50 ₽</div>
        <div class="bgrid">${t(A.cash)}${t(A.main)}${t(A.credit)}<div class="bt add">+&nbsp;Счёт</div></div>
        <div class="lbl"><span>Накопления</span></div><div class="note" style="margin-top:-4px">Скрыты</div>
        <div class="bgrid">${t(A.trip)}</div>
        <div class="lbl" style="margin-top:4px"><span>Евро</span><span>Доступно</span></div><div class="total">1 240,00 €</div>
        <div class="bgrid">${t(A.eur)}</div>
      </div>
      <div class="fab ext">+&nbsp;&nbsp;Операция</div>${nav("balances")}`; } },
  };

  // ---------- Сборка документа ----------
  function figure(id, number, theme, withCaption) {
    const s = SCREENS[id];
    const cap = L().caps[id];
    const sid = `${L().n}.${number}`;
    return `<figure class="unit"><figcaption class="cap"><span class="sid">${sid}</span><b>${s.title}</b>${withCaption && cap ? `<em>${cap}</em>` : ""}</figcaption>
      <div class="phone${s.short ? " " + s.short : ""}${s.sim ? " " + s.sim : ""}" data-theme="${theme}">${s.html()}</div></figure>`;
  }

  // Таблица сплошной палитры: заливка к карточке (без порога — счёт всегда подписан названием)
  // и значок на заливке — не ниже 3, это знак
  function solidTable() {
    const num = v => v.toFixed(2).replace(".", ",");
    const cell = (v, floor) => `<td class="${v >= floor ? "ok" : "bad"}">${num(v)}</td>`;
    const ok = (h, theme) => { const { L, C } = oklch(h); const band = theme === "light" ? L >= 0.43 && L <= 0.77 : L >= 0.48 && L <= 0.67; return `<td class="${band && C >= 0.1 ? "ok" : "bad"}">L ${num(L)} · C ${C.toFixed(3).replace(".", ",")}</td>`; };
    const rows = PALETTE.map(p => `<tr><td><span class="chipsw"><i style="background:${p.light}"></i><i style="background:${p.dark}"></i>${p.name}</span></td>
      <td class="n">${token(p.key)}Light ${p.light}<br>${token(p.key)}Dark ${p.dark}</td>
      <td class="n">${p.on === "light" ? "AccountGlyphLight" : "AccountGlyphDark"}</td>
      ${ok(p.light, "light")}<td>${num(ratio(p.light, THEME.light.card))}</td>${cell(ratio(p.light, GLYPH[p.on]), 3)}
      ${ok(p.dark, "dark")}<td>${num(ratio(p.dark, THEME.dark.card))}</td>${cell(ratio(p.dark, GLYPH[p.on]), 3)}</tr>`).join("");
    return `<table class="tokens"><thead><tr><th>Цвет</th><th>Токен заливки</th><th>Значок</th>
      <th>OKLCH светлой</th><th>к карточке</th><th>значок ≥ 3</th><th>OKLCH тёмной</th><th>к карточке</th><th>значок ≥ 3</th></tr></thead><tbody>${rows}</tbody></table>`;
  }
  const oklch = h => { const [l, a, b] = oklab(rgb(h).map(lin)); return { L: l, C: Math.hypot(a, b) }; };

  function paletteTable() {
    if (PALETTE[0].on) return solidTable();
    const cell = (v, floor) => `<td class="${v >= floor ? "ok" : "bad"}">${v.toFixed(2).replace(".", ",")}</td>`;
    const rows = PALETTE.map(p => {
      const lt = tintOf(p.light, "light"), dt = tintOf(p.dark, "dark");
      return `<tr><td><span class="chipsw"><i style="background:${p.light}"></i><i style="background:${p.dark}"></i>${p.name}</span></td>
        <td class="n">${token(p.key)}Light ${p.light}<br>${token(p.key)}Dark ${p.dark}</td>
        <td class="n">${token(p.key)}BackgroundLight ${lt}<br>${token(p.key)}BackgroundDark ${dt}</td>
        ${cell(ratio(p.light, THEME.light.card), 4.5)}${cell(ratio(p.light, THEME.light.paper), 3)}${cell(ratio(p.light, lt), 3)}
        ${cell(ratio(p.dark, THEME.dark.card), 4.5)}${cell(ratio(p.dark, THEME.dark.paper), 3)}${cell(ratio(p.dark, dt), 3)}</tr>`;
    }).join("");
    return `<table class="tokens"><thead><tr><th>Цвет</th><th>Токен цвета</th><th>Токен подложки</th>
      <th>к карточке ≥ 4,5</th><th>к бумаге ≥ 3</th><th>на подложке ≥ 3</th><th>тёмн.: к карточке</th><th>к бумаге</th><th>на подложке</th></tr></thead><tbody>${rows}</tbody></table>`;
  }

  // Облик варианта 5 «Цвет и значок»: плашка с выбранным значком в списках, жетон в ленте,
  // строка «Цвет и значок» в карточке и свой экран выбора. solid — сплошная заливка яркой палитры
  // shape — форма бейджа: circle, square (скруглённый квадрат), plate (карта) или glow (круг с объёмом)
  function signLook({ solid = false, shape = "circle" } = {}) {
    const toneName = c => title(PALETTE.find(p => p.key === c).name);
    const tile = (a, size = "") => `<span class="tile ${solid ? "solid " : ""}${shape} ${size} ac-${a.color}${a.closed ? " off" : ""}">${ic(a.glyph)}</span>`;
    const tokenOf = a => `<span class="token ${shape} ac-${a.color}">${ic(a.glyph)}</span>`;
    return {
      mark: tile,
      inline: a => `${tokenOf(a)}${a.name}`,
      editor: a => `<div class="fld"><span class="k">Цвет и значок</span><div class="signrow">${tile(a)}<span class="v">${toneName(a.color)} · ${GLYPH_NAMES[a.glyph]}</span>${ic("chevron-right", "mut")}</div></div>`,
      sign: a => `
        <div class="signprev">
          <div class="card">${accRow(a)}</div>
          <div class="card">${row(FEED[3])}</div>
        </div>
        <div class="lbl sp"><span>Цвет</span></div>
        <div class="card form">${swatches(a.color).replace(`class="swatches`, `class="swatches ${shape}`)}</div>
        <div class="lbl sp"><span>Значок</span></div>
        <div class="card form">
          <div class="suggest">Подходит: <b>${ic("percentage")}процент</b></div>
          <div class="signgrid${solid ? " solid" : ""} ${shape} ac-${a.color}">${ACCOUNT_GLYPHS.map(g => `<span${g === a.glyph ? ' class="on"' : ""}>${ic(g)}</span>`).join("")}</div>
        </div>`,
    };
  }

  // mode — deutan, protan или normal; metric oklab — по методу dataviz, иначе прежний CIE Lab
  function confusable(mode, threshold, metric) {
    const out = [];
    const d2 = (a, b) => metric === "oklab" ? okE(a, b, SIM[mode]) : dE(simulate(a, SIM[mode]), simulate(b, SIM[mode]));
    for (let i = 0; i < PALETTE.length; i++)
      for (let j = i + 1; j < PALETTE.length; j++) {
        const a = PALETTE[i], b = PALETTE[j];
        const d = Math.min(d2(a.light, b.light), d2(a.dark, b.dark));
        if (d < threshold) out.push([a, b, d]);
      }
    return out.sort((x, y) => x[2] - y[2]);
  }

  // root — часть страницы: документ с несколькими обликами рендерит каждый раздел отдельно
  function render(custom, root = document) {
    look = { ...BASE, ...custom };
    if (custom.palette) PALETTE = custom.palette;
    // Цвет счёта — по его месту в очереди палитры документа
    const keyAt = q => PALETTE[q % PALETTE.length].key;
    for (const a of [...Object.values(A), ...MANY, LONG]) a.color = keyAt(a.q);
    injectPalette();
    // Номера сквозные по документу; тёмный ряд повторяет номера светлого над ним
    let next = 1, lastStart = 1;
    root.querySelectorAll("[data-screens]").forEach(el => {
      const theme = el.dataset.theme || "light";
      // Тема — у каждого телефона; на ряду она перекрасила бы подписи под цвет страницы
      el.removeAttribute("data-theme");
      el.classList.add("screens");
      if (theme === "dark") el.classList.add("dark-row");
      const ids = el.dataset.screens.split(/\s+/);
      const start = theme === "dark" ? lastStart : next;
      if (theme !== "dark") { lastStart = next; next += ids.length; }
      el.innerHTML = ids.map((id, i) => figure(id, start + i, theme, theme !== "dark" || el.hasAttribute("data-captions"))).join("");
    });
    root.querySelectorAll("[data-palette]").forEach(el => el.innerHTML = paletteTable());
    root.querySelectorAll("[data-order]").forEach(el => el.innerHTML = `<ol class="order">${PALETTE.map(p => `<li><i class="ac-${p.key}" style="background:var(--c)"></i>${p.name}</li>`).join("")}</ol>`);
    root.querySelectorAll("[data-pairs]").forEach(el => {
      const pairs = confusable(el.dataset.pairs, Number(el.dataset.threshold || 12), el.dataset.metric);
      if (el.hasAttribute("data-values")) { el.innerHTML = `<div class="pairs">${pairs.map(([a, b, d]) => `<span><i style="background:${a.light}"></i>${a.name} — <i style="background:${b.light}"></i>${b.name} · ${d.toFixed(1).replace(".", ",")}</span>`).join("") || "<span>нет</span>"}</div>`; return; }
      el.innerHTML = `<div class="pairs">${pairs.map(([a, b]) => `<span><i style="background:${a.light}"></i>${a.name} — <i style="background:${b.light}"></i>${b.name}</span>`).join("") || "<span>нет</span>"}</div>`;
    });
    root.querySelectorAll("[data-fragment]").forEach(el => {
      const f = el.dataset.fragment;
      if (f === "accounts") el.innerHTML = `<div class="card">${accRow(A.cash)}${accRow(A.main)}${accRow(A.credit)}</div>`;
      if (f === "feed") el.innerHTML = `<div class="card">${FEED.slice(1, 4).map(t => row(t)).join("")}</div>`;
      el.style.fontFamily = "var(--app)";
      el.style.fontSize = "13px";
    });
  }

  window.KIT = { render, signLook, A, VIVID, get PALETTE() { return PALETTE; }, ACCOUNT_GLYPHS, GLYPH_NAMES, FEED, ic, typeGlyph, swatches, glyphPicker, row, accRow, field };
})();
