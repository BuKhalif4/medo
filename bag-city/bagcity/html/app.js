'use strict';
// واجهة مدينة الحقيبة. كل نص قادم من لاعب يُعرض بـ textContent فقط (لا innerHTML أبداً).

const RES = typeof GetParentResourceName === 'function' ? GetParentResourceName() : 'bagcity';
const $ = (s) => document.querySelector(s);

function post(endpoint, data) {
  return fetch(`https://${RES}/${endpoint}`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data || {}),
  }).catch(() => {});
}
function act(name, args) { post('action', { name, args: args || [] }); }

function h(tag, props, ...kids) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(props || {})) {
    if (k === 'class') el.className = v;
    else if (k.startsWith('on')) el.addEventListener(k.slice(2), v);
    else if (v !== false && v != null) el.setAttribute(k, v);
  }
  for (const kid of kids.flat()) if (kid != null && kid !== false) el.append(kid);
  return el;
}

// ===== الحالة =====
let st = {};            // حالة اللاعب من السيرفر
let nearby = [];        // لاعبون قريبون
let places = [];        // أماكن (من الإعدادات)
let bribe = { min: 5000, max: 200000 };
let catalog = { weapons: [], gear: [], tree: [], ranks: [], base: {}, refill: 0.3 };
let panel = null;       // market | ranks
let tab = 'citizen';
let picker = null;      // { title, items }
let menuOpen = false;
let hoverHint = '';

const SHAPES = [
  '40% 60% 52% 48% / 50% 42% 58% 50%', '58% 42% 46% 54% / 44% 56% 44% 56%', '46% 54% 60% 40% / 56% 44% 52% 48%',
  '52% 48% 42% 58% / 40% 58% 42% 60%',
];

const TABS = [
  { id: 'citizen', icon: '👥', title: 'قائمة المواطنين', show: () => true },
  { id: 'chase', icon: '🚨', title: 'القبض عليك', show: () => !!st.arrested },
  { id: 'police', icon: '🛡️', title: 'قائمة الشرطة', show: () => st.copAllowed || st.cop },
  { id: 'press', icon: '📰', title: 'قائمة الصحافة', show: () => st.pressAllowed || st.press },
  { id: 'tools', icon: '⚙️', title: 'أدوات التجربة', show: () => st.dev || st.ia },
];

const CLOSE = { icon: '✖', label: 'إغلاق القائمة', danger: true, hint: 'إغلاق', run: () => closeMenu() };
const money = (n) => Number(n || 0).toLocaleString('en-US');

function nearbyPicker(title, actionName) {
  return () => {
    const build = () => {
      const items = nearby.map((p) => ({ icon: '🧍', label: `${p.name} (#${p.id})`, hint: `${p.dist} م`, run: () => { act(actionName, [p.id]); picker = null; render(); } }));
      if (!items.length) items.push({ icon: '🚫', label: 'ما فيه أحد قريب', hint: 'قرّب من لاعب (٣ أمتار)', disabled: true });
      return items;
    };
    picker = { title, items: build(), refresh: () => { picker.items = build(); } };
    render();
  };
}

function placesPicker(title, kindFilter, onPick) {
  return () => {
    const items = places.filter((p) => !kindFilter || kindFilter(p)).map((p) => ({ icon: p.icon, label: p.label, hint: p.kind, run: () => { picker = null; onPick(p); if (!picker) render(); } }));
    picker = { title, items };
    render();
  };
}

// يختار النوع (الباب / داخل الغرفة / الطاولة) للأماكن، أو ينفّذ مباشرة لغيرها
function kindPicker(spot, title, onKind) {
  if (!spot.place) return onKind('main');
  const items = [{ icon: '🚪', label: 'الباب', hint: 'نقطة الاقتحام', run: () => onKind('main') },
    { icon: '🛋️', label: 'داخل الغرفة', hint: 'نقطة التخبئة', run: () => onKind('inside') }];
  if (spot.gang) items.push({ icon: '🔧', label: 'طاولة الأدوات', hint: 'داخل المقر', run: () => onKind('bench') });
  picker = { title, items };
  render();
}

function bribePicker() {
  const set = new Set([bribe.min, 10000, 25000, 50000, 100000, bribe.max].filter((v) => v >= bribe.min && v <= bribe.max));
  const items = [...set].sort((a, b) => a - b).map((v) => ({
    icon: '💰', label: money(v), hint: v > (st.balance || 0) ? 'رصيدك البنكي ما يكفي' : 'اعرضها على الشرطي',
    disabled: v > (st.balance || 0), run: () => { act('bribe', [v]); picker = null; render(); },
  }));
  picker = { title: 'اختر مبلغ الرشوة', items };
  render();
}

function itemsFor(id) {
  const bag = st.bag;
  switch (id) {
    case 'citizen': return [
      CLOSE,
      { icon: '🎭', label: st.masked ? 'شيل القناع' : 'البس القناع', hint: st.masked ? 'ترجع مدني، وما أحد يهاجمك' : 'تقدر تسرق، وتصير هدفاً', run: () => act('mask') },
      { icon: '🎒', label: 'حقيبتي', hint: bag ? `${bag.serial} · ${money(bag.value)} · ${bag.label}` : 'ما معك حقيبة', disabled: !bag, run: () => act('bag') },
      { icon: '📤', label: 'رمي الحقيبة', hint: 'أي أحد يقدر يلتقطها', disabled: !bag, run: () => act('dropbag') },
      { icon: '🤝', label: 'تمرير الحقيبة', hint: 'لشخص قريب (ترتفع حرارتها)', disabled: !bag, run: nearbyPicker('اختر من تمرّر له الحقيبة', 'passbag') },
      { icon: '📍', label: 'أماكن مميزة', hint: 'يحط علامة على الخريطة', run: placesPicker('أماكن مميزة', null, (p) => post('waypoint', { id: p.id })) },
      { icon: '👥', label: 'كم موجود؟', hint: 'عدد اللاعبين في السيرفر', run: () => post('online') },
      { icon: '💵', label: 'رصيدي', hint: `${money(st.balance)}`, run: () => toast(`رصيدك: ${money(st.balance)}`, 'good') },
    ];
    case 'chase': return [
      CLOSE,
      { icon: '🏳️', label: 'استسلام', hint: 'تخسر الحقيبة فقط وتبقى معداتك', run: () => act('surrender') },
      { icon: '💰', label: 'رشوة', hint: st.arrested && st.arrested.offer ? `عرضت ${money(st.arrested.offer)}` : 'اعرض مبلغاً على الشرطي', run: bribePicker },
    ];
    case 'police': return [
      CLOSE,
      { icon: '🛡️', label: st.cop ? 'خروج من الخدمة' : 'دخول الخدمة', hint: 'عند مركز الشرطة', disabled: !st.copAllowed, run: () => act('duty') },
      { icon: '🚔', label: 'قبض', hint: 'على لاعب قريب (ملثم أو معه حقيبة)', disabled: !st.cop, run: nearbyPicker('اختر من تقبض عليه', 'arrest') },
      { icon: '🔍', label: 'فحص أثر الفلوس', hint: 'يكشف رقم حقيبة بُيّضت مؤخراً', disabled: !st.cop, run: nearbyPicker('اختر من تفحصه', 'scan') },
      { icon: '🎖️', label: 'اختيار المعدات', hint: 'رتبتك ومعداتك (من مركز الشرطة)', disabled: !st.cop, run: () => showPanel('ranks') },
      { icon: '🗂️', label: 'قائمة الأدلة', hint: 'اقرأ سجل حياة أي حقيبة', disabled: !st.cop, run: () => act('evidencelist') },
      { icon: '✅', label: 'قبول الرشوة', hint: st.bribeOffer ? `${money(st.bribeOffer.amount)} (تنسجل في سجل الفساد)` : 'لا يوجد عرض', disabled: !st.bribeOffer, run: () => act('acceptbribe') },
      { icon: '❌', label: 'رفض الرشوة', hint: 'تصادر الحقيبة', disabled: !st.bribeOffer, run: () => act('refusebribe') },
    ];
    case 'press': return [
      CLOSE,
      { icon: '📰', label: st.press ? 'خروج من الصحافة' : 'دخول كصحفي', hint: 'وضع التجربة', disabled: !st.pressAllowed, run: () => act('press') },
      { icon: '📸', label: 'تصوير الحدث', hint: 'أقرب حدث حقيقي خلال ٣٠ متر', disabled: !st.press, run: () => act('snap') },
      { icon: '📝', label: 'نشر خبر', hint: 'العنوان يطلع للجميع', disabled: !st.press, run: () => askText('عنوان الخبر', (t) => act('publish', [t])) },
    ];
    case 'tools': return [
      CLOSE,
      { icon: '📒', label: 'سجل الرشاوى', hint: 'الشؤون الداخلية', disabled: !st.ia, run: () => act('ledger') },
      { icon: '🧭', label: 'انتقال سريع', hint: 'لأي مكان في الإعدادات', disabled: !st.dev, run: placesPicker('انتقال سريع', null, (p) => kindPicker(p, 'انتقل إلى...', (k) => { act('tp', [p.id, k]); picker = null; render(); })) },
      { icon: '📌', label: 'تحديد نقطة', hint: 'قف بالمكان الصح، يحفظ نقطتك الحالية', disabled: !st.dev, run: placesPicker('أي مكان تحدد له نقطة؟', null, (p) => kindPicker(p, 'أي نقطة؟', (k) => { act('setpoint', [p.id, k]); picker = null; render(); })) },
      { icon: '📍', label: 'إحداثياتي', hint: 'للمرجع', disabled: !st.dev, run: () => act('here') },
    ];
    default: return [CLOSE];
  }
}

// ===== الرسم =====
function render() {
  if (!menuOpen) return;
  const visible = TABS.filter((t) => t.show());
  if (!visible.find((t) => t.id === tab)) tab = 'citizen';
  const cur = TABS.find((t) => t.id === tab);

  const tabsEl = $('#tabs');
  tabsEl.replaceChildren(...visible.map((t) => h('button', {
    class: 'tab' + (t.id === tab ? ' on' : ''), title: t.title, 'aria-label': t.title, type: 'button',
    onclick: () => { tab = t.id; picker = null; render(); },
  }, t.icon)));

  let items;
  if (picker) {
    items = [{ icon: '↩', label: 'رجوع', hint: 'للقائمة السابقة', run: () => { picker = null; render(); } }, ...picker.items];
    $('#title').textContent = picker.title;
    $('#titleIcon').textContent = '👆';
  } else {
    items = itemsFor(tab);
    $('#title').textContent = cur.title;
    $('#titleIcon').textContent = cur.icon;
  }

  const n = items.length;
  const R = n > 9 ? 38 : 35;
  const size = n > 11 ? { w: .15, h: .12 } : n > 8 ? { w: .17, h: .135 } : { w: .19, h: .15 };
  const ring = $('#ring');
  ring.replaceChildren(...items.map((it, i) => {
    const a = (-90 + (360 / n) * i) * Math.PI / 180;
    const b = h('button', {
      class: 'item' + (it.danger ? ' danger' : ''), type: 'button', disabled: it.disabled || false,
      style: `left:${50 + Math.cos(a) * R}%;top:${50 + Math.sin(a) * R}%;--w:${size.w};--h:${size.h};--r:${SHAPES[i % SHAPES.length]}`,
      onclick: () => it.run && it.run(),
      onmouseenter: () => setHint(it.hint), onfocus: () => setHint(it.hint), onmouseleave: () => setHint(''),
    },
    i > 0 && i < 10 ? h('span', { class: 'num' }, String(i)) : null,
    h('span', { class: 'ico' }, it.icon), h('span', { class: 'lbl' }, it.label));
    b._item = it;
    return b;
  }));
  setHint('');
  renderHud();
}

function setHint(t) { hoverHint = t; $('#centerHint').textContent = t || (st.alias ? `اسمك: ${st.alias}` : 'اختر من القائمة'); }

function renderHud() {
  const hud = $('#hud');
  const chips = [];
  if (st.cop) chips.push(h('span', { class: 'chip cool' }, '🛡️ شرطي'));
  if (st.press) chips.push(h('span', { class: 'chip cool' }, '📰 صحفي'));
  if (st.masked) chips.push(h('span', { class: 'chip warm' }, `🎭 ${st.alias || 'ملثم'}`));
  if (st.bag) chips.push(h('span', { class: 'chip ' + (st.bag.heat >= 60 ? 'hot' : st.bag.heat >= 30 ? 'warm' : 'cool') }, `🎒 ${money(st.bag.value)} · ${st.bag.label}`));
  if (chips.length) chips.push(h('span', { class: 'chip' }, `💵 ${money(st.balance)}`));
  hud.replaceChildren(...chips);
  hud.hidden = chips.length === 0;
}

// ===== النوافذ =====
let modalCb = null;
function askText(title, cb) {
  modalCb = cb;
  $('#modalTitle').textContent = title;
  $('#modalInput').value = '';
  $('#modal').hidden = false;
  setTimeout(() => $('#modalInput').focus(), 30);
}
function closeModal() { $('#modal').hidden = true; modalCb = null; }
$('#modalForm').addEventListener('submit', (e) => {
  e.preventDefault();
  const v = $('#modalInput').value.trim();
  const cb = modalCb;
  closeModal();
  if (v && cb) cb(v);
});
$('#modalCancel').addEventListener('click', closeModal);

function showDoc(title, lines, items) {
  $('#docTitle').textContent = title;
  const ul = $('#docLines');
  if (items) {
    ul.replaceChildren(...items.map((it) => h('li', {}, h('button', { type: 'button', onclick: () => { $('#doc').hidden = true; act('evidence', [it.serial]); } },
      h('span', {}, it.serial), h('span', {}, `${money(it.value)} · ${it.note || ''}`)))));
  } else {
    ul.replaceChildren(...(lines || []).map((l) => h('li', {}, String(l))));
  }
  $('#doc').hidden = false;
}
$('#docClose').addEventListener('click', () => { $('#doc').hidden = true; });

function toast(text, kind) {
  const box = $('#toasts');
  const el = h('div', { class: 'toast ' + (kind || 'info') }, String(text));
  box.append(el);
  while (box.children.length > 4) box.firstChild.remove();
  setTimeout(() => el.remove(), 4800);
}

let bannerTimer = null;
function showBanner(title, text, kind) {
  const b = $('#banner');
  b.className = 'banner ' + (kind || '');
  b.querySelector('b').textContent = title;
  b.querySelector('span').textContent = text;
  b.hidden = false;
  clearTimeout(bannerTimer);
  bannerTimer = setTimeout(() => { b.hidden = true; }, 7000);
}

function showProgress(label, seconds) {
  const p = $('#progress');
  p.querySelector('span').textContent = label;
  const old = p.querySelector('i');
  const fresh = old.cloneNode();
  fresh.style.setProperty('--dur', `${seconds}s`);
  old.replaceWith(fresh);
  p.hidden = false;
}

// ===== فتح وإغلاق =====
function openMenu(msg) {
  menuOpen = true;
  tab = msg.tab || 'citizen';
  picker = null;
  nearby = msg.nearby || [];
  places = msg.places || places;
  if (msg.bribe) bribe = msg.bribe;
  if (msg.catalog) catalog = msg.catalog;
  if (msg.state) st = msg.state;
  if (msg.panel) return showPanel(msg.panel);
  $('#menu').hidden = false;
  render();
}
function closeMenu() {
  if (!menuOpen) return;
  menuOpen = false;
  picker = null;
  panel = null;
  $('#menu').hidden = true;
  $('#market').hidden = true;
  $('#ranks').hidden = true;
  $('#ammoModal').hidden = true;
  closeModal();
  $('#doc').hidden = true;
  post('close');
}

window.addEventListener('keydown', (e) => {
  if (e.key === 'Escape') {
    if (!$('#modal').hidden) return closeModal();
    if (!$('#ammoModal').hidden) { $('#ammoModal').hidden = true; return; }
    if (!$('#doc').hidden) { $('#doc').hidden = true; return; }
    if (picker) { picker = null; return render(); }
    return closeMenu();
  }
  if (!menuOpen || !$('#modal').hidden || e.target.tagName === 'INPUT') return;
  if (/^[1-9]$/.test(e.key)) {
    const btn = $('#ring').children[Number(e.key)];
    if (btn && !btn.disabled) btn.click();
  }
});

// ===== رسائل من اللعبة =====
window.addEventListener('message', (e) => {
  const d = e.data || {};
  switch (d.type) {
    case 'open': openMenu(d); break;
    case 'close': menuOpen = false; panel = null; $('#menu').hidden = true; $('#market').hidden = true; $('#ranks').hidden = true; $('#ammoModal').hidden = true; closeModal(); $('#doc').hidden = true; break;
    case 'state':
      st = d.state || {}; renderHud();
      if (panel) renderPanel();
      else if (menuOpen && !(document.activeElement && document.activeElement.tagName === 'INPUT')) render();
      break;
    case 'nearby': {
      // نعيد الرسم فقط لو تغيّر من هو قريب، حتى ما تضيع نقرة بين الضغط والإفلات
      const changed = nearby.map((p) => p.id).join() !== (d.list || []).map((p) => p.id).join();
      nearby = d.list || [];
      if (changed && menuOpen && picker && picker.refresh) { picker.refresh(); render(); }
      break;
    }
    case 'toast': toast(d.text, d.kind); break;
    case 'banner': showBanner(d.title, d.text, d.kind); break;
    case 'doc': showDoc(d.title, d.lines); break;
    case 'list': showDoc(d.title, null, d.items); break;
    case 'prompt': { const p = $('#prompt'); p.textContent = d.text || ''; p.hidden = !d.text; break; }
    case 'progresslabel': $('#progress').querySelector('span').textContent = d.label; break;
    case 'progress': if (d.hide) $('#progress').hidden = true; else showProgress(d.label, d.seconds); break;
    case 'zone': { const z = $('#zone'); z.hidden = !(d.left > 0); z.textContent = `ارجع لحدود المدينة! ${d.left} ثانية`; break; }
    default: break;
  }
});

// ===== السوق السوداء =====
const ICON = { pistol: '🔫', smg: '🔫', shotgun: '🔫', rifle: '🔫', rpg: '🚀', armor: '🦺', bag: '🎒' };
const SLOT_LABEL = { primary: 'سلاح رئيسي', secondary: 'سلاح ثانوي', armor: 'الدرع', bag: 'الحقيبة', pouch: 'الجراب' };
let mkTab = 'weapons';
let treeSel = null;
let treeView = { x: 30, y: 30, k: 1 };

const prog = () => st.prog || { unlocked: [], equip: {}, mods: {}, xp: 0, squares: catalog.base.squares || 18 };
const nodeOpen = (n) => !!n.root || (prog().unlocked || []).includes(n.id);
const nodeOf = (id) => catalog.tree.find((n) => n.id === id);
const slotOf = (it) => (it.cat === 'weapon' ? it.slot || 'primary' : it.cat);
const isLocked = (it) => !!it.node && !(nodeOf(it.node) && nodeOpen(nodeOf(it.node)));
const isEquipped = (it) => prog().equip[slotOf(it)] === it.id;
const itemOf = (id) => catalog.weapons.find((w) => w.id === id) || catalog.gear.find((g) => g.id === id);

function showPanel(name) {
  panel = name;
  $('#menu').hidden = true;
  $('#market').hidden = name !== 'market';
  $('#ranks').hidden = name !== 'ranks';
  menuOpen = true;
  renderPanel();
}
function renderPanel() { if (panel === 'market') renderMarket(); else if (panel === 'ranks') renderRanks(); }

function card(it) {
  const locked = isLocked(it), eq = isEquipped(it), poor = (st.cash || 0) < it.price;
  const stat = it.cat === 'weapon' ? `ذخيرة ${it.ammo}` : it.cat === 'armor' ? `حماية ${it.armor}` : `سعة ${money(it.capacity)} · ${it.squares} خانة`;
  return h('button', {
    class: `card t${it.tier}` + (eq ? ' eq' : '') + (locked ? ' locked' : ''), type: 'button', disabled: eq || (!locked && poor),
    onclick: () => { if (locked) { mkTab = 'tree'; treeSel = it.node; renderMarket(); } else act('m_buy', [it.id]); },
  }, h('span', { class: 'ico' }, ICON[it.id] || ICON[it.cat] || '📦'), h('b', {}, it.label), h('small', {}, stat),
  h('span', { class: 'price' }, eq ? 'مجهّز' : locked ? 'افتحه من الشحنات' : money(it.price)));
}

function tiers(items) {
  const out = [];
  for (const t of [1, 2, 3]) {
    const list = items.filter((i) => i.tier === t);
    if (list.length) out.push(h('section', { class: `tier t${t}` }, h('h3', {}, `الدرجة ${t}`), h('div', { class: 'cards' }, list.map(card))));
  }
  return out;
}

function renderMarket() {
  $('#mkCash').textContent = money(st.cash);
  const tabs = [['weapons', 'الأسلحة'], ['equipment', 'المعدات'], ['ammo', 'الذخيرة'], ['tree', 'الشحنات']];
  $('#mkTabs').replaceChildren(...tabs.map(([id, label]) => h('button', { class: 'mk-tab' + (id === mkTab ? ' on' : ''), type: 'button', onclick: () => { mkTab = id; renderMarket(); } }, label)));
  $('#mkClose').onclick = () => closeMenu();

  const main = $('#mkMain'), side = $('#mkSide');
  const body = main.parentElement;
  body.classList.toggle('solo', mkTab === 'tree');
  side.hidden = mkTab === 'tree';

  if (mkTab === 'weapons') main.replaceChildren(...tiers(catalog.weapons));
  else if (mkTab === 'equipment') main.replaceChildren(...tiers(catalog.gear));
  else if (mkTab === 'ammo') {
    const eqs = catalog.weapons.filter((w) => isEquipped(w));
    main.replaceChildren(eqs.length
      ? h('div', { class: 'cards' }, eqs.map((w) => h('button', { class: `card t${w.tier}`, type: 'button', onclick: () => openAmmo(w) },
        h('span', { class: 'ico' }, '🎯'), h('b', {}, w.label), h('small', {}, `${money(ammoEach(w))} للطلقة`), h('span', { class: 'price' }, 'اشترِ ذخيرة'))))
      : h('div', { class: 'empty' }, 'جهّز سلاحاً أول من تبويب «الأسلحة»'));
  } else renderTree(main);

  if (mkTab !== 'tree') renderSide(side);
}

function renderSide(side) {
  const p = prog(), squares = p.squares || 18, cap = st.cap || catalog.base.capacity || 60000;
  const bag = st.bag;
  const filled = bag ? Math.min(squares, Math.max(1, Math.ceil((bag.value / cap) * squares))) : 0;
  const cells = Array.from({ length: squares }, (_, i) => h('div', { class: 'cell' + (i < filled ? ' full' : '') }));
  const slots = ['primary', 'secondary', 'armor', 'bag', 'pouch'].map((sl) => {
    const it = itemOf(p.equip[sl]);
    return h('div', { class: 'slot' + (it ? ' on' : '') }, h('small', {}, SLOT_LABEL[sl]), h('b', {}, it ? it.label : sl === 'pouch' ? 'قريباً' : '—'));
  });
  const mods = [];
  for (const w of catalog.weapons.filter((x) => isEquipped(x) && x.mods && x.mods.length)) {
    for (const m of w.mods) {
      const done = p.mods && p.mods[w.id] && p.mods[w.id][m.id];
      mods.push(h('div', { class: 'modrow' }, h('span', {}, `${w.label} · ${m.label}`),
        h('button', { type: 'button', disabled: !!done || (st.cash || 0) < m.price, onclick: () => act('m_mod', [w.id, m.id]) }, done ? 'مركّب' : money(m.price))));
    }
  }
  side.replaceChildren(
    h('div', {}, h('h3', {}, `الحقيبة · ${squares} خانة`), h('div', { class: 'grid' }, cells),
      h('p', { class: 'sub' }, bag ? `غنائمك: ${money(bag.value)} / ${money(cap)}` : `السعة: ${money(cap)}`)),
    h('div', {}, h('h3', {}, 'التجهيز'), h('div', { class: 'slots' }, slots)),
    mods.length ? h('div', {}, h('h3', {}, 'التعديلات'), h('div', { style: 'display:flex;flex-direction:column;gap:6px' }, mods)) : null,
    h('button', { class: 'restore', type: 'button', onclick: () => act('m_restore') }, 'استرجاع معداتي بعد الدخول'));
}

// ===== شراء الذخيرة =====
const ammoEach = (w) => Math.max(1, Math.floor((w.price * (catalog.refill || 0.3)) / w.ammo));
let ammoW = null;
function openAmmo(w) {
  ammoW = w;
  $('#ammoTitle').textContent = `ذخيرة ${w.label}`;
  const r = $('#ammoRange');
  r.value = Math.min(360, w.ammo);
  updateAmmo();
  $('#ammoModal').hidden = false;
}
function updateAmmo() {
  const q = Number($('#ammoRange').value);
  $('#ammoQty').textContent = q;
  $('#ammoPrice').textContent = money(q * ammoEach(ammoW));
}
$('#ammoRange').addEventListener('input', updateAmmo);
$('#ammoCancel').addEventListener('click', () => { $('#ammoModal').hidden = true; });
$('#ammoForm').addEventListener('submit', (e) => {
  e.preventDefault();
  act('m_ammo', [ammoW.id, Number($('#ammoRange').value)]);
  $('#ammoModal').hidden = true;
});

// ===== شجرة الشحنات (سحب وتكبير) =====
function renderTree(container) {
  const W = 190, H = 120, NW = 150, NH = 70;
  const pos = (n) => ({ x: n.col * W, y: n.row * H });
  const stage = h('div', { class: 'tree-stage' });
  const wrap = h('div', { class: 'tree-wrap' }, stage);
  const apply = () => { stage.style.transform = `translate(${treeView.x}px,${treeView.y}px) scale(${treeView.k})`; };

  const maxX = Math.max(...catalog.tree.map((n) => pos(n).x)) + NW + 40, maxY = Math.max(...catalog.tree.map((n) => pos(n).y)) + NH + 40;
  const NS = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(NS, 'svg');
  svg.setAttribute('width', maxX); svg.setAttribute('height', maxY);
  for (const n of catalog.tree) {
    for (const rid of n.requires || []) {
      const r = nodeOf(rid), a = pos(r), b = pos(n);
      const path = document.createElementNS(NS, 'path');
      const x1 = a.x + NW, y1 = a.y + NH / 2, x2 = b.x, y2 = b.y + NH / 2, mx = (x1 + x2) / 2;
      path.setAttribute('d', `M${x1} ${y1} C${mx} ${y1} ${mx} ${y2} ${x2} ${y2}`);
      if (nodeOpen(r) && nodeOpen(n)) path.setAttribute('class', 'on');
      svg.append(path);
    }
  }
  stage.append(svg);

  for (const n of catalog.tree) {
    const p = pos(n), open = nodeOpen(n), ready = !open && (n.requires || []).every((r) => nodeOpen(nodeOf(r)));
    const el = h('button', { class: 'node' + (open ? ' open' : ready ? ' avail' : '') + (treeSel === n.id ? ' sel' : ''), type: 'button',
      style: `left:${p.x}px;top:${p.y}px`, onclick: (e) => { e.stopPropagation(); treeSel = n.id; renderMarket(); } },
    h('span', {}, n.label), h('small', {}, open ? 'مفتوحة' : money(n.cost)));
    stage.append(el);
  }

  // شريط التفاصيل
  const sel = treeSel && nodeOf(treeSel);
  if (sel) {
    const open = nodeOpen(sel), missing = (sel.requires || []).filter((r) => !nodeOpen(nodeOf(r))).map((r) => nodeOf(r).label);
    const items = [...catalog.weapons, ...catalog.gear].filter((i) => i.node === sel.id).map((i) => i.label);
    wrap.append(h('div', { class: 'tree-bar' },
      h('div', {}, h('b', {}, sel.label), h('div', { class: 'sub' }, items.length ? `تفتح: ${items.join('، ')}` : 'نقطة بداية'),
        missing.length ? h('div', { class: 'sub' }, `تحتاج أول: ${missing.join('، ')}`) : null),
      open ? h('span', { class: 'sub' }, 'مفتوحة ✓') : h('button', { class: 'go', type: 'button', disabled: missing.length > 0 || (st.cash || 0) < sel.cost, onclick: () => act('m_unlock', [sel.id]) }, `فتح بـ ${money(sel.cost)}`)));
  }

  // سحب
  let drag = null;
  wrap.addEventListener('pointerdown', (e) => { if (e.target.closest('.tree-bar,.node')) return; drag = { x: e.clientX - treeView.x, y: e.clientY - treeView.y }; wrap.classList.add('drag'); wrap.setPointerCapture(e.pointerId); });
  wrap.addEventListener('pointermove', (e) => { if (!drag) return; treeView.x = e.clientX - drag.x; treeView.y = e.clientY - drag.y; apply(); });
  wrap.addEventListener('pointerup', () => { drag = null; wrap.classList.remove('drag'); });
  // تكبير
  wrap.addEventListener('wheel', (e) => { e.preventDefault(); treeView.k = Math.min(1.8, Math.max(0.5, treeView.k * (e.deltaY < 0 ? 1.1 : 0.9))); apply(); }, { passive: false });
  apply();
  container.replaceChildren(wrap);
}

// ===== اختيار معدات الشرطة =====
function renderRanks() {
  const p = prog();
  $('#rkSub').textContent = `نقاطك: ${p.xp || 0}` + (st.cop ? '' : ' · ادخل الخدمة أول');
  $('#rkClose').onclick = () => closeMenu();
  $('#rkCards').replaceChildren(...catalog.ranks.map((r) => {
    const inUse = p.rank === r.id, locked = (p.xp || 0) < r.xp;
    return h('div', { class: 'rk' }, h('h2', {}, r.label),
      h('ul', {}, [...r.kit.map((k) => h('li', {}, k.label)), h('li', {}, `درع ${r.armor}`)]),
      h('div', { class: 'need' }, r.xp ? `يحتاج ${r.xp} نقطة` : 'بداية الخدمة'),
      h('button', { type: 'button', disabled: locked || !st.cop || inUse, onclick: () => act('rank_select', [r.id]) },
        inUse ? 'قيد الاستخدام' : locked ? 'مقفلة' : 'تأكيد'));
  }));
}
