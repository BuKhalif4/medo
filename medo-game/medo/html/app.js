'use strict';
// واجهة ميدو. أي نص من لاعب (أسماء) يُعرض بـ textContent فقط، لا innerHTML أبداً.

const RES = typeof GetParentResourceName === 'function' ? GetParentResourceName() : 'medo';
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
let st = { inv: [], equip: {}, mods: {}, unlocked: [] };
let catalog = { weapons: [], gear: [], tree: [], ranks: [], loot: [], base: { squares: 18, width: 6 }, refill: 0.3, partyMax: 3 };
let places = [];
let panel = null;            // market | ranks | inventory | dealer | stash
let players = [];

const ICON = { pistol: '🔫', smg: '🔫', shotgun: '🔫', rifle: '🔫', rpg: '🚀', armor: '🦺', bag: '🎒' };
const SLOT_LABEL = { primary: 'سلاح رئيسي', secondary: 'سلاح ثانوي', armor: 'الدرع', bag: 'الحقيبة', pouch: 'الجراب' };
const ROLE = { civilian: 'مدني', criminal: 'مجرم', police: 'شرطي' };
const lootOf = (id) => catalog.loot.find((l) => l.id === id);
const nodeOf = (id) => catalog.tree.find((n) => n.id === id);
const nodeOpen = (n) => !!n.root || (st.unlocked || []).includes(n.id);
const itemOf = (id) => catalog.weapons.find((w) => w.id === id) || catalog.gear.find((g) => g.id === id);
const slotOf = (it) => (it.cat === 'weapon' ? it.slot || 'primary' : it.cat);
const isLocked = (it) => !!it.node && !(nodeOf(it.node) && nodeOpen(nodeOf(it.node)));
const isEquipped = (it) => (st.equip || {})[slotOf(it)] === it.id;

// ===== HUD =====
function renderHud(d) {
  $('#loc').hidden = false;
  $('#locArea').textContent = d.area || '';
  $('#locStreet').textContent = d.street || '';
  $('#bars').hidden = false;
  $('#hpFill').style.width = `${d.hp || 0}%`;
  $('#arFill').style.width = `${d.armor || 0}%`;
  const hb = $('#hotbar');
  hb.hidden = !(d.slots && d.slots.length);
  hb.replaceChildren(...(d.slots || []).map((s, i) => h('div', { class: 'slot' }, h('small', {}, String(i + 1)), s.label, h('b', {}, `${s.ammo}`, h('span', {}, ` / ${s.max}`)))));
  const o = $('#obj');
  o.hidden = !d.objective;
  o.textContent = d.objective || '';
}
function renderRole() {
  const r = $('#role');
  r.hidden = false;
  r.className = 'role ' + (st.role || 'civilian');
  const stars = st.role === 'criminal' && st.notoriety ? ' ' + '★'.repeat(st.notoriety) : '';
  r.textContent = (ROLE[st.role] || '') + stars + (st.crim && st.role !== 'criminal' ? ' (وسم)' : '');
}

// ===== رسائل =====
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

// ===== اللوحات =====
function hideAll() {
  for (const id of ['dealer', 'stash', 'market', 'ranks', 'inventory', 'ammoModal']) $('#' + id).hidden = true;
}
function showPanel(name) {
  panel = name;
  hideAll();
  $('#' + name).hidden = false;
  renderPanel();
}
function renderPanel() {
  if (panel === 'market') renderMarket();
  else if (panel === 'ranks') renderRanks();
  else if (panel === 'inventory') renderInventory();
  else if (panel === 'stash') renderStash();
}
function closeMenu() {
  if (!panel) return;
  panel = null;
  hideAll();
  post('close');
}

// ----- التاجر والتخزين -----
$('#dealer').addEventListener('click', (e) => {
  const b = e.target.closest('button[data-go]');
  if (!b) return;
  if (b.dataset.go === 'close') closeMenu(); else showPanel(b.dataset.go);
});
$('#stash').addEventListener('click', (e) => { const b = e.target.closest('button[data-go]'); if (b && b.dataset.go === 'close') closeMenu(); });
$('#stDep').addEventListener('click', () => act('stash_deposit'));
$('#stWit').addEventListener('click', () => act('stash_withdraw'));
function renderStash() { $('#stashInfo').textContent = `في الحقيبة ${(st.inv || []).length} غرض · في التخزين ${st.stashCount || 0} غرض`; }

// ----- السوق السوداء -----
let mkTab = 'weapons';
let treeSel = null;
let treeView = { x: 30, y: 30, k: 1 };

function card(it) {
  const locked = isLocked(it), eq = isEquipped(it), poor = (st.cash || 0) < it.price;
  const stat = it.cat === 'weapon' ? `ذخيرة ${it.ammo}` : it.cat === 'armor' ? `حماية ${it.armor}` : `${it.squares}+ خانة`;
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

function gridView(extraClass) {
  const squares = st.squares || catalog.base.squares, W = st.width || catalog.base.width;
  const rows = Math.ceil(squares / W);
  const grid = h('div', { class: 'grid ' + (extraClass || ''), style: `grid-template-columns:repeat(${W},1fr)` });
  for (let i = 0; i < rows * W; i++) {
    const cell = h('div', { class: 'cell' });
    cell.style.gridColumn = String((i % W) + 1);
    cell.style.gridRow = String(Math.floor(i / W) + 1);
    if (i >= squares) cell.style.visibility = 'hidden';
    grid.append(cell);
  }
  // الغنائم فوق الخانات: نحسب الموضع كنسبة من الشبكة
  for (const it of st.inv || []) {
    const d = lootOf(it.id);
    if (!d) continue;
    const gap = 4;
    const el = h('div', { class: 'loot' }, h('b', {}, d.label), h('span', {}, money(d.value)));
    el.style.gridColumn = `${it.x + 1} / span ${d.size[0]}`;
    el.style.gridRow = `${it.y + 1} / span ${d.size[1]}`;
    el.style.margin = `${gap / 2}px`;
    grid.append(el);
  }
  return grid;
}

function renderMarket() {
  $('#mkCash').textContent = money(st.cash);
  const tabs = [['weapons', 'الأسلحة'], ['equipment', 'المعدات'], ['ammo', 'الذخيرة'], ['tree', 'الشحنات']];
  $('#mkTabs').replaceChildren(...tabs.map(([id, label]) => h('button', { class: 'mk-tab' + (id === mkTab ? ' on' : ''), type: 'button', onclick: () => { mkTab = id; renderMarket(); } }, label)));
  $('#mkClose').onclick = () => closeMenu();
  const main = $('#mkMain'), side = $('#mkSide');
  main.parentElement.classList.toggle('solo', mkTab === 'tree');
  side.hidden = mkTab === 'tree';
  if (mkTab === 'weapons') main.replaceChildren(...tiers(catalog.weapons));
  else if (mkTab === 'equipment') main.replaceChildren(...tiers(catalog.gear));
  else if (mkTab === 'ammo') {
    const eqs = catalog.weapons.filter(isEquipped);
    main.replaceChildren(eqs.length
      ? h('div', { class: 'cards' }, eqs.map((w) => h('button', { class: `card t${w.tier}`, type: 'button', onclick: () => openAmmo(w) },
        h('span', { class: 'ico' }, '🎯'), h('b', {}, w.label), h('small', {}, `${money(ammoEach(w))} للطلقة`), h('span', { class: 'price' }, 'اشترِ ذخيرة'))))
      : h('div', { class: 'empty' }, 'جهّز سلاحاً أول من تبويب «الأسلحة»'));
  } else renderTree(main);
  if (mkTab !== 'tree') renderSide(side);
}

function renderSide(side) {
  const squares = st.squares || catalog.base.squares;
  const slots = ['primary', 'secondary', 'armor', 'bag', 'pouch'].map((sl) => {
    const it = itemOf((st.equip || {})[sl]);
    return h('div', { class: 'slotbox' + (it ? ' on' : '') }, h('small', {}, SLOT_LABEL[sl]), h('b', {}, it ? it.label : sl === 'pouch' ? 'قريباً' : '—'));
  });
  const mods = [];
  for (const w of catalog.weapons.filter((x) => isEquipped(x) && x.mods && x.mods.length)) {
    for (const m of w.mods) {
      const done = st.mods && st.mods[w.id] && st.mods[w.id][m.id];
      mods.push(h('div', { class: 'modrow' }, h('span', {}, `${w.label} · ${m.label}`),
        h('button', { type: 'button', disabled: !!done || (st.cash || 0) < m.price, onclick: () => act('m_mod', [w.id, m.id]) }, done ? 'مركّب' : money(m.price))));
    }
  }
  side.replaceChildren(
    h('div', {}, h('h3', {}, `الحقيبة · ${squares} خانة`), gridView(), h('p', { class: 'sub' }, `قيمة الغنائم: ${money(st.lootValue)}`)),
    h('div', {}, h('h3', {}, 'التجهيز'), h('div', { class: 'slots' }, slots)),
    mods.length ? h('div', {}, h('h3', {}, 'التعديلات'), h('div', { style: 'display:flex;flex-direction:column;gap:6px' }, mods)) : null,
    h('button', { class: 'restore', type: 'button', onclick: () => act('m_restore') }, 'استرجاع معداتي بعد الدخول'));
}

// ----- الذخيرة -----
const ammoEach = (w) => Math.max(1, Math.floor((w.price * (catalog.refill || 0.3)) / w.ammo));
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
    stage.append(h('button', { class: 'node' + (open ? ' open' : ready ? ' avail' : '') + (treeSel === n.id ? ' sel' : ''), type: 'button',
      style: `left:${p.x}px;top:${p.y}px`, onclick: (e) => { e.stopPropagation(); treeSel = n.id; renderMarket(); } },
    h('span', {}, n.label), h('small', {}, open ? 'مفتوحة' : money(n.cost))));
  }
  const sel = treeSel && nodeOf(treeSel);
  if (sel) {
    const open = nodeOpen(sel), missing = (sel.requires || []).filter((r) => !nodeOpen(nodeOf(r))).map((r) => nodeOf(r).label);
    const items = [...catalog.weapons, ...catalog.gear].filter((i) => i.node === sel.id).map((i) => i.label);
    wrap.append(h('div', { class: 'tree-bar' },
      h('div', {}, h('b', {}, sel.label), h('div', { class: 'sub' }, items.length ? `تفتح: ${items.join('، ')}` : 'نقطة بداية'),
        missing.length ? h('div', { class: 'sub' }, `تحتاج أول: ${missing.join('، ')}`) : null),
      open ? h('span', { class: 'sub' }, 'مفتوحة ✓') : h('button', { class: 'go', type: 'button', disabled: missing.length > 0 || (st.cash || 0) < sel.cost, onclick: () => act('m_unlock', [sel.id]) }, `فتح بـ ${money(sel.cost)}`)));
  }
  let drag = null;
  wrap.addEventListener('pointerdown', (e) => { if (e.target.closest('.tree-bar,.node')) return; drag = { x: e.clientX - treeView.x, y: e.clientY - treeView.y }; wrap.classList.add('drag'); wrap.setPointerCapture(e.pointerId); });
  wrap.addEventListener('pointermove', (e) => { if (!drag) return; treeView.x = e.clientX - drag.x; treeView.y = e.clientY - drag.y; apply(); });
  wrap.addEventListener('pointerup', () => { drag = null; wrap.classList.remove('drag'); });
  wrap.addEventListener('wheel', (e) => { e.preventDefault(); treeView.k = Math.min(1.8, Math.max(0.5, treeView.k * (e.deltaY < 0 ? 1.1 : 0.9))); apply(); }, { passive: false });
  apply();
  container.replaceChildren(wrap);
}

// ----- اختيار المعدات (الشرطة) -----
function renderRanks() {
  $('#rkSub').textContent = `نقاطك: ${st.xp || 0}` + (st.role === 'police' ? '' : ' · انضم للشرطة أول');
  $('#rkClose').onclick = () => closeMenu();
  $('#rkCards').replaceChildren(...catalog.ranks.map((r) => {
    const inUse = st.rank === r.id, locked = (st.xp || 0) < r.xp;
    return h('div', { class: 'rk' }, h('h2', {}, r.label),
      h('ul', {}, [...r.kit.map((k) => h('li', {}, k.label)), h('li', {}, `درع ${r.armor}`)]),
      h('div', { class: 'need' }, r.xp ? `يحتاج ${r.xp} نقطة` : 'بداية الخدمة'),
      h('button', { type: 'button', disabled: locked || st.role !== 'police' || inUse, onclick: () => act('rank_select', [r.id]) }, inUse ? 'قيد الاستخدام' : locked ? 'مقفلة' : 'تأكيد'));
  }));
}

// ----- الحقيبة والمجموعة والأدوات -----
let ivTab = 'bag';
function renderInventory() {
  $('#ivCash').textContent = money(st.cash);
  $('#ivClose').onclick = () => closeMenu();
  const tabs = [['bag', 'الحقيبة'], ['party', 'المجموعة']];
  if (st.dev) tabs.push(['tools', 'أدوات التجربة']);
  $('#ivTabs').replaceChildren(...tabs.map(([id, label]) => h('button', { class: 'mk-tab' + (id === ivTab ? ' on' : ''), type: 'button',
    onclick: () => { ivTab = id; if (id === 'party') act('players'); renderInventory(); } }, label)));
  const main = $('#ivMain');
  if (ivTab === 'bag') {
    main.replaceChildren(
      h('div', { class: 'iv-top' }, h('span', { class: 'chip lime' }, ROLE[st.role] || ''), st.role === 'criminal' ? h('span', { class: 'chip hot' }, `السمعة ${'★'.repeat(st.notoriety || 0) || '—'}`) : null,
        h('span', { class: 'chip' }, `الغنائم ${money(st.lootValue)}`), h('span', { class: 'chip' }, `نقاط الشرطة ${st.xp || 0}`), h('span', { class: 'chip' }, `السلطة ${st.authority || 0}`), h('span', { class: 'chip' }, `وقت السجن ${st.jail || 0} ث`), h('span', { class: 'chip' }, `الموسم ${st.season || 1}`)),
      h('div', { class: 'iv-grid' }, gridView()));
  } else if (ivTab === 'party') renderParty(main);
  else renderTools(main);
}

function renderParty(main) {
  const party = st.party || { members: [], invites: [] };
  const rows = [];
  rows.push(h('div', { class: 'sect' }, `مجموعتك (${party.members.length}/${catalog.partyMax})`));
  if (party.members.length) {
    rows.push(...party.members.map((m) => h('div', { class: 'prow' }, h('span', {}, `${m.name} (#${m.id})`))));
    rows.push(h('button', { class: 'restore', type: 'button', onclick: () => act('party_leave') }, 'مغادرة المجموعة'));
  } else rows.push(h('div', { class: 'empty' }, 'ما عندك مجموعة. ادعُ لاعباً من القائمة تحت.'));
  if ((party.invites || []).length) {
    rows.push(h('div', { class: 'sect' }, 'دعوات'));
    rows.push(...party.invites.map((i) => h('div', { class: 'prow' }, h('span', {}, `${i.name} يدعوك`), h('button', { type: 'button', onclick: () => act('party_accept') }, 'قبول'))));
  }
  rows.push(h('div', { class: 'sect' }, 'اللاعبون المتصلون'));
  rows.push(players.length ? h('div', { class: 'plist' }, players.map((p) => h('div', { class: 'prow' }, h('span', {}, `${p.name} (#${p.id})`),
    h('button', { type: 'button', disabled: p.inParty, onclick: () => act('party_invite', [p.id]) }, p.inParty ? 'في مجموعة' : 'دعوة')))) : h('div', { class: 'empty' }, 'لا يوجد لاعبون آخرون الحين'));
  main.replaceChildren(h('div', { class: 'plist' }, rows));
}

function renderTools(main) {
  const rows = [h('div', { class: 'sect' }, 'انتقال سريع وتحديد النقاط (وقوفك الحالي يُحفظ)'),
    h('div', { class: 'prow' }, h('span', {}, 'كاش للتجربة'), h('button', { type: 'button', onclick: () => act('give', [100000]) }, '+100,000'))];
  for (const p of places) {
    rows.push(h('div', { class: 'prow' }, h('span', {}, p.label),
      h('span', {}, h('button', { type: 'button', onclick: () => act('tp', [p.id]) }, 'انتقل'), ' ', h('button', { type: 'button', onclick: () => act('setpoint', [p.id, 'main']) }, 'حدد هنا'),
        p.clerk ? [' ', h('button', { type: 'button', onclick: () => act('setpoint', [p.id, 'clerk']) }, 'موقف الموظف')] : null)));
  }
  main.replaceChildren(h('div', { class: 'plist' }, rows));
}

// ===== فتح/إغلاق =====
window.addEventListener('keydown', (e) => {
  if (e.key !== 'Escape') return;
  if (!$('#ammoModal').hidden) { $('#ammoModal').hidden = true; return; }
  closeMenu();
});

// ===== رسائل من اللعبة =====
window.addEventListener('message', (e) => {
  const d = e.data || {};
  switch (d.type) {
    case 'open':
      if (d.catalog) catalog = d.catalog;
      if (d.places) places = d.places;
      if (d.state) st = d.state;
      if (d.panel === 'inventory') act('players');
      showPanel(d.panel || 'inventory');
      break;
    case 'close': panel = null; hideAll(); break;
    case 'state': st = d.state || st; renderRole(); if (panel) renderPanel(); break;
    case 'players': players = d.list || []; if (panel === 'inventory') renderInventory(); break;
    case 'hud': renderHud(d); break;
    case 'toast': toast(d.text, d.kind); break;
    case 'banner': showBanner(d.title, d.text, d.kind); break;
    case 'prompt': renderPrompt(d.lines); break;
    case 'progress': if (d.hide) $('#progress').hidden = true; else showProgress(d.label, d.seconds); break;
    case 'zone': { const z = $('#zone'); z.hidden = !(d.left > 0); z.textContent = `ارجع لحدود اللعب! ${d.left} ثانية`; break; }
    default: break;
  }
});
