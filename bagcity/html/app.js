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
    const items = places.filter((p) => !kindFilter || kindFilter(p)).map((p) => ({ icon: p.icon, label: p.label, hint: p.kind, run: () => { onPick(p); picker = null; render(); } }));
    picker = { title, items };
    render();
  };
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
      { icon: '🧭', label: 'انتقال سريع', hint: 'لأي مكان في الإعدادات', disabled: !st.dev, run: placesPicker('انتقال سريع', null, (p) => act('tp', [p.id])) },
      { icon: '📌', label: 'إحداثياتي', hint: 'لتعديل مكان في config.lua', disabled: !st.dev, run: () => act('here') },
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
  if (msg.state) st = msg.state;
  $('#menu').hidden = false;
  render();
}
function closeMenu() {
  if (!menuOpen) return;
  menuOpen = false;
  picker = null;
  $('#menu').hidden = true;
  closeModal();
  $('#doc').hidden = true;
  post('close');
}

window.addEventListener('keydown', (e) => {
  if (e.key === 'Escape') {
    if (!$('#modal').hidden) return closeModal();
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
    case 'close': menuOpen = false; $('#menu').hidden = true; closeModal(); $('#doc').hidden = true; break;
    case 'state': st = d.state || {}; renderHud(); if (menuOpen && !(document.activeElement && document.activeElement.tagName === 'INPUT')) render(); break;
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
    case 'progress': if (d.hide) $('#progress').hidden = true; else showProgress(d.label, d.seconds); break;
    case 'zone': { const z = $('#zone'); z.hidden = !(d.left > 0); z.textContent = `ارجع لحدود المدينة! ${d.left} ثانية`; break; }
    default: break;
  }
});
