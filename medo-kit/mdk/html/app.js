'use strict';
// لوحة تحكم MDK. أي نص من لاعب (أسماء، عناوين) يُعرض بـ textContent فقط، لا innerHTML أبداً.

const RES = typeof GetParentResourceName === 'function' ? GetParentResourceName() : 'mdk';
const $ = (s) => document.querySelector(s);
const money = (n) => Number(n || 0).toLocaleString('en-US');

function post(endpoint, data) {
  return fetch(`https://${RES}/${endpoint}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data || {}) }).catch(() => {});
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
let st = { mods: {}, modules: [] };
let cfg = { weapons: [], gear: [], tree: [], refill: 0.3, baseSquares: 18, partyMax: 3, bribe: { min: 5000, max: 200000 } };
let page = 'home';
let tab = {};                    // تبويب كل صفحة
let nearby = [], players = [], settings = [], stats = null;
let draft = {};                  // قيم الإعدادات قبل الحفظ
let hubOpen = false;
let treeSel = null;
let treeView = { x: 30, y: 30, k: 1 };

const mod = (id) => (st.mods || {})[id];
const modEnabled = (id) => (st.modules || []).some((m) => m.id === id && m.enabled);
const ICON = { pistol: '🔫', smg: '🔫', shotgun: '🔫', rifle: '🔫', rpg: '🚀', armor: '🦺', bag: '🎒' };
const SLOT_LABEL = { primary: 'سلاح رئيسي', secondary: 'سلاح ثانوي', armor: 'الدرع', bag: 'الحقيبة', pouch: 'الجراب' };

const PAGES = [
  { id: 'home', icon: '🏠', label: 'الرئيسية', sub: 'ملخص حالتك وإجراءات سريعة' },
  { id: 'bag', icon: '🎒', label: 'الحقيبة', mod: 'bag', sub: 'غنائمك وسجل حياة حقيبتك' },
  { id: 'market', icon: '🛒', label: 'السوق والشحنات', mod: 'market', sub: 'سلاح ومعدات وذخيرة وشجرة الشحنات' },
  { id: 'police', icon: '🛡️', label: 'الشرطة', mod: 'police', sub: 'الخدمة والمعدات والقبض والأدلة' },
  { id: 'press', icon: '📰', label: 'الصحافة', mod: 'press', sub: 'صوّر الأحداث الحقيقية وانشرها' },
  { id: 'party', icon: '👥', label: 'المجموعة', mod: 'party', sub: 'العب مع أصحابك' },
  { id: 'admin', icon: '⚙️', label: 'لوحة التحكم', admin: true, sub: 'تفعيل السكربتات وتعديل الإعدادات والإحصاءات' },
  { id: 'tools', icon: '🧰', label: 'أدوات التجربة', dev: true, sub: 'انتقال سريع، تحديد النقاط، كاش' },
];

// ===== الهيكل =====
function renderNav() {
  const items = PAGES.filter((p) => (p.mod ? modEnabled(p.mod) : true) && (!p.admin || st.admin) && (!p.dev || st.dev));
  const nav = $('#nav');
  const out = [];
  let sep = false;
  for (const p of items) {
    if ((p.admin || p.dev) && !sep) { out.push(h('div', { class: 'nav-sep' }, 'الإدارة')); sep = true; }
    out.push(h('button', { class: 'ni' + (page === p.id ? ' on' : ''), type: 'button', onclick: () => go(p.id) }, h('span', { class: 'ic' }, p.icon), h('span', {}, p.label)));
  }
  nav.replaceChildren(...out);
  $('#meName').textContent = st.name || '—';
  $('#meAv').textContent = (st.name || '؟').slice(0, 1).toUpperCase();
  $('#meCash').textContent = `${money(st.cash)} كاش`;
}

function go(id) {
  page = id;
  if (id === 'party') act('players');
  if (id === 'admin') { post('nearby'); act('admin_stats'); }
  if (id === 'bag' || id === 'police') post('nearby');
  render();
}

function render() {
  if (!hubOpen) return;
  const p = PAGES.find((x) => x.id === page) || PAGES[0];
  $('#pgTitle').textContent = p.label;
  $('#pgSub').textContent = p.sub || '';
  renderNav();
  const fn = { home, bag: bagPage, market: marketPage, police: policePage, press: pressPage, party: partyPage, admin: adminPage, tools: toolsPage }[page] || home;
  $('#pg').replaceChildren(...[].concat(fn()));
}

// ===== مكونات =====
const card = (title, ...kids) => h('div', { class: 'card' }, title ? h('h3', {}, title) : null, ...kids);
const chip = (text, kind) => h('span', { class: 'chip ' + (kind || '') }, text);
const meter = (pct, cls) => h('div', { class: 'meter ' + (cls || '') }, h('i', { style: `width:${Math.max(0, Math.min(100, pct))}%` }));
const btn = (label, fn, cls, disabled) => h('button', { class: 'btn ' + (cls || ''), type: 'button', disabled: disabled || false, onclick: fn }, label);
const tabs = (id, list) => h('div', { class: 'tabs' }, list.map(([k, label]) => h('button', { class: 'tab' + ((tab[id] || list[0][0]) === k ? ' on' : ''), type: 'button', onclick: () => { tab[id] = k; render(); } }, label)));
const curTab = (id, list) => tab[id] || list[0][0];
const kv = (k, v) => h('div', { class: 'kv' }, h('span', {}, k), h('span', {}, v));

function nearbyPicker(actionName, label) {
  if (!nearby.length) return h('div', { class: 'empty' }, 'ما فيه لاعب قريب (٣ أمتار)');
  return h('div', { class: 'list' }, nearby.map((p) => h('div', { class: 'item' }, h('span', {}, `${p.name} (#${p.id})`, ' ', h('small', {}, `${p.dist} م`)), btn(label, () => act(actionName, [p.id]), 'sm'))));
}

// ===== الرئيسية =====
function home() {
  const b = mod('bag') || {}, pol = mod('police') || {}, party = mod('party') || {}, press = mod('press') || {};
  const out = [];
  const chips = [];
  if (b.masked) chips.push(chip(`🎭 ${b.alias || 'ملثم'}`, 'rose'));
  if (pol.cop) chips.push(chip('🛡️ في الخدمة', 'cyan'));
  if (press.on) chips.push(chip('📰 صحفي', 'amber'));
  if (!chips.length) chips.push(chip('مدني', 'mint'));
  out.push(h('div', { class: 'grid3' },
    h('div', { class: 'card glow' }, h('h3', {}, 'أهلاً'), h('div', { class: 'big' }, st.name || ''), h('div', { class: 'row' }, h('div', { class: 'chips' }, chips))),
    h('div', { class: 'card' }, h('h3', {}, 'رصيدك'), h('div', { class: 'big g' }, money(st.cash)), h('p', { class: 'sub' }, 'كاش')),
    modEnabled('bag') ? h('div', { class: 'card' }, h('h3', {}, 'حقيبتك'),
      b.bag ? [h('div', { class: 'big' }, money(b.bag.value)), meter((b.bag.value / (b.cap || 1)) * 100), h('div', { class: 'sub' }, `${b.bag.serial} · ${b.bag.label}`), meter(b.bag.heat, 'heat')]
        : [h('div', { class: 'big' }, '—'), h('p', { class: 'sub' }, 'ما معك حقيبة. اسرق واحدة!')]) : null,
    modEnabled('police') ? h('div', { class: 'card' }, h('h3', {}, 'الشرطة'), h('div', { class: 'big' }, `${pol.xp || 0}`), h('p', { class: 'sub' }, `نقاط · الرتبة: ${(pol.ranks || []).find((r) => r.id === pol.rank)?.label || '—'}`)) : null,
    modEnabled('party') ? h('div', { class: 'card' }, h('h3', {}, 'المجموعة'), h('div', { class: 'big' }, `${(party.members || []).length}/${party.max || 3}`), h('p', { class: 'sub' }, (party.members || []).map((m) => m.name).join('، ') || 'ما عندك مجموعة')) : null));
  out.push(h('div', { class: 'sect' }, 'إجراءات سريعة'));
  out.push(h('div', { class: 'row' },
    modEnabled('bag') ? btn(b.masked ? '🎭 شيل القناع (F6)' : '🎭 البس القناع (F6)', () => act('mask'), 'pri') : null,
    modEnabled('bag') ? btn('📤 رمي الحقيبة (F7)', () => act('dropbag'), '', !b.bag) : null,
    modEnabled('market') ? btn('🛒 السوق', () => go('market')) : null,
    modEnabled('police') ? btn('🛡️ الشرطة', () => go('police')) : null));
  out.push(h('div', { class: 'sect' }, 'الاختصارات'));
  out.push(h('div', { class: 'grid3' }, ...[['F5', 'لوحة التحكم'], ['F6', 'القناع'], ['F7', 'رمي الحقيبة'], ['E', 'تفاعل / ضغطة'], ['G', 'اقتحام (شرطة)'], ['H', 'السوق / المعدات']].map(([k, t]) =>
    h('div', { class: 'item' }, h('span', {}, t), h('span', { class: 'chip cyan' }, k)))));
  return out;
}

// ===== الحقيبة =====
function bagPage() {
  const b = mod('bag') || {};
  const mk = mod('market') || {};
  const squares = mk.squares || cfg.baseSquares;
  const filled = b.bag ? Math.min(squares, Math.max(1, Math.ceil((b.bag.value / (b.cap || 1)) * squares))) : 0;
  const bagCard = card('الحقيبة الحالية', b.bag
    ? [h('div', { class: 'big g' }, money(b.bag.value)), h('p', { class: 'sub' }, `الرقم التسلسلي ${b.bag.serial}`),
      h('div', { class: 'sect' }, 'الحرارة'), meter(b.bag.heat, 'heat'), h('p', { class: 'sub' }, b.bag.label),
      h('div', { class: 'sect' }, `السعة (${money(b.cap)})`), h('div', { class: 'sq', style: `grid-template-columns:repeat(${Math.min(6, squares)},1fr)` }, Array.from({ length: squares }, (_, i) => h('i', { class: i < filled ? 'f' : '' }))),
      h('div', { class: 'row' }, btn('📤 رميها', () => act('dropbag'), 'warm'))]
    : h('div', { class: 'empty' }, 'ما معك حقيبة. البس القناع (F6) واسرق محلاً.'));
  const chain = card('سجل حياة الحقيبة', b.bag && b.bag.history.length
    ? h('ul', { class: 'tl' }, b.bag.history.map((l) => h('li', {}, l)))
    : h('div', { class: 'empty' }, 'لا يوجد سجل بعد'));
  const out = [h('div', { class: 'grid2' }, bagCard, chain)];
  out.push(h('div', { class: 'sect' }, 'تمرير الحقيبة'));
  out.push(h('div', { class: 'row' }, btn('🔄 تحديث اللاعبين القريبين', () => post('nearby'), 'sm')));
  out.push(b.bag ? nearbyPicker('passbag', 'مرّرها') : h('div', { class: 'empty' }, 'تحتاج حقيبة'));
  return out;
}

// ===== السوق والشحنات =====
const nodeOf = (id) => cfg.tree.find((n) => n.id === id);
const nodeOpen = (n) => !!n.root || ((mod('market') || {}).unlocked || []).includes(n.id);
const itemOf = (id) => cfg.weapons.find((w) => w.id === id) || cfg.gear.find((g) => g.id === id);
const slotOf = (it) => (it.cat === 'weapon' ? it.slot || 'primary' : it.cat);
const isLocked = (it) => !!it.node && !(nodeOf(it.node) && nodeOpen(nodeOf(it.node)));
const isEquipped = (it) => ((mod('market') || {}).equip || {})[slotOf(it)] === it.id;

function gcard(it) {
  const locked = isLocked(it), eq = isEquipped(it), poor = (st.cash || 0) < it.price;
  const stat = it.cat === 'weapon' ? `ذخيرة ${it.ammo}` : it.cat === 'armor' ? `حماية ${it.armor}` : `+${it.squares} خانة · سعة ${money(it.capacity)}`;
  return h('button', {
    class: `gc t${it.tier}` + (eq ? ' eq' : '') + (locked ? ' locked' : ''), type: 'button', disabled: eq || (!locked && poor),
    onclick: () => { if (locked) { tab.market = 'tree'; treeSel = it.node; render(); } else act('m_buy', [it.id]); },
  }, h('span', { class: 'ico' }, ICON[it.id] || ICON[it.cat] || '📦'), h('b', {}, it.label), h('small', {}, stat),
  h('span', { class: 'price' }, eq ? 'مجهّز' : locked ? 'افتحه من الشحنات' : money(it.price)));
}
function tiers(items) {
  const out = [];
  for (const t of [1, 2, 3]) {
    const list = items.filter((i) => i.tier === t);
    if (list.length) out.push(h('section', { class: `tier t${t}` }, h('h4', {}, `الدرجة ${t}`), h('div', { class: 'cards' }, list.map(gcard))));
  }
  return out;
}
const ammoEach = (w) => Math.max(1, Math.floor((w.price * (cfg.refill || 0.3)) / w.ammo));

function marketPage() {
  const T = [['weapons', 'الأسلحة'], ['equipment', 'المعدات'], ['ammo', 'الذخيرة'], ['tree', 'الشحنات']];
  const cur = curTab('market', T);
  const mk = mod('market') || {};
  const body = [];
  if (cur === 'weapons') body.push(...tiers(cfg.weapons));
  else if (cur === 'equipment') body.push(...tiers(cfg.gear));
  else if (cur === 'ammo') {
    const eqs = cfg.weapons.filter(isEquipped);
    body.push(eqs.length ? h('div', { class: 'cards' }, eqs.map((w) => h('button', { class: `gc t${w.tier}`, type: 'button', onclick: () => openAmmo(w) },
      h('span', { class: 'ico' }, '🎯'), h('b', {}, w.label), h('small', {}, `${money(ammoEach(w))} للطلقة`), h('span', { class: 'price' }, 'اشترِ ذخيرة'))))
      : h('div', { class: 'empty' }, 'جهّز سلاحاً أول من تبويب «الأسلحة»'));
  } else body.push(treeView_());
  const out = [tabs('market', T)];
  if (cur === 'tree') { out.push(...body); return out; }
  // الجانب: التجهيز والتعديلات
  const equip = mk.equip || {};
  const slots = ['primary', 'secondary', 'armor', 'bag', 'pouch'].map((sl) => {
    const it = itemOf(equip[sl]);
    return h('div', { class: 'slot' + (it ? ' on' : '') }, h('small', {}, SLOT_LABEL[sl]), h('b', {}, it ? it.label : sl === 'pouch' ? 'قريباً' : '—'));
  });
  const mods = [];
  for (const w of cfg.weapons.filter((x) => isEquipped(x) && x.mods && x.mods.length)) {
    for (const m of w.mods) {
      const done = mk.mods && mk.mods[w.id] && mk.mods[w.id][m.id];
      mods.push(h('div', { class: 'item' }, h('span', {}, `${w.label} · ${m.label}`), btn(done ? 'مركّب' : money(m.price), () => act('m_mod', [w.id, m.id]), 'sm', !!done || (st.cash || 0) < m.price)));
    }
  }
  const side = h('div', { class: 'list' },
    card('التجهيز', h('div', { class: 'slots' }, slots), h('div', { class: 'row' }, h('span', { class: 'chip mint' }, `${mk.squares || cfg.baseSquares} خانة`), h('span', { class: 'chip amber' }, `سعة ${money(mk.cap)}`))),
    mods.length ? card('التعديلات', h('div', { class: 'list' }, mods)) : null,
    btn('استرجاع معداتي بعد الدخول', () => act('m_restore'), 'sm'),
    h('p', { class: 'sub' }, 'الشراء من طاولة الأدوات داخل مقر العصابة (H).'));
  out.push(h('div', { class: 'mkgrid' }, h('div', {}, ...body), side));
  return out;
}

// ----- شراء الذخيرة -----
let ammoW = null;
function openAmmo(w) {
  ammoW = w;
  $('#ammoTitle').textContent = `ذخيرة ${w.label}`;
  $('#ammoRange').value = Math.min(360, w.ammo);
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
$('#ammoForm').addEventListener('submit', (e) => { e.preventDefault(); act('m_ammo', [ammoW.id, Number($('#ammoRange').value)]); $('#ammoModal').hidden = true; });

// ----- شجرة الشحنات -----
function treeView_() {
  const W = 190, H = 120, NW = 150, NH = 70;
  const pos = (n) => ({ x: n.col * W, y: n.row * H });
  const stage = h('div', { class: 'tree-stage' });
  const wrap = h('div', { class: 'tree-wrap' }, stage);
  const apply = () => { stage.style.transform = `translate(${treeView.x}px,${treeView.y}px) scale(${treeView.k})`; };
  if (!cfg.tree.length) return h('div', { class: 'empty' }, 'لا توجد شحنات');
  const maxX = Math.max(...cfg.tree.map((n) => pos(n).x)) + NW + 40, maxY = Math.max(...cfg.tree.map((n) => pos(n).y)) + NH + 40;
  const NS = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(NS, 'svg');
  svg.setAttribute('width', maxX); svg.setAttribute('height', maxY);
  for (const n of cfg.tree) {
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
  for (const n of cfg.tree) {
    const p = pos(n), open = nodeOpen(n), ready = !open && (n.requires || []).every((r) => nodeOpen(nodeOf(r)));
    stage.append(h('button', { class: 'node' + (open ? ' open' : ready ? ' avail' : '') + (treeSel === n.id ? ' sel' : ''), type: 'button', style: `left:${p.x}px;top:${p.y}px`,
      onclick: (e) => { e.stopPropagation(); treeSel = n.id; render(); } }, h('span', {}, n.label), h('small', {}, open ? 'مفتوحة' : money(n.cost))));
  }
  const sel = treeSel && nodeOf(treeSel);
  if (sel) {
    const open = nodeOpen(sel), missing = (sel.requires || []).filter((r) => !nodeOpen(nodeOf(r))).map((r) => nodeOf(r).label);
    const items = [...cfg.weapons, ...cfg.gear].filter((i) => i.node === sel.id).map((i) => i.label);
    wrap.append(h('div', { class: 'tree-bar' }, h('div', {}, h('b', {}, sel.label), h('div', { class: 'sub' }, items.length ? `تفتح: ${items.join('، ')}` : 'نقطة بداية'),
      missing.length ? h('div', { class: 'sub' }, `تحتاج أول: ${missing.join('، ')}`) : null),
    open ? h('span', { class: 'sub' }, 'مفتوحة ✓') : btn(`فتح بـ ${money(sel.cost)}`, () => act('m_unlock', [sel.id]), 'pri go', missing.length > 0 || (st.cash || 0) < sel.cost)));
  }
  let drag = null;
  wrap.addEventListener('pointerdown', (e) => { if (e.target.closest('.tree-bar,.node')) return; drag = { x: e.clientX - treeView.x, y: e.clientY - treeView.y }; wrap.classList.add('drag'); wrap.setPointerCapture(e.pointerId); });
  wrap.addEventListener('pointermove', (e) => { if (!drag) return; treeView.x = e.clientX - drag.x; treeView.y = e.clientY - drag.y; apply(); });
  wrap.addEventListener('pointerup', () => { drag = null; wrap.classList.remove('drag'); });
  wrap.addEventListener('wheel', (e) => { e.preventDefault(); treeView.k = Math.min(1.8, Math.max(0.5, treeView.k * (e.deltaY < 0 ? 1.1 : 0.9))); apply(); }, { passive: false });
  apply();
  return wrap;
}

// ===== الشرطة =====
function policePage() {
  const p = mod('police') || {};
  const T = [['duty', 'الخدمة والمعدات'], ['arrest', 'القبض والأدلة'], ['bribe', 'الرشوة']];
  if (p.ia) T.push(['ia', 'الشؤون الداخلية']);
  const cur = curTab('police', T);
  const out = [tabs('police', T)];
  if (cur === 'duty') {
    out.push(h('div', { class: 'row' }, btn(p.cop ? 'الخروج من الخدمة' : 'دخول الخدمة', () => act('duty'), p.cop ? 'warm' : 'pri', !p.allowed),
      h('span', { class: 'chip cyan' }, `نقاطك: ${p.xp || 0}`), h('span', { class: 'chip' }, 'من مركز الشرطة فقط')));
    out.push(h('div', { class: 'sect' }, 'الرتب والمعدات'));
    out.push(h('div', { class: 'ranks' }, (p.ranks || []).map((r) => {
      const inUse = p.rank === r.id, locked = (p.xp || 0) < r.xp;
      return h('div', { class: 'rk' }, h('h4', {}, r.label), h('ul', {}, [...r.kit.map((k) => h('li', {}, k)), h('li', {}, `درع ${r.armor}`)]),
        h('div', { class: 'need' }, r.xp ? `يحتاج ${r.xp} نقطة` : 'بداية الخدمة'),
        btn(inUse ? 'قيد الاستخدام' : locked ? 'مقفلة' : 'تأكيد', () => act('rank_select', [r.id]), 'pri sm', locked || !p.cop || inUse));
    })));
  } else if (cur === 'arrest') {
    out.push(h('div', { class: 'row' }, btn('🔄 تحديث القريبين', () => post('nearby'), 'sm'), btn('🗂️ قائمة الأدلة', () => act('evidencelist'), '', !p.cop)));
    out.push(h('div', { class: 'sect' }, 'قبض'));
    out.push(p.cop ? nearbyPicker('arrest', 'قبض') : h('div', { class: 'empty' }, 'ادخل الخدمة أول'));
    out.push(h('div', { class: 'sect' }, 'فحص أثر الفلوس'));
    out.push(p.cop ? nearbyPicker('scan', 'فحص') : h('div', { class: 'empty' }, 'ادخل الخدمة أول'));
  } else if (cur === 'bribe') {
    if (p.arrested) {
      out.push(card('انقبض عليك', h('p', { class: 'sub' }, 'استسلم (تخسر الحقيبة فقط) أو اعرض رشوة أو اهرب.'),
        h('div', { class: 'row' }, btn('🏳️ استسلام', () => act('surrender'), 'warm'))));
      const set = new Set([cfg.bribe.min, 10000, 25000, 50000, 100000, cfg.bribe.max].filter((v) => v >= cfg.bribe.min && v <= cfg.bribe.max));
      out.push(h('div', { class: 'sect' }, 'اعرض رشوة'));
      out.push(h('div', { class: 'row' }, [...set].sort((a, b) => a - b).map((v) => btn(`💰 ${money(v)}`, () => act('bribe', [v]), 'sm'))));
    }
    if (p.bribeOffer) {
      out.push(card('عرض رشوة', h('div', { class: 'big g' }, money(p.bribeOffer.amount)), h('p', { class: 'sub' }, 'القبول يُسجَّل في سجل الفساد.'),
        h('div', { class: 'row' }, btn('✅ قبول', () => act('acceptbribe'), 'pri'), btn('❌ رفض', () => act('refusebribe'), 'warm'))));
    }
    if (!p.arrested && !p.bribeOffer) out.push(h('div', { class: 'empty' }, 'لا يوجد قبض ولا عروض رشوة الحين.'));
  } else {
    out.push(card('سجل الفساد', h('p', { class: 'sub' }, 'كل رشوة مقبولة تتسجل هنا.'), h('div', { class: 'row' }, btn('📒 فتح السجل', () => act('ledger'), 'pri'))));
  }
  return out;
}

// ===== الصحافة =====
function pressPage() {
  const p = mod('press') || {};
  return [
    h('div', { class: 'grid2' },
      card('الخدمة', h('div', { class: 'row' }, btn(p.on ? 'الخروج من الصحافة' : 'دخول كصحفي', () => act('press'), p.on ? 'warm' : 'pri', !p.allowed), h('span', { class: 'chip ' + (p.on ? 'mint' : '') }, p.on ? 'في الخدمة' : 'خارج الخدمة'))),
      card('كيف تشتغل؟', h('ul', { class: 'tl' }, [h('li', {}, 'قرّب من حدث حقيقي (سطو، رشوة، قبض، حقيبة مرمية).'), h('li', {}, 'اضغط «تصوير الحدث» خلال ٣٠ متراً.'), h('li', {}, 'اكتب عنواناً وانشر قبل ما يبرد الخبر. النشر يسخّن الحقيبة.')]))),
    h('div', { class: 'sect' }, 'التصوير والنشر'),
    h('div', { class: 'row' }, btn('📸 تصوير الحدث', () => act('snap'), 'pri', !p.on), btn('📝 نشر خبر', () => askText('عنوان الخبر', (t) => act('publish', [t])), 'warm', !p.on)),
  ];
}

// ===== المجموعة =====
function partyPage() {
  const p = mod('party') || { members: [], invites: [], max: 3 };
  const out = [];
  out.push(h('div', { class: 'grid2' },
    card(`مجموعتك (${p.members.length}/${p.max})`, p.members.length
      ? [h('div', { class: 'list' }, p.members.map((m) => h('div', { class: 'item' }, h('span', {}, `${m.name} (#${m.id})`), h('span', { class: 'chip mint' }, 'عضو')))), h('div', { class: 'row' }, btn('مغادرة المجموعة', () => act('party_leave'), 'warm sm'))]
      : h('div', { class: 'empty' }, 'ما عندك مجموعة. ادعُ لاعباً من القائمة.')),
    card('الدعوات', (p.invites || []).length
      ? h('div', { class: 'list' }, p.invites.map((i) => h('div', { class: 'item' }, h('span', {}, `${i.name} يدعوك`), btn('قبول', () => act('party_accept'), 'pri sm'))))
      : h('div', { class: 'empty' }, 'لا توجد دعوات'))));
  out.push(h('div', { class: 'sect' }, 'اللاعبون المتصلون'));
  out.push(h('div', { class: 'row' }, btn('🔄 تحديث', () => act('players'), 'sm')));
  out.push(players.length ? h('div', { class: 'list' }, players.map((pl) => h('div', { class: 'item' }, h('span', {}, `${pl.name} (#${pl.id})`),
    btn(pl.inParty ? 'في مجموعة' : 'دعوة', () => act('party_invite', [pl.id]), 'sm', pl.inParty)))) : h('div', { class: 'empty' }, 'لا يوجد لاعبون آخرون'));
  return out;
}

// ===== لوحة التحكم (أدمن) =====
function adminPage() {
  const T = [['modules', 'السكربتات'], ['settings', 'الإعدادات'], ['stats', 'الإحصاءات']];
  const cur = curTab('admin', T);
  const out = [tabs('admin', T)];
  if (cur === 'modules') {
    out.push(h('div', { class: 'grid2' }, (st.modules || []).map((m) => h('div', { class: 'card mod' + (m.enabled ? '' : ' off') },
      h('div', { class: 'head' }, h('div', { class: 'ico' }, m.icon), h('b', {}, m.label),
        h('button', { class: 'sw' + (m.enabled ? ' on' : ''), type: 'button', 'aria-label': m.label, 'aria-pressed': String(m.enabled), onclick: () => act('admin_module', [m.id, !m.enabled]) })),
      h('p', {}, m.desc), h('div', { class: 'chips' }, chip(m.enabled ? 'يعمل' : 'متوقف', m.enabled ? 'mint' : 'rose'))))));
  } else if (cur === 'settings') {
    const byMod = {};
    for (const s of settings) (byMod[s.module] = byMod[s.module] || []).push(s);
    for (const m of st.modules || []) {
      const list = byMod[m.id];
      if (!list) continue;
      out.push(h('div', { class: 'sect' }, `${m.icon} ${m.label}`));
      for (const s of list) out.push(settingRow(s));
    }
    out.push(h('div', { class: 'row' }, btn('مسح كل الإعدادات المحفوظة', () => act('admin_reset'), 'warm sm')));
  } else {
    out.push(h('div', { class: 'row' }, btn('🔄 تحديث', () => act('admin_stats'), 'sm')));
    if (!stats) out.push(h('div', { class: 'empty' }, 'اضغط تحديث'));
    else {
      out.push(h('div', { class: 'grid3' }, card('اللاعبون', h('div', { class: 'big g' }, String(stats.players)))));
      const NAMES = { bag: ['حاملو الحقائب', 'holding'], police: ['شرطة في الخدمة', 'onDuty'], press: ['أحداث طازجة', 'freshIncidents'], party: ['مجموعات', 'parties'] };
      const cards = [];
      for (const m of st.modules || []) {
        const s = stats[m.id];
        if (!s) continue;
        cards.push(card(`${m.icon} ${m.label}`, ...Object.entries(s).map(([k, v]) => kv(k, typeof v === 'number' ? money(v) : String(v)))));
      }
      out.push(h('div', { class: 'grid3', style: 'margin-top:14px' }, ...cards));
    }
  }
  return out;
}

function settingRow(s) {
  const val = draft[s.id] !== undefined ? draft[s.id] : s.value;
  let control;
  if (s.type === 'bool') {
    control = h('button', { class: 'sw' + (s.value ? ' on' : ''), type: 'button', 'aria-label': s.label, onclick: () => act('admin_set', [s.id, !s.value]) });
    return h('div', { class: 'set' }, h('label', {}, s.label), control, h('span', {}));
  }
  const input = h('input', { type: 'number', min: s.min, max: s.max, step: s.step || 1, value: String(val), 'aria-label': s.label });
  input.addEventListener('input', () => { draft[s.id] = input.value; });
  input.addEventListener('keydown', (e) => { if (e.key === 'Enter') { act('admin_set', [s.id, input.value]); delete draft[s.id]; } });
  return h('div', { class: 'set' }, h('label', {}, s.label, h('small', {}, `من ${s.min} إلى ${s.max}`)), input,
    btn('حفظ', () => { act('admin_set', [s.id, input.value]); delete draft[s.id]; }, 'pri sm'));
}

// ===== أدوات التجربة =====
function toolsPage() {
  const spots = ((mod('zone') || {}).spots) || [];
  const out = [h('div', { class: 'row' }, btn('💵 +100,000 كاش', () => act('give', [100000]), 'pri'), h('span', { class: 'chip' }, 'قف في المكان الصحيح ثم «حدد هنا»'))];
  if (!spots.length) { out.push(h('div', { class: 'empty' }, 'فعّل سكربت «الحدود والأماكن» لرؤية الأماكن')); return out; }
  const TYPE = { heist: 'سطو', launderer: 'تبييض', police: 'الشرطة', gang: 'مقر عصابة', house: 'بيت آمن' };
  out.push(h('div', { class: 'list' }, spots.map((s) => h('div', { class: 'item' }, h('span', {}, s.label, ' ', h('small', {}, TYPE[s.type] || s.type)),
    h('span', { class: 'row', style: 'margin:0' }, btn('انتقل', () => act('tp', [s.id]), 'sm'), btn('حدد هنا', () => act('setpoint', [s.id, 'main']), 'sm'),
      s.type === 'gang' || s.type === 'house' ? [btn('داخل الغرفة', () => act('setpoint', [s.id, 'inside']), 'sm')] : null,
      s.type === 'gang' ? [btn('الطاولة', () => act('setpoint', [s.id, 'bench']), 'sm')] : null)))));
  return out;
}

// ===== نوافذ =====
let modalCb = null;
function askText(title, cb) {
  modalCb = cb;
  $('#modalTitle').textContent = title;
  $('#modalInput').value = '';
  $('#modal').hidden = false;
  setTimeout(() => $('#modalInput').focus(), 30);
}
function closeModal() { $('#modal').hidden = true; modalCb = null; }
$('#modalForm').addEventListener('submit', (e) => { e.preventDefault(); const v = $('#modalInput').value.trim(); const cb = modalCb; closeModal(); if (v && cb) cb(v); });
$('#modalCancel').addEventListener('click', closeModal);

function showDoc(title, lines, items) {
  $('#docTitle').textContent = title;
  const ul = $('#docLines');
  if (items) ul.replaceChildren(...items.map((it) => h('li', {}, h('button', { type: 'button', onclick: () => { $('#doc').hidden = true; act('evidence', [it.serial]); } }, h('span', {}, it.serial), h('span', {}, `${money(it.value)} · ${it.note || ''}`)))));
  else ul.replaceChildren(...(lines || []).map((l) => h('li', {}, String(l))));
  $('#doc').hidden = false;
}
$('#docClose').addEventListener('click', () => { $('#doc').hidden = true; });

// ===== رسائل وHUD =====
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
function renderPrompt(lines) {
  const p = $('#prompt');
  p.hidden = !lines || !lines.length;
  p.replaceChildren(...(lines || []).map((l) => {
    const row = h('div', { class: 'pl' + (l.hold ? ' hold' : '') }, l.key ? h('kbd', {}, l.key) : null, h('span', {}, l.text));
    row.style.setProperty('--p', String(l.progress || 0));
    return row;
  }));
}
function renderStatus() {
  const el = $('#status');
  const b = mod('bag') || {}, pol = mod('police') || {}, press = mod('press') || {};
  const sp = [];
  if (b.masked) sp.push(h('span', { class: 'sp rose' }, `🎭 ${b.alias || 'ملثم'}`));
  if (b.bag) sp.push(h('span', { class: 'sp ' + (b.bag.heat >= 60 ? 'rose' : b.bag.heat >= 30 ? 'amber' : 'mint') }, `🎒 ${money(b.bag.value)} · ${b.bag.label}`));
  if (pol.cop) sp.push(h('span', { class: 'sp cyan' }, '🛡️ شرطي'));
  if (press.on) sp.push(h('span', { class: 'sp amber' }, '📰 صحفي'));
  if (sp.length) sp.push(h('span', { class: 'sp' }, `💵 ${money(st.cash)}`));
  el.hidden = !sp.length;
  el.replaceChildren(...sp);
}

// ===== فتح وإغلاق =====
function openHub(msg) {
  hubOpen = true;
  if (msg.config) cfg = msg.config;
  if (msg.state) st = msg.state;
  page = msg.page && PAGES.find((p) => p.id === msg.page) ? msg.page : 'home';
  $('#hub').hidden = false;
  render();
}
function closeHub() {
  if (!hubOpen) return;
  hubOpen = false;
  $('#hub').hidden = true;
  for (const id of ['modal', 'doc', 'ammoModal']) $('#' + id).hidden = true;
  post('close');
}
$('#hubClose').addEventListener('click', closeHub);
window.addEventListener('keydown', (e) => {
  if (e.key !== 'Escape') return;
  if (!$('#ammoModal').hidden) { $('#ammoModal').hidden = true; return; }
  if (!$('#modal').hidden) return closeModal();
  if (!$('#doc').hidden) { $('#doc').hidden = true; return; }
  closeHub();
});

window.addEventListener('message', (e) => {
  const d = e.data || {};
  switch (d.type) {
    case 'open': openHub(d); break;
    case 'close': hubOpen = false; $('#hub').hidden = true; break;
    case 'state': {
      st = d.state || st;
      renderStatus();
      const cur = PAGES.find((p) => p.id === page);
      if (cur && cur.mod && !modEnabled(cur.mod)) page = 'home';
      if (hubOpen && !(document.activeElement && document.activeElement.tagName === 'INPUT')) render();
      break;
    }
    case 'settings': settings = d.list || []; if (hubOpen && page === 'admin' && !(document.activeElement && document.activeElement.tagName === 'INPUT')) render(); break;
    case 'stats': stats = d.stats; if (hubOpen && page === 'admin') render(); break;
    case 'nearby': nearby = d.list || []; if (hubOpen && ['bag', 'police'].includes(page)) render(); break;
    case 'players': players = d.list || []; if (hubOpen && page === 'party') render(); break;
    case 'toast': toast(d.text, d.kind); break;
    case 'banner': showBanner(d.title, d.text, d.kind); break;
    case 'doc': showDoc(d.title, d.lines); break;
    case 'list': showDoc(d.title, null, d.items); break;
    case 'prompt': renderPrompt(d.lines); break;
    case 'progress': if (d.hide) $('#progress').hidden = true; else showProgress(d.label, d.seconds); break;
    case 'progresslabel': $('#progress').querySelector('span').textContent = d.label; break;
    case 'zone': { const z = $('#zone'); z.hidden = !(d.left > 0); z.textContent = `ارجع لحدود المدينة! ${d.left} ثانية`; break; }
    default: break;
  }
});
