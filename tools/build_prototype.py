#!/usr/bin/env python3
"""Сборка docs/ui/prototype.html из docs/ui/mockups.html.

Прототип не рисуется руками: каждый экран берётся из макетов как есть, а сценарии
описаны данными ниже. Поэтому макеты и прототип не могут разойтись — правится
только mockups.html, затем запускается этот скрипт.

Запуск:   python tools/build_prototype.py          # пересобрать
Проверка: python tools/build_prototype.py --check  # упасть, если prototype.html устарел
"""
import json, pathlib, re, sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SRC = ROOT / "docs/ui/mockups.html"
DST = ROOT / "docs/ui/prototype.html"

# Экраны, которые выезжают снизу как лист, а не сдвигаются сбоку
SHEETS = ["B-01", "B-05", "B-06", "C-01", "C-07", "D-02", "D-04"]

# Сценарии. Шаг: s — экран, c — подпись, t — цель касания (селектор или text=…),
# k — клавиша цифровой клавиатуры, set — подставить текст, css — подставить стиль,
# theme — переключить тему, w — пауза в мс.
SCENARIOS = [
 {"n": "Завести счёт", "uc": "UC-13", "steps": [
  {"s": "C-06", "c": "Первый запуск: категории засеяны, счетов ещё нет", "w": 1600},
  {"s": "C-06", "c": "Касание «Создать счёт»", "t": ".btn"},
  {"s": "D-02", "c": "Тип и валюта заполнены умолчаниями — вводятся только название и остаток", "w": 1800,
   "set": {"text=Название": "", ".hd h2": "Новый счёт"}, "css": {"text=Валюту нельзя изменить — по счёту есть операции": "display:none", "text=Назад — свободно": "display:none"}},
  {"s": "D-02", "c": "Ввод названия", "t": "text=Название", "set": {"text=Название": "Карта осн"}, "w": 500},
  {"s": "D-02", "c": "Ввод названия", "set": {"text=Название": "Карта основная"}, "w": 700},
  {"s": "D-02", "c": "Начальный остаток — поле счёта, а не операция в ленте", "t": "text=Начальный остаток", "w": 1400},
  {"s": "D-02", "c": "Сохранение", "t": ".hd .act.on", "w": 700},
  {"s": "A-01", "c": "Баланс равен начальному остатку — операций ещё нет", "w": 2600},
 ]},
 {"n": "Записать расход", "uc": "UC-01", "steps": [
  {"s": "A-01", "c": "Главный экран. Доступно к тратам — 82 430,50 ₽", "w": 1300},
  {"s": "A-01", "c": "Касание кнопки добавления", "t": ".fab"},
  {"s": "B-01", "c": "Форма открывается сразу с клавиатурой: вид «расход» и последний счёт подставлены", "w": 1600,
   "set": {".amt .v": "0", ".amt .expr": ""}},
  {"s": "B-01", "c": "Ввод суммы", "k": "1", "set": {".amt .v": "1"}, "w": 260},
  {"s": "B-01", "c": "Ввод суммы", "k": "2", "set": {".amt .v": "12"}, "w": 260},
  {"s": "B-01", "c": "Ввод суммы", "k": "5", "set": {".amt .v": "125"}, "w": 260},
  {"s": "B-01", "c": "Ввод суммы", "k": "0", "set": {".amt .v": "1 250"}, "w": 700},
  {"s": "B-01", "c": "Касание поля «Категория»", "t": "text=Категория"},
  {"s": "B-03", "c": "Плоский список подкатегорий под заголовками групп: один выбор вместо двух", "w": 1600},
  {"s": "B-03", "c": "Касание «Продукты»", "t": "text=Продукты"},
  {"s": "B-01", "c": "Категория выбрана, место необязательно — можно сохранять", "w": 1300, "set": {"text=Категория": "Продукты"}},
  {"s": "B-01", "c": "Сохранение с клавиатуры", "t": ".kp b.go"},
  {"s": "A-01", "c": "Расход записан: четыре касания сверх набора суммы", "w": 2800},
 ]},
 {"n": "Отложить в накопления", "uc": "UC-05", "steps": [
  {"s": "A-01", "c": "Накопления показаны группой без общей суммы: они скрыты из расчётов", "w": 1500},
  {"s": "A-01", "c": "Касание кнопки добавления", "t": ".fab"},
  {"s": "B-01", "c": "Переключение вида на «Перевод»", "t": "text=Перевод", "w": 900},
  {"s": "B-05", "c": "Категория и место исчезают, появляются «Откуда» и «Куда»", "w": 1700},
  {"s": "B-05", "c": "Касание «Куда»", "t": "text=Куда"},
  {"s": "B-02", "c": "Выбор счёта назначения", "t": "text=Копилка на отпуск", "w": 900},
  {"s": "B-05", "c": "Валюты совпадают — сумма одна", "w": 1400},
  {"s": "B-05", "c": "Сохранение", "t": ".hd .act.on"},
  {"s": "A-01", "c": "Доступно к тратам уменьшилось, накопления выросли. Общая сумма денег не изменилась", "w": 2800},
 ]},
 {"n": "Перевести между валютами", "uc": "UC-10", "steps": [
  {"s": "B-05", "c": "Перевод: счёт назначения ещё не выбран", "w": 1200},
  {"s": "B-05", "c": "Касание «Куда»", "t": "text=Куда"},
  {"s": "B-02", "c": "Выбор счёта в другой валюте", "t": "text=Карта евро", "w": 900},
  {"s": "B-06", "c": "Появилась вторая сумма — зачисление в евро. Курс не запрашивается, только две суммы", "w": 2200},
  {"s": "B-06", "c": "Сохранение", "t": ".hd .act.on"},
  {"s": "A-01", "c": "Оба баланса изменились, каждый в своей валюте", "w": 2400},
 ]},
 {"n": "Исправить операцию", "uc": "UC-07", "steps": [
  {"s": "A-03", "c": "Лента счёта. Ошибка: кофе записан как продукты", "w": 1500},
  {"s": "A-03", "c": "Касание строки", "t": "text=Продукты"},
  {"s": "C-07", "c": "Карточка операции — та же форма, что при вводе", "w": 1500, "css": {".veil": "display:none"}},
  {"s": "C-07", "c": "Касание поля «Категория»", "t": "text=Категория"},
  {"s": "B-03", "c": "Выбор правильной подкатегории", "t": "text=Кофе", "w": 900},
  {"s": "C-07", "c": "Категория заменена. Баланс не меняется — сумма та же", "w": 1500, "set": {"text=Категория": "Кофе"}, "css": {".veil": "display:none"}},
  {"s": "C-07", "c": "Сохранение", "t": ".hd .act", "css": {".veil": "display:none"}},
  {"s": "A-03", "c": "Строка в ленте обновилась", "w": 2200},
 ]},
 {"n": "Дата вне периода счёта", "uc": "UC-01", "steps": [
  {"s": "B-01", "c": "Ввод расхода", "w": 1000, "set": {".amt .v": "1 250", ".amt .expr": ""}},
  {"s": "B-01", "c": "Касание поля даты", "t": "text=Дата"},
  {"s": "B-04", "c": "Календарь. Будущие даты погашены", "w": 1500},
  {"s": "B-04", "c": "Выбор даты раньше открытия счёта", "t": ".cal .gr i:nth-child(1)", "w": 800},
  {"s": "C-01", "c": "Ошибка объясняет причину и даёт два выхода: сдвинуть дату открытия или выбрать другой счёт", "w": 3200},
 ]},
 {"n": "Удалить операцию", "uc": "UC-08", "steps": [
  {"s": "A-03", "c": "Лента счёта", "w": 1100},
  {"s": "A-03", "c": "Касание строки", "t": "text=Продукты"},
  {"s": "C-07", "c": "Карточка операции", "w": 1200, "css": {".veil": "display:none"}},
  {"s": "C-07", "c": "Касание удаления", "t": ".hd .act:last-child", "css": {".veil": "display:none"}},
  {"s": "C-07", "c": "Подтверждение называет, каким станет баланс. Отмены и корзины нет — это единственная защита", "w": 2600, "css": {".veil": ""}},
  {"s": "C-07", "c": "Касание «Удалить»", "t": ".dlg .yes"},
  {"s": "A-03", "c": "Строка исчезла, баланс пересчитан", "w": 2200},
 ]},
 {"n": "Удалить подкатегорию", "uc": "UC-12", "steps": [
  {"s": "A-04", "c": "Раздел «Ещё»: справочники и настройки", "w": 1200},
  {"s": "A-04", "c": "Касание «Категории»", "t": "text=Категории"},
  {"s": "D-03", "c": "Дерево из двух уровней. «Прочее» неудаляемо", "w": 1600},
  {"s": "D-03", "c": "Касание «Такси»", "t": "text=Такси"},
  {"s": "C-02", "c": "Карточка подкатегории", "w": 1200, "css": {".veil": "display:none"}},
  {"s": "C-02", "c": "Касание «Удалить подкатегорию»", "t": "text=Удалить подкатегорию", "css": {".veil": "display:none"}},
  {"s": "C-02", "c": "Диалог называет число операций и куда они переедут", "w": 2600, "css": {".veil": ""}},
  {"s": "C-02", "c": "Касание «Удалить»", "t": ".dlg .yes"},
  {"s": "D-03", "c": "Операции перенесены в «Прочее» той же группы. Балансы не изменились", "w": 2600},
 ]},
 {"n": "Куда ушли деньги", "uc": "UC-19", "steps": [
  {"s": "A-01", "c": "Главный экран", "w": 1000},
  {"s": "A-01", "c": "Касание вкладки «Отчёт»", "t": ".nav a:nth-child(3)"},
  {"s": "E-01", "c": "Расходы за месяц по группам. Доли и полосы — для сравнения глазом", "w": 2200},
  {"s": "E-01", "c": "Касание «Еда»", "t": "text=Еда"},
  {"s": "E-02", "c": "Подкатегории группы. Доли пересчитаны внутри группы", "w": 2200},
  {"s": "E-02", "c": "Касание «Доставка еды»", "t": "text=Доставка еды"},
  {"s": "E-03", "c": "Сами операции — те же строки, что в ленте, другой разрез", "w": 2400},
  {"s": "E-02", "c": "Назад", "t": ".hd .act", "w": 700},
  {"s": "E-01", "c": "Назад", "t": ".hd .act", "w": 700},
  {"s": "E-01", "c": "Переключение месяца назад", "t": ".cal .mo .act"},
  {"s": "E-04", "c": "Месяц без трат: пустое состояние, а не список нулей", "w": 2600},
 ]},
 {"n": "Оформление", "uc": "FR-SET-03", "steps": [
  {"s": "A-04", "c": "Раздел «Ещё»", "w": 1000},
  {"s": "A-04", "c": "Касание «Оформление»", "t": "text=Оформление"},
  {"s": "D-06", "c": "Три состояния: как в системе, светлая, тёмная", "w": 1600},
  {"s": "D-06", "c": "Касание «Тёмная»", "t": "text=Тёмная", "theme": "dark", "w": 1800},
  {"s": "D-06", "c": "Все экраны следуют теме: цвета заданы токенами, прямых значений нет", "w": 1800},
  {"s": "A-04", "c": "Назад", "t": ".hd .act", "w": 1400},
  {"s": "A-04", "c": "Возврат к системной теме", "theme": "", "w": 1400},
 ]},
]

TEMPLATE = r"""<!doctype html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Личный трекер финансов — прототип</title>
<!-- СГЕНЕРИРОВАНО из mockups.html скриптом tools/build_prototype.py. Руками не править. -->
<style>
__MOCKUP_CSS__

/* --- каркас прототипа ------------------------------------------------------ */
:root{--z:0.6}
body{padding:0}
.page{max-width:1080px;margin:0 auto;padding:34px 20px 60px}
.page h1{font-size:23px;font-weight:500;margin:0 0 5px}
.lede{color:var(--ink2);font-size:13.5px;margin:0 0 22px;max-width:70ch}
.lede b{font-weight:500;color:var(--ink);font-family:var(--mono);font-size:13px}
.bar{display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-bottom:14px}
.tab,.ctl{border:1px solid var(--line2);background:var(--card);border-radius:8px;padding:7px 13px;font-size:13.5px;cursor:pointer;font-family:inherit;color:var(--ink)}
.tab.on{background:var(--acc);color:var(--onacc);border-color:var(--acc)}
.tab small{font-family:var(--mono);font-size:10.5px;opacity:.7;margin-left:6px}
.ctl.wide{min-width:96px}
.sep{flex:1}
.capt{background:var(--card);border:1px solid var(--line2);border-radius:10px;padding:11px 15px;font-size:14px;min-height:44px;display:flex;align-items:center;gap:11px;margin-bottom:16px}
.capt .step{font-family:var(--mono);font-size:11.5px;color:var(--acc);background:var(--accbg);padding:2px 7px;border-radius:4px;flex:none}
.track{height:3px;background:var(--line2);border-radius:2px;margin:0 0 16px;overflow:hidden}
.track i{display:block;height:100%;background:var(--acc);width:0;transition:width .25s}
.meter{background:var(--card);border:1px solid var(--line2);border-radius:8px;padding:6px 12px;font-size:13px;color:var(--ink2);font-family:var(--mono)}
.meter b{color:var(--acc);font-weight:500}
.holder{width:480px;margin:0 auto;height:calc(1040px * var(--z));transition:height .2s}
.frame{width:480px;height:1040px;transform:scale(var(--z));transform-origin:top center;transition:transform .2s;background:var(--frame);border-radius:46px;padding:9px;position:relative;cursor:pointer}
.dev{position:relative;width:100%;height:100%;background:var(--card);border-radius:38px;overflow:hidden;display:flex;flex-direction:column}
.dev.nofx *{transition:none!important;animation:none!important}
.sb{height:36px;display:flex;align-items:center;justify-content:space-between;padding:0 26px;font-size:13.5px;font-family:var(--mono);color:var(--ink);flex:none}
.sb .r{display:flex;gap:6px;align-items:center}
.sb .dot{width:7px;height:7px;border-radius:4px;background:var(--ink)}
.stage{position:relative;flex:1;overflow:hidden}
.scr{position:absolute;inset:0;background:var(--card);transition:transform .34s cubic-bezier(.3,.7,.3,1);will-change:transform;zoom:1.4545}
.scr.right{transform:translateX(100%)}
.scr.down{transform:translateY(100%)}
.scr .phone{width:330px;max-width:none;height:690px;border:none;border-radius:0;display:flex;flex-direction:column}
.scr .pad{display:none}
.scr .nav{margin-top:auto}
.scr .kp{margin-top:auto}
.scr .veil{z-index:2}
.rip{position:absolute;width:64px;height:64px;margin:-32px 0 0 -32px;border-radius:32px;background:var(--ripple);pointer-events:none;opacity:0;z-index:5}
.rip.go{animation:rp .55s ease-out}
@keyframes rp{0%{opacity:1;transform:scale(.3)}70%{opacity:.5}100%{opacity:0;transform:scale(1.15)}}
.hit{background:var(--acc-press)!important}
.legend{margin-top:22px;font-size:13px;color:var(--ink2);line-height:1.7;max-width:80ch}
.legend code{font-family:var(--mono);font-size:12.5px;background:var(--card);border:1px solid var(--line);padding:1px 5px;border-radius:4px}
</style>
</head>
<body>
__SPRITE__

<div class="page">
<h1>Анимированный прототип — итерация 3</h1>
<p class="lede">Экраны взяты из <b>mockups.html</b> без изменений, файл собирается скриптом <b>tools/build_prototype.py</b> — править нужно макеты, а не этот файл. Кадр — Samsung Galaxy S25 Ultra: <b>480 × 1040</b> логических пикселей.</p>

<div class="bar" id="tabs"></div>
<div class="bar">
  <button class="ctl wide" id="pp">Пауза</button>
  <button class="ctl" id="bk">◂ шаг</button>
  <button class="ctl" id="fw">шаг ▸</button>
  <button class="ctl" id="rs">Сначала</button>
  <button class="ctl" id="sp">1×</button>
  <button class="ctl" id="zm">60%</button>
  <button class="ctl" id="th">Тема</button>
  <span class="sep"></span>
  <span class="meter">касаний по интерфейсу: <b id="m-ui">0</b> · цифр: <b id="m-key">0</b></span>
</div>

<div class="capt"><span class="step" id="stepno">1 / 1</span><span id="captxt">Загрузка…</span></div>
<div class="track"><i id="trackbar"></i></div>

<div class="holder"><div class="frame" id="frame"><div class="dev" id="dev">
<div class="sb"><span>21:40</span><div class="r"><span class="dot"></span><span class="dot"></span><span class="dot"></span></div></div>
<div class="stage" id="stage">
__SCREENS__
<div class="rip" id="rip"></div>
</div>
</div></div></div>

<p class="legend">Зелёный круг — касание. Счётчик считает касания по интерфейсу отдельно от набора цифр: первое зависит от раскладки экранов, второе — от суммы. С клавиатуры: <code>пробел</code> — пауза, <code>←</code> и <code>→</code> — шаг, цифры — сценарий. Клик по кадру ставит на паузу. Экранов в прототипе столько же, сколько в макетах: __N_SCREENS__.</p>
</div>

<script>
const SC=__SCENARIOS__;
const SHEETS=__SHEETS__;
const stage=document.getElementById('stage'), snapshot=stage.innerHTML;
const cap=document.getElementById('captxt'), stepno=document.getElementById('stepno');
const trackbar=document.getElementById('trackbar'), mUi=document.getElementById('m-ui'), mKey=document.getElementById('m-key');
let timer=null,steps=[],idx=0,playing=true,speed=1,cur=0,uiTaps=0,keyTaps=0,silent=false,shown=null;
const q=id=>document.getElementById(id);
const scr=id=>stage.querySelector('[data-screen="'+id+'"]');

function show(id){
  stage.querySelectorAll('.scr').forEach(el=>{
    const k=el.dataset.screen;
    if(k===id) el.classList.remove('right','down');
    else if(!el.classList.contains('right')&&!el.classList.contains('down'))
      el.classList.add(SHEETS.includes(k)?'down':'right');
  });
  shown=id;
}
function find(root,spec){
  if(!spec) return null;
  if(spec.startsWith('text=')){
    const t=spec.slice(5); let best=null;
    root.querySelectorAll('*').forEach(el=>{
      if(el.children.length>3) return;
      const own=(el.textContent||'').trim();
      if(own===t||own.startsWith(t)){ if(!best||el.textContent.length<best.textContent.length) best=el; }
    });
    return best;
  }
  return root.querySelector(spec);
}
function counters(){mUi.textContent=uiTaps;mKey.textContent=keyTaps}
function ripple(el){
  if(!el||silent) return;
  const r=q('rip'), a=el.getBoundingClientRect(), b=stage.getBoundingClientRect();
  const kx=stage.offsetWidth/b.width, ky=stage.offsetHeight/b.height;
  r.style.left=((a.left+a.width/2-b.left)*kx)+'px'; r.style.top=((a.top+a.height/2-b.top)*ky)+'px';
  r.classList.remove('go'); void r.offsetWidth; r.classList.add('go');
  el.classList.add('hit'); setTimeout(()=>el.classList.remove('hit'),170);
}
function apply(st){
  if(st.s&&st.s!==shown) show(st.s);
  const root=scr(st.s||shown); if(!root) return;
  if(st.t){ uiTaps++; ripple(find(root,st.t)); }
  if(st.k){ keyTaps++; ripple([...root.querySelectorAll('.kp b')].find(b=>b.textContent.trim()===st.k)); }
  if(st.set) for(const [sel,txt] of Object.entries(st.set)){
    let el=find(root,sel); if(!el) continue;
    if(el.classList.contains('k')){ const box=el.closest('.fld,.row'); el=(box&&box.querySelector('.v,.num'))||el; }
    el.textContent=txt; el.classList.remove('ask','none');
  }
  if(st.css) for(const [sel,style] of Object.entries(st.css)){ const el=find(root,sel); if(el) el.style.cssText=style; }
  if('theme' in st) document.documentElement.dataset.theme=st.theme;
  counters();
}
function reset(){
  clearTimeout(timer); stage.innerHTML=snapshot; idx=0; uiTaps=0; keyTaps=0; counters(); shown=null;
  stage.querySelectorAll('.scr').forEach(el=>el.classList.add('right'));
  steps=SC[cur].steps; show(steps[0].s);
}
function paint(){stepno.textContent=idx+' / '+steps.length;trackbar.style.width=(idx/steps.length*100)+'%'}
function run(){
  if(!playing)return;
  if(idx>=steps.length){reset();timer=setTimeout(run,800/speed);return}
  const s=steps[idx++];
  try{apply(s)}catch(e){console.warn(e)}
  cap.textContent=s.c; paint();
  timer=setTimeout(run,(s.w||700)/speed);
}
function jump(n){
  clearTimeout(timer); playing=false; q('pp').textContent='Продолжить';
  n=Math.max(1,Math.min(steps.length,n));
  silent=true; stage.classList.add('nofx'); reset();
  for(let i=0;i<n;i++){try{apply(steps[i])}catch(e){}}
  idx=n; cap.textContent=steps[n-1].c; paint();
  void stage.offsetWidth; stage.classList.remove('nofx'); silent=false;
}
function start(i){
  cur=i; document.querySelectorAll('.tab').forEach((t,n)=>t.classList.toggle('on',n===i));
  document.documentElement.dataset.theme=sysTheme();
  reset(); playing=true; q('pp').textContent='Пауза'; paint(); timer=setTimeout(run,450/speed);
}
function toggle(){playing=!playing;q('pp').textContent=playing?'Пауза':'Продолжить';if(playing)run();else clearTimeout(timer)}
const sysTheme=()=>matchMedia('(prefers-color-scheme: dark)').matches?'dark':'';
const tabs=q('tabs');
SC.forEach((sc,i)=>{const b=document.createElement('button');b.className='tab'+(i?'':' on');b.innerHTML=sc.n+'<small>'+sc.uc+'</small>';b.onclick=()=>start(i);tabs.appendChild(b)});
q('pp').onclick=toggle; q('rs').onclick=()=>start(cur);
q('bk').onclick=()=>jump(idx-1); q('fw').onclick=()=>jump(idx+1);
q('frame').onclick=toggle;
q('sp').onclick=function(){speed=speed===1?1.75:speed===1.75?0.6:1;this.textContent=speed===0.6?'0,6×':speed===1?'1×':'1,75×'};
q('zm').onclick=function(){const z=getComputedStyle(document.documentElement).getPropertyValue('--z').trim();
  const nz=z==='0.6'?'1':'0.6';document.documentElement.style.setProperty('--z',nz);this.textContent=nz==='1'?'100%':'60%'};
document.addEventListener('keydown',e=>{
  if(e.key===' '){e.preventDefault();toggle()}
  else if(e.key==='ArrowLeft')jump(idx-1);
  else if(e.key==='ArrowRight')jump(idx+1);
  else if(e.key>='1'&&e.key<='9'&&SC[+e.key-1])start(+e.key-1);
  else if(e.key==='0'&&SC[9])start(9);
});
q('th').onclick=()=>{const r=document.documentElement;r.dataset.theme=r.dataset.theme==='dark'?'':'dark'};
document.documentElement.dataset.theme=sysTheme();
start(0);
</script>
</body>
</html>
"""


def build():
    src = SRC.read_text(encoding="utf-8")
    css = re.search(r"<style>(.*?)</style>", src, re.S).group(1).strip()
    sprite = re.search(r"<svg[^>]*>\s*<defs>.*?</defs>\s*</svg>", src, re.S).group(0)
    screens = []
    for fig in re.findall(r"<figure class=\"unit\">(.*?)</figure>", src, re.S):
        sid = re.search(r'<span class="sid">([A-Z]-\d\d)</span>', fig).group(1)
        title = re.search(r"<b>(.*?)</b>", fig).group(1)
        body = fig[fig.index('<div class="phone">'):].rstrip()
        screens.append(f'<div class="scr right" data-screen="{sid}" title="{sid} · {title}">\n{body}\n</div>')
    ids = [re.search(r'data-screen="([^"]+)"', x).group(1) for x in screens]
    for sc in SCENARIOS:
        for st in sc["steps"]:
            assert st["s"] in ids, f"сценарий «{sc['n']}» ссылается на несуществующий экран {st['s']}"
    out = (TEMPLATE
           .replace("__MOCKUP_CSS__", css)
           .replace("__SPRITE__", sprite)
           .replace("__SCREENS__", "\n\n".join(screens))
           .replace("__N_SCREENS__", str(len(screens)))
           .replace("__SCENARIOS__", json.dumps(SCENARIOS, ensure_ascii=False))
           .replace("__SHEETS__", json.dumps(SHEETS)))
    return out, len(screens)


if __name__ == "__main__":
    html, n = build()
    if "--check" in sys.argv:
        if not DST.exists() or DST.read_text(encoding="utf-8") != html:
            print("prototype.html устарел: запустите python tools/build_prototype.py")
            sys.exit(1)
        print(f"prototype.html актуален, экранов {n}")
    else:
        DST.write_text(html, encoding="utf-8")
        print(f"prototype.html собран: экранов {n}, сценариев {len(SCENARIOS)}")
