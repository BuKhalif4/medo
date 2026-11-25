-- Bag City server 0.3: السيرفر هو الحاكم. القائمة (NUI) تطلب فقط، والسيرفر يتحقق ويقرر.

local SOLO = GetConvar('bagcity_solotest', 'false') == 'true'   -- يسمح بالقبض على نفسك (للتجربة فقط)

local P = {}                 -- حالة كل لاعب
local drops, dropSeq = {}, 0 -- حقائب مرمية
local heat = {}              -- حرارة الأحياء
local cooldowns = {}         -- تبريد المحلات
local pending = {}           -- اعتقالات جارية (المفتاح = المطلوب)
local copsDuty, pressDuty = {}, {}
local traces = {}            -- آخر تبييض لكل لاعب
local archive, archiveOrder = {}, {}
local ledger = {}            -- سجل الرشاوى
local incidents, incSeq = {}, 0
local stashes = {}           -- placeId -> { [identifier] = { bag, t } }
local raidCooldown = {}
local zoneOut = {}
local Actions = {}

local heistById, launderById, placeById = {}, {}, {}
for _, h in ipairs(Config.Heists) do heistById[h.id] = h end
for _, l in ipairs(Config.Launderers) do launderById[l.id] = l end
for _, pl in ipairs(Config.Places) do placeById[pl.id] = pl end

local KIND = { heist = 'سطو مسلح', drop = 'حقيبة مرمية', arrest = 'عملية قبض', bribe = 'رشوة', raid = 'مداهمة' }

-- ===== أدوات عامة =====
local function clamp(v, a, b) return math.max(a, math.min(b, v)) end
local function heatLabel(h) if h < 30 then return 'بارد' elseif h < 60 then return 'دافئ' else return 'ساخن' end end

local function getP(src)
  local p = P[src]
  if not p then p = { masked = false }; P[src] = p end
  return p
end

local function isCop(src) return copsDuty[src] == true or Bridge.isCop(src) end

local function findPendingByCop(cop)
  for target, pd in pairs(pending) do
    if pd.cop == cop and pd.offer then return target, pd end
  end
end

local function srcByIdentifier(id)
  for _, s in ipairs(GetPlayers()) do
    local n = tonumber(s)
    if n and Bridge.id(n) == id then return n end
  end
end

local function nearPlaceType(src, ptype)
  for _, pl in ipairs(Config.Places) do
    if pl.type == ptype and Sec.near(src, pl.coords, pl.radius + 3.0) then return pl end
  end
end

-- ===== إرسال الحالة للقائمة =====
local function pushState(src)
  local p = P[src]
  local bag = p and p.bag
  local pd = pending[src]
  local _, offerPd = findPendingByCop(src)
  local standalone = Bridge.fw == 'standalone'
  TriggerClientEvent('bagcity:client:state', src, {
    id = src, fw = Bridge.fw, solo = SOLO,
    masked = p and p.masked or false, alias = p and p.alias or nil,
    bag = bag and { serial = bag.serial, value = bag.value, heat = bag.heat, label = heatLabel(bag.heat) } or nil,
    cop = isCop(src), press = pressDuty[src] == true,
    copAllowed = (not standalone) or IsPlayerAceAllowed(src, 'bagcity.cop'),
    pressAllowed = (not standalone) or IsPlayerAceAllowed(src, 'bagcity.press'),
    ia = IsPlayerAceAllowed(src, 'bagcity.ia'), dev = IsPlayerAceAllowed(src, 'bagcity.dev'),
    arrested = pd and { offer = pd.offer } or nil,
    bribeOffer = offerPd and { amount = offerPd.offer } or nil,
    balance = Bridge.getBank(src),
  })
end

local function say(src, msg, kind)
  TriggerClientEvent('bagcity:client:notify', src, msg, kind or 'info')
  pushState(src)
end

local function doc(src, title, lines) TriggerClientEvent('bagcity:client:doc', src, title, lines) end
local function banner(title, text, kind) TriggerClientEvent('bagcity:client:banner', -1, title, text, kind or 'news') end

-- ===== الحقيبة وسجلها =====
local function newBag()
  return { serial = ('BC-%06d'):format(math.random(0, 999999)), value = 0, heat = 0, history = {} }
end

local function addHist(bag, who, what, where)
  bag.history[#bag.history + 1] = { t = os.time(), who = who or '?', what = what, where = where or '' }
end

local function chainLines(bag)
  local lines = {}
  for _, e in ipairs(bag.history) do
    lines[#lines + 1] = ('[%s] %s - %s %s'):format(os.date('%H:%M', e.t), e.who, e.what, e.where ~= '' and ('(' .. e.where .. ')') or '')
  end
  return lines
end

local function archiveBag(bag)
  archive[bag.serial] = bag
  archiveOrder[#archiveOrder + 1] = bag.serial
  if #archiveOrder > Config.ArchiveMax then
    archive[table.remove(archiveOrder, 1)] = nil
  end
end

-- ===== الحوادث (يصوّرها الصحفي) =====
local function addIncident(kind, coords, extra)
  incSeq = incSeq + 1
  extra = extra or {}
  extra.id, extra.kind, extra.c, extra.t = incSeq, kind, coords, os.time()
  incidents[incSeq] = extra
  incidents[incSeq - Config.IncidentKeep] = nil
  return incSeq
end

-- ===== الحقائب المرمية =====
local function dropList()
  local list = {}
  for id, d in pairs(drops) do list[#list + 1] = { id = id, x = d.c[1], y = d.c[2], z = d.c[3] } end
  return list
end

local function broadcastDrops() TriggerClientEvent('bagcity:client:drops', -1, dropList()) end

local function createDrop(c, bag)
  dropSeq = dropSeq + 1
  drops[dropSeq] = { c = c, bag = bag, exp = os.time() + Config.DropTTL }
  broadcastDrops()
  return dropSeq
end

-- ===== بلاغات =====
local function alertCops(h)
  for _, idStr in ipairs(GetPlayers()) do
    local s = tonumber(idStr)
    if s and isCop(s) then
      local f = Config.AlertFuzz
      TriggerClientEvent('bagcity:client:alert', s,
        h.coords[1] + (math.random() * 2 - 1) * f, h.coords[2] + (math.random() * 2 - 1) * f, h.coords[3], h.tag)
    end
  end
  if Config.News and h.heat >= Config.NewsMinHeat then
    banner('عاجل', ('بلاغ عن سطو مسلح على %s'):format(h.label), 'alert')
  end
end

-- ===== القبض (منطق مشترك) =====
local function seize(copSrc, target)
  local tp = P[target]
  if tp and tp.bag then
    local bag = tp.bag
    local cut = math.floor(bag.value * Config.CopCut)
    addHist(bag, 'الشرطة', 'صادرتها (' .. Bridge.name(copSrc) .. ')', '')
    addIncident('arrest', Sec.coords(target) or { 0, 0, 0 }, { bag = bag, serial = bag.serial, who = tp.alias or 'مدني', heat = 25 })
    archiveBag(bag)
    Bridge.addCash(copSrc, cut)
    tp.bag = nil
    say(copSrc, ('صادرت الحقيبة %s. حصتك %d. تقدر تقرأ سجلها من «قائمة الأدلة».'):format(bag.serial, cut), 'good')
  else
    say(copSrc, 'ما كان معه حقيبة.')
  end
  if tp then tp.masked, tp.alias = false, nil end
  say(target, 'انقبض عليك وخسرت الحقيبة. معداتك بقت معك.', 'alert')
end

-- ===== الإجراءات (من القائمة) =====
function Actions.mask(src)
  if not Sec.rate(src, 'mask', 1500) then return end
  local p = getP(src)
  p.masked = not p.masked
  p.alias = p.masked and ('قناع-%04d'):format(math.random(1000, 9999)) or nil
  say(src, p.masked and ('لبست القناع. اسمك للعصابات: ' .. p.alias) or 'شلت القناع. ما أحد يقدر يهاجمك.')
end

function Actions.bag(src)
  local p = getP(src)
  if not p.bag then return say(src, 'ما معك حقيبة.') end
  say(src, ('الحقيبة %s | القيمة %d | الحرارة %s'):format(p.bag.serial, p.bag.value, heatLabel(p.bag.heat)))
end

function Actions.dropbag(src)
  if not Sec.rate(src, 'drop', 1000) then return end
  local p = P[src]
  if not p or not p.bag then return end
  local c = Sec.coords(src)
  if not c then return end
  addHist(p.bag, p.alias or 'مدني', 'رماها بالشارع', '')
  addIncident('drop', c, { bag = p.bag, serial = p.bag.serial, who = p.alias or 'مدني', heat = p.bag.heat })
  createDrop(c, p.bag)
  p.bag = nil
  say(src, 'رميت الحقيبة. أي أحد يقدر يلتقطها.')
end

function Actions.passbag(src, args)
  if not Sec.rate(src, 'pass', 1000) then return end
  local p = P[src]
  local target = tonumber(args[1])
  if not p or not p.bag or not target or target == src or not GetPlayerName(target) then return end
  if not Sec.between(src, target, Config.HandRange) then return say(src, 'قرّب منه أكثر.') end
  local tp = getP(target)
  if tp.bag then return say(src, 'معه حقيبة أصلاً.') end
  p.bag.heat = math.min(100, p.bag.heat + Config.PassHeat)
  addHist(p.bag, p.alias or 'مدني', 'مرّرها إلى ' .. (tp.alias or 'مدني'), '')
  tp.bag, p.bag = p.bag, nil
  say(src, 'مرّرت الحقيبة.')
  say(target, 'استلمت حقيبة! افتح «حقيبتي» لتشوف تفاصيلها.', 'good')
end

-- ----- الشرطة -----
function Actions.duty(src)
  if Bridge.fw ~= 'standalone' then return say(src, 'الوظيفة تُدار من نظام الوظائف.') end
  if not IsPlayerAceAllowed(src, 'bagcity.cop') then return say(src, 'ما عندك صلاحية.') end
  if Config.DutyAtHQ and not nearPlaceType(src, 'police') then return say(src, 'لازم تكون عند مركز الشرطة.') end
  copsDuty[src] = not copsDuty[src] or nil
  say(src, copsDuty[src] and 'دخلت الخدمة كشرطي.' or 'خرجت من الخدمة.')
end

function Actions.arrest(src, args)
  if not Sec.rate(src, 'arrest', 1500) or not isCop(src) then return end
  local target = tonumber(args[1])
  if not target or (target == src and not SOLO) or not GetPlayerName(target) or pending[target] then return end
  if not Sec.between(src, target, Config.HandRange) then return say(src, 'قرّب منه أكثر.') end
  local tp = getP(target)
  if not tp.masked and not tp.bag then return say(src, 'ما عليه شبهة.') end
  local token = {}
  pending[target] = { cop = src, token = token }
  say(src, 'بدأ القبض. ينتظر قراره...')
  say(target, ('الشرطة تقبض عليك! خلال %d ثانية: استسلم، أو اعرض رشوة، أو اهرب.'):format(Config.ArrestWindow), 'alert')
  SetTimeout(Config.ArrestWindow * 1000, function()
    local pd = pending[target]
    if not pd or pd.token ~= token then return end
    pending[target] = nil
    if Sec.between(src, target, Config.EscapeDist) then
      seize(src, target)
    else
      say(src, 'هرب منك.'); say(target, 'هربت!', 'good')
    end
  end)
end

function Actions.surrender(src)
  local pd = pending[src]
  if not pd then return end
  pending[src] = nil
  seize(pd.cop, src)
end

function Actions.bribe(src, args)
  local pd = pending[src]
  local amt = tonumber(args[1])
  if not pd or not amt then return end
  amt = math.floor(amt)
  if amt < Config.BribeMin or amt > Config.BribeMax then
    return say(src, ('الرشوة بين %d و %d'):format(Config.BribeMin, Config.BribeMax))
  end
  if Bridge.getBank(src) < amt then return say(src, 'رصيدك البنكي ما يكفي.') end
  pd.offer = amt
  say(src, 'عرضت الرشوة. انتظر قرار الشرطي.')
  say(pd.cop, ('المطلوب يعرض عليك رشوة %d. اقبل أو ارفض من القائمة.'):format(amt), 'alert')
end

function Actions.acceptbribe(src)
  if not isCop(src) then return end
  local target, pd = findPendingByCop(src)
  if not target then return end
  if not Sec.between(src, target, Config.EscapeDist) then return end
  if not Bridge.removeBank(target, pd.offer) then return say(src, 'فشلت الرشوة.') end
  if not Bridge.addCash(src, pd.offer) then
    Bridge.addCash(target, pd.offer)   -- ردّ المبلغ لو فشل الدفع للشرطي
    return say(src, 'فشلت الرشوة.')
  end
  local tp = P[target]
  ledger[#ledger + 1] = { t = os.time(), cop = Bridge.name(src), amount = pd.offer, serial = tp and tp.bag and tp.bag.serial or nil }
  if tp and tp.bag then addHist(tp.bag, 'شرطي فاسد', 'قبض رشوة وأطلقه', '') end
  addIncident('bribe', Sec.coords(src) or { 0, 0, 0 }, { bag = tp and tp.bag, serial = tp and tp.bag and tp.bag.serial, cop = Bridge.name(src), who = tp and tp.alias or 'مدني', heat = 40 })
  pending[target] = nil
  say(src, 'قبلت الرشوة. الله يستر عليك...')
  say(target, 'الشرطي قبل الرشوة وتركك تمشي بالحقيبة.', 'good')
end

function Actions.refusebribe(src)
  if not isCop(src) then return end
  local target = findPendingByCop(src)
  if not target then return end
  pending[target] = nil
  seize(src, target)
end

function Actions.evidence(src, args)
  if not isCop(src) then return end
  local serial = args[1] and args[1]:upper()
  local bag = serial and archive[serial]
  if not bag then return say(src, 'ما لقيت دليل بهذا الرقم.') end
  doc(src, ('سجل الحقيبة %s (قيمة %d)'):format(bag.serial, bag.value), chainLines(bag))
end

function Actions.evidencelist(src)
  if not isCop(src) then return end
  local items = {}
  for i = #archiveOrder, math.max(1, #archiveOrder - 14), -1 do
    local bag = archive[archiveOrder[i]]
    if bag then items[#items + 1] = { serial = bag.serial, value = bag.value, note = bag.history[#bag.history] and bag.history[#bag.history].what or '' } end
  end
  if #items == 0 then return say(src, 'لا توجد أدلة بعد.') end
  TriggerClientEvent('bagcity:client:list', src, 'قائمة الأدلة', items)
end

function Actions.scan(src, args)
  if not isCop(src) or not Sec.rate(src, 'scan', 2000) then return end
  local target = tonumber(args[1])
  if not target or not GetPlayerName(target) or not Sec.between(src, target, Config.HandRange) then return end
  local tr = traces[Bridge.id(target)]
  if tr and os.time() - tr.t <= Config.TraceWindow then
    say(src, ('فلوسه عليها أثر الحقيبة %s قبل %d دقيقة. افتح سجلها من قائمة الأدلة.'):format(tr.serial, math.floor((os.time() - tr.t) / 60)), 'good')
  else
    say(src, 'ما لقيت أثر.')
  end
end

function Actions.ledger(src)
  if not IsPlayerAceAllowed(src, 'bagcity.ia') then return end
  if #ledger == 0 then return say(src, 'السجل نظيف... حالياً.') end
  local lines = {}
  for i = math.max(1, #ledger - 9), #ledger do
    local e = ledger[i]
    lines[#lines + 1] = ('[%s] %s استلم %d %s'):format(os.date('%m/%d %H:%M', e.t), e.cop, e.amount, e.serial and ('(حقيبة ' .. e.serial .. ')') or '')
  end
  doc(src, 'سجل الرشاوى', lines)
end

-- ----- الصحافة -----
function Actions.press(src)
  if Bridge.fw ~= 'standalone' then return end
  if not IsPlayerAceAllowed(src, 'bagcity.press') then return say(src, 'ما عندك صلاحية.') end
  pressDuty[src] = not pressDuty[src] or nil
  say(src, pressDuty[src] and 'دخلت كصحفي. قرّب من حدث وصوّره، ثم انشر الخبر.' or 'خرجت من الصحافة.')
end

function Actions.snap(src)
  if not pressDuty[src] or not Sec.rate(src, 'snap', 3000) then return end
  local me, now, best, bestD = Sec.coords(src), os.time(), nil, Config.SnapRange
  if not me then return end
  for _, inc in pairs(incidents) do
    if not inc.published and now - inc.t <= Config.IncidentTTL then
      local d = Sec.dist(me, inc.c)
      if d <= bestD then best, bestD = inc, d end
    end
  end
  if not best then return say(src, 'ما فيه حدث قريب يستاهل التصوير.') end
  local p = getP(src)
  p.shots = p.shots or {}
  p.shots[best.id] = true
  say(src, ('صوّرت: %s. اكتب العنوان وانشر قبل ما يبرد الخبر.'):format(KIND[best.kind] or best.kind), 'good')
end

function Actions.publish(src, args)
  if not pressDuty[src] or not Sec.rate(src, 'publish', 5000) then return end
  local p = getP(src)
  local pick
  for id in pairs(p.shots or {}) do
    local inc = incidents[id]
    if inc and not inc.published and os.time() - inc.t <= Config.IncidentTTL then
      if not pick or inc.t > pick.t then pick = inc end
    end
  end
  if not pick then return say(src, 'ما عندك صور صالحة للنشر. صوّر حدثاً أولاً.') end
  local headline = table.concat(args, ' '):gsub('%^%d', ''):sub(1, Config.HeadlineMax)
  if #headline < 3 then return say(src, 'اكتب عنواناً.') end
  pick.published = true
  p.shots[pick.id] = nil
  local tail = pick.kind == 'bribe' and pick.cop and (' | الشرطي: ' .. pick.cop) or ''
  banner('الصحافة - ' .. Bridge.name(src), headline .. tail, 'news')
  if pick.bag then
    pick.bag.heat = math.min(100, pick.bag.heat + Config.PressHeat)
    addHist(pick.bag, 'صحفي ' .. Bridge.name(src), 'نشر خبراً: ' .. headline, '')
  end
  local reward = math.min(Config.PressRewardMax, 250 + (pick.heat or 0) * 60)
  Bridge.addCash(src, reward)
  say(src, ('تم النشر! مكافأتك %d.'):format(reward), 'good')
end

-- ----- المخابئ -----
function Actions.stash(src, args)
  if not Sec.rate(src, 'stash', 1000) then return end
  local pl = placeById[args[1] or '']
  if not pl or (pl.type ~= 'house' and pl.type ~= 'gang') then return Sec.strike(src, 'bad stash place') end
  if not Sec.near(src, pl.coords, pl.radius + 2.0) then return Sec.strike(src, 'stash out of range') end
  local p, id = getP(src), Bridge.id(src)
  local s = stashes[pl.id]
  if not s then s = {}; stashes[pl.id] = s end
  if p.bag then
    if s[id] then return say(src, 'عندك حقيبة مخبأة هنا أصلاً.') end
    addHist(p.bag, p.alias or 'مدني', 'خبّأها في', pl.label)
    s[id] = { bag = p.bag, t = os.time() }
    p.bag = nil
    say(src, 'خبّأت الحقيبة. حرارتها بتبرد مع الوقت، بس انتبه من المداهمة!', 'good')
  else
    local e = s[id]
    if not e then return say(src, 'ما عندك حقيبة مخبأة هنا.') end
    addHist(e.bag, p.alias or 'مدني', 'استرجعها من', pl.label)
    p.bag, s[id] = e.bag, nil
    say(src, ('استرجعت حقيبتك %s (الحرارة: %s).'):format(p.bag.serial, heatLabel(p.bag.heat)), 'good')
  end
end

RegisterNetEvent('bagcity:server:startRaid', function(id)
  local src = source
  if type(id) ~= 'string' or not Sec.rate(src, 'rstart', 1500) then return end
  local pl = placeById[id]
  if not pl or (pl.type ~= 'house' and pl.type ~= 'gang') then return Sec.strike(src, 'bad raid place') end
  if not isCop(src) then return say(src, 'للشرطة فقط.') end
  local p = getP(src)
  if p.raid or p.rob or p.laund then return end
  if (raidCooldown[id] or 0) > os.time() then return say(src, 'المكان انداهم قبل شوي.') end
  if not Sec.near(src, pl.coords, pl.radius + 2.0) then return Sec.strike(src, 'raid out of range') end
  p.raid = { id = id, t = os.time() }
  for ownerId in pairs(stashes[id] or {}) do
    local o = srcByIdentifier(ownerId)
    if o then say(o, ('الشرطة تقتحم %s! الحق عليهم!'):format(pl.label), 'alert') end
  end
  TriggerClientEvent('bagcity:client:raidStart', src, id, Config.Stash.raidTime)
end)

RegisterNetEvent('bagcity:server:cancelRaid', function()
  local p = P[source]
  if p then p.raid = nil end
end)

RegisterNetEvent('bagcity:server:finishRaid', function()
  local src = source
  local p = P[src]
  if not p or not p.raid then return end
  local pl = placeById[p.raid.id]
  local elapsed = os.time() - p.raid.t
  p.raid = nil
  if elapsed < Config.Stash.raidTime - 1 then return Sec.strike(src, 'raid finished too early') end
  if not Sec.near(src, pl.coords, pl.radius + 3.0) then return say(src, 'ابتعدت، انلغت المداهمة.') end
  raidCooldown[pl.id] = os.time() + Config.Stash.raidCooldown
  local count, total = 0, 0
  for ownerId, e in pairs(stashes[pl.id] or {}) do
    addHist(e.bag, 'الشرطة', 'صودرت بمداهمة ' .. pl.label, '(' .. Bridge.name(src) .. ')')
    archiveBag(e.bag)
    count, total = count + 1, total + e.bag.value
    local o = srcByIdentifier(ownerId)
    if o then say(o, ('انداهم مخبأك وخسرت الحقيبة %s'):format(e.bag.serial), 'alert') end
  end
  stashes[pl.id] = {}
  local cut = math.floor(total * Config.CopCut)
  if cut > 0 then Bridge.addCash(src, cut) end
  addIncident('raid', pl.coords, { heat = 30 })
  say(src, count > 0 and ('داهمت المكان وصادرت %d حقيبة. حصتك %d.'):format(count, cut) or 'داهمت المكان وما لقيت شي.', count > 0 and 'good' or 'info')
end)

-- ----- التجربة -----
function Actions.tp(src, args)
  if not IsPlayerAceAllowed(src, 'bagcity.dev') then return end
  local spot = heistById[args[1] or ''] or launderById[args[1] or ''] or placeById[args[1] or '']
  if not spot then return end
  TriggerClientEvent('bagcity:client:tp', src, spot.coords[1], spot.coords[2], spot.coords[3] + 1.0)
end

function Actions.here(src)
  if not IsPlayerAceAllowed(src, 'bagcity.dev') then return end
  local c = Sec.coords(src)
  if c then say(src, ('إحداثياتك: {%.1f, %.1f, %.1f}'):format(c[1], c[2], c[3])) end
end

-- ===== بوابة الإجراءات الوحيدة =====
RegisterNetEvent('bagcity:server:action', function(name, args)
  local src = source
  if type(name) ~= 'string' or not Actions[name] then return Sec.strike(src, 'bad action') end
  if not Sec.rate(src, 'act', 250) then return end
  local clean = {}
  if type(args) == 'table' then
    for i = 1, math.min(#args, 12) do
      local v = args[i]
      if type(v) == 'string' or type(v) == 'number' then clean[#clean + 1] = tostring(v):sub(1, 120) end
    end
  end
  Actions[name](src, clean)
end)

-- أوامر الشات للمطوّر فقط (تتفعل بـ: set bagcity_commands "true"). تُسجّل بالشكل: /bc_mask ...
if GetConvar('bagcity_commands', 'false') == 'true' then
  for name, fn in pairs(Actions) do
    RegisterCommand('bc_' .. name, function(src, args) if src ~= 0 then fn(src, args) end end, false)
  end
end

RegisterNetEvent('bagcity:server:state', function()
  local src = source
  if Sec.rate(src, 'state', 500) then pushState(src) end
end)

-- ===== السطو =====
RegisterNetEvent('bagcity:server:startRob', function(id)
  local src = source
  if type(id) ~= 'string' or not Sec.rate(src, 'start', 1500) then return end
  local h = heistById[id]
  if not h then return Sec.strike(src, 'bad heist id') end
  local p = getP(src)
  if p.rob or p.laund or p.raid then return end
  if not p.masked then return say(src, 'لازم تلبس القناع أول.') end
  if (cooldowns[id] or 0) > os.time() then return say(src, 'المحل انسرق قبل شوي.') end
  if not Sec.near(src, h.coords, h.radius + 2.0) then return Sec.strike(src, 'rob out of range') end
  if p.bag and p.bag.value >= Config.MaxBagValue then return say(src, 'حقيبتك ممتلئة.') end

  p.rob = { id = id, t = os.time(), inc = addIncident('heist', h.coords, { who = p.alias, label = h.label, heat = h.heat }) }
  cooldowns[id] = os.time() + h.cooldown
  heat[h.district] = (heat[h.district] or 0) + h.heat
  alertCops(h)
  TriggerClientEvent('bagcity:client:robStart', src, id, h.duration)
end)

RegisterNetEvent('bagcity:server:cancelRob', function()
  local p = P[source]
  if not p or not p.rob then return end
  cooldowns[p.rob.id] = os.time() + 30
  p.rob = nil
end)

RegisterNetEvent('bagcity:server:finishRob', function()
  local src = source
  local p = P[src]
  if not p or not p.rob then return end
  local h = heistById[p.rob.id]
  local elapsed = os.time() - p.rob.t
  local incId = p.rob.inc
  p.rob = nil
  if elapsed < h.duration - 1 then return Sec.strike(src, 'rob finished too early') end
  if not Sec.near(src, h.coords, h.radius + 3.0) then
    cooldowns[h.id] = os.time() + 30
    return say(src, 'ابتعدت عن المحل، فشلت السرقة.')
  end
  local mult = 1 + clamp(heat[h.district] or 0, 0, 100) / 200
  local amount = math.floor(math.random(h.loot[1], h.loot[2]) * mult)
  local bag = p.bag or newBag()
  bag.value = math.min(Config.MaxBagValue, bag.value + amount)
  bag.heat = math.min(100, bag.heat + h.heat)
  addHist(bag, p.alias, 'سرقها من', h.label)
  if incidents[incId] then incidents[incId].bag, incidents[incId].serial = bag, bag.serial end
  p.bag = bag
  say(src, ('أخذت الغنيمة! حقيبتك %s (حرارة: %s). الشرطة في طريقها، اهرب وبيّضها عند التاجر.'):format(bag.serial, heatLabel(bag.heat)), 'good')
end)

-- ===== التقاط الحقائب =====
RegisterNetEvent('bagcity:server:pickup', function(id)
  local src = source
  if type(id) ~= 'number' or not Sec.rate(src, 'pickup', 800) then return end
  local d = drops[id]
  if not d then return end
  if not Sec.near(src, d.c, 3.0) then return Sec.strike(src, 'pickup out of range') end
  local p = getP(src)
  if p.bag then return say(src, 'معك حقيبة أصلاً.') end
  addHist(d.bag, p.alias or 'مدني', 'التقطها', '')
  d.bag.heat = math.min(100, d.bag.heat + Config.PassHeat)
  p.bag = d.bag
  drops[id] = nil
  broadcastDrops()
  say(src, ('التقطت حقيبة %s. دبّر نفسك!'):format(p.bag.serial), 'good')
end)

RegisterNetEvent('bagcity:server:requestSync', function()
  local src = source
  if Sec.rate(src, 'sync', 3000) then TriggerClientEvent('bagcity:client:drops', src, dropList()) end
end)

-- الموت: السيرفر يتحقق من صحة اللاعب بنفسه قبل ما يُسقط الحقيبة
RegisterNetEvent('bagcity:server:died', function()
  local src = source
  if not Sec.rate(src, 'died', 800) then return end
  local p = P[src]
  if not p or not p.bag then return end
  local ped = GetPlayerPed(src)
  if not ped or ped == 0 or GetEntityHealth(ped) > 105 then return end
  local c = Sec.coords(src) or p.last
  if not c then return end
  addHist(p.bag, p.alias or 'مدني', 'سقطت منه (مات)', '')
  addIncident('drop', c, { bag = p.bag, serial = p.bag.serial, who = p.alias or 'مدني', heat = p.bag.heat })
  createDrop(c, p.bag)
  p.bag, p.rob, p.laund, p.raid = nil, nil, nil, nil
  say(src, 'متّ وسقطت منك الحقيبة.', 'alert')
end)

-- ===== التبييض =====
RegisterNetEvent('bagcity:server:startLaunder', function(id)
  local src = source
  if type(id) ~= 'string' or not Sec.rate(src, 'lstart', 1500) then return end
  local l = launderById[id]
  if not l then return Sec.strike(src, 'bad launderer id') end
  local p = getP(src)
  if p.rob or p.laund or p.raid or not p.bag then return end
  if not Sec.near(src, l.coords, l.radius + 2.0) then return Sec.strike(src, 'launder out of range') end
  p.laund = { id = id, t = os.time() }
  TriggerClientEvent('bagcity:client:launderStart', src, id, Config.LaunderTime)
end)

RegisterNetEvent('bagcity:server:cancelLaunder', function()
  local p = P[source]
  if p then p.laund = nil end
end)

RegisterNetEvent('bagcity:server:finishLaunder', function()
  local src = source
  local p = P[src]
  if not p or not p.laund or not p.bag then return end
  local l = launderById[p.laund.id]
  local elapsed = os.time() - p.laund.t
  p.laund = nil
  if elapsed < Config.LaunderTime - 1 then return Sec.strike(src, 'launder finished too early') end
  if not Sec.near(src, l.coords, l.radius + 3.0) then return say(src, 'ابتعدت، انلغى التبييض.') end

  local bag = p.bag
  local cut = clamp(Config.LaunderMinCut + bag.heat / 100 * (Config.LaunderMaxCut - Config.LaunderMinCut), Config.LaunderMinCut, Config.LaunderMaxCut)
  local payout = math.floor(bag.value * (1 - cut))
  -- الفلوس أول، وبعدين نشيل الحقيبة (لو فشل الدفع تبقى الحقيبة معه)
  if not Bridge.addCash(src, payout) then return say(src, 'تعذّر الدفع الحين، حاول مرة ثانية.', 'alert') end
  addHist(bag, p.alias or 'مدني', 'بيّضها عند ' .. l.label, '')
  archiveBag(bag)
  traces[Bridge.id(src)] = { serial = bag.serial, t = os.time() }
  p.bag = nil
  say(src, ('بيّضت الحقيبة. التاجر أخذ %d%% وصلك %d. انتبه: الفلوس تحمل أثر الحقيبة نصف ساعة.'):format(math.floor(cut * 100), payout), 'good')
end)

-- ===== الدورات الدورية (دوال منفصلة حتى نقدر نختبرها) =====
local function minuteTick()
  for d, v in pairs(heat) do heat[d] = math.max(0, v - Config.HeatDecay) end
  for placeId, s in pairs(stashes) do
    local cool = (placeById[placeId] and placeById[placeId].cool or 1) * Config.Stash.coolPerMinute
    for _, e in pairs(s) do e.bag.heat = math.max(0, e.bag.heat - cool) end
  end
end

local function dropCleanup()
  local changed, now = false, os.time()
  for id, d in pairs(drops) do
    if d.exp <= now then drops[id] = nil; changed = true end
  end
  if changed then broadcastDrops() end
end

local function zoneTick()
  local z = Config.Zone
  for _, idStr in ipairs(GetPlayers()) do
    local src = tonumber(idStr)
    local c = src and Sec.coords(src)
    if c and not (math.abs(c[1]) < 1 and math.abs(c[2]) < 1) then   -- يتجاهل اللاعب اللي لسا ما حمّل
      local dx, dy = c[1] - z.center[1], c[2] - z.center[2]
      if dx * dx + dy * dy > z.radius * z.radius then
        zoneOut[src] = (zoneOut[src] or 0) + 3
        local left = z.warnSeconds - zoneOut[src]
        if left <= 0 then
          zoneOut[src] = nil
          TriggerClientEvent('bagcity:client:zone', src, 0)
          TriggerClientEvent('bagcity:client:tp', src, z.safe[1], z.safe[2], z.safe[3])
          say(src, 'تجاوزت حدود المدينة، رجعناك.', 'alert')
        else
          TriggerClientEvent('bagcity:client:zone', src, left)
        end
      elseif zoneOut[src] then
        zoneOut[src] = nil
        TriggerClientEvent('bagcity:client:zone', src, 0)
      end
    end
  end
end

local function trackHolders()
  for src, p in pairs(P) do
    if p.bag then p.last = Sec.coords(src) or p.last end
  end
end

local function save()
  SetResourceKvp('ledger', json.encode(ledger))
  local arc = {}
  for _, s in ipairs(archiveOrder) do arc[#arc + 1] = archive[s] end
  SetResourceKvp('archive', json.encode(arc))
  SetResourceKvp('stashes', json.encode(stashes))
end

-- للاختبار الآلي فقط
if __BAGCITY_TEST then __BT = { minuteTick = minuteTick, dropCleanup = dropCleanup, zoneTick = zoneTick, trackHolders = trackHolders, stashes = stashes, P = P } end

CreateThread(function() while true do Wait(60000); minuteTick() end end)
CreateThread(function() while true do Wait(30000); dropCleanup() end end)
CreateThread(function() while true do Wait(3000); zoneTick(); trackHolders() end end)

CreateThread(function()
  local l = GetResourceKvpString('ledger')
  if l then ledger = json.decode(l) or {} end
  local a = GetResourceKvpString('archive')
  for _, bag in ipairs(a and json.decode(a) or {}) do archive[bag.serial] = bag; archiveOrder[#archiveOrder + 1] = bag.serial end
  local st = GetResourceKvpString('stashes')
  for placeId, s in pairs(st and json.decode(st) or {}) do stashes[placeId] = s end
  while true do Wait(300000); save() end
end)

AddEventHandler('onResourceStop', function(res) if res == GetCurrentResourceName() then save() end end)

AddEventHandler('playerDropped', function()
  local src = source
  local p = P[src]
  if p and p.bag and p.last then
    addHist(p.bag, p.alias or 'مدني', 'وقعت منه (خرج من اللعبة)', '')
    createDrop(p.last, p.bag)
  end
  for t, pd in pairs(pending) do if pd.cop == src then pending[t] = nil end end
  P[src], pending[src], copsDuty[src], pressDuty[src], zoneOut[src] = nil, nil, nil, nil, nil
  Sec.clear(src)
end)
