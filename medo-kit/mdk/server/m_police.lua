-- سكربت الشرطة: الخدمة، الرتب والمعدات، القبض والاستسلام والرشوة، الأدلة، والمداهمة.
local P = K.P
local duty, pending, ledger, raidCooldown = {}, {}, {}, {}
local rankById = {}
for _, r in ipairs(Config.PoliceRanks) do rankById[r.id] = r end

local function cfgStore(src) return K.store('police', src) end
local function xpOf(src) return cfgStore(src).xp or 0 end

function K.isCop(src) return duty[src] == true or Bridge.isCop(src) end

local function findPendingByCop(cop)
  for target, pd in pairs(pending) do if pd.cop == cop and pd.offer then return target, pd end end
end

-- ===== النقاط والرتب =====
K.on('xp', function(src, n)
  local st = cfgStore(src)
  local before = st.xp or 0
  st.xp = before + n
  for _, r in ipairs(Config.PoliceRanks) do
    if st.xp >= r.xp and before < r.xp and r.xp > 0 then K.say(src, ('ترقية! صرت مؤهلاً لرتبة %s.'):format(r.label), 'good') end
  end
end)

local cd = {}
local function wait(src, k, seconds)
  cd[src] = cd[src] or {}
  local left = (cd[src][k] or 0) - os.time()
  if left > 0 then return left end
  cd[src][k] = os.time() + seconds
  return 0
end
K.onPlayerDrop(function(src) cd[src] = nil; duty[src] = nil; pending[src] = nil; for t, pd in pairs(pending) do if pd.cop == src then pending[t] = nil end end end)

-- ===== الخدمة =====
K.action('police', 'duty', function(src)
  if Bridge.fw ~= 'standalone' then return K.say(src, 'الوظيفة تُدار من نظام الوظائف.') end
  if not IsPlayerAceAllowed(src, 'mdk.cop') then return K.say(src, 'ما عندك صلاحية.') end
  if Config.DutyAtHQ and not K.nearPlaceType(src, 'police') then return K.say(src, 'لازم تكون عند مركز الشرطة.') end
  duty[src] = not duty[src] or nil
  K.say(src, duty[src] and 'دخلت الخدمة كشرطي.' or 'خرجت من الخدمة.')
end)

local function giveKit(src, rank)
  for _, k in ipairs(rank.kit) do TriggerClientEvent('mdk:client:gear', src, { weapon = k.weapon, ammo = k.ammo }) end
  TriggerClientEvent('mdk:client:gear', src, { armor = rank.armor })
end
K.action('police', 'rank_select', function(src, args)
  if not Sec.rate(src, 'rank', 700) or not K.isCop(src) then return end
  local rank = rankById[args[1] or '']
  if not rank then return Sec.strike(src, 'bad rank id') end
  if not K.nearPlaceType(src, 'police') then return K.say(src, 'اختيار المعدات من مركز الشرطة فقط.') end
  local st = cfgStore(src)
  if (st.xp or 0) < rank.xp then return K.say(src, ('هذي الرتبة تحتاج %d نقطة (عندك %d).'):format(rank.xp, st.xp or 0)) end
  local left = wait(src, 'kit', Config.RankKitCooldown)
  if left > 0 then return K.say(src, ('استلمت معداتك قبل شوي. انتظر %d ثانية.'):format(left)) end
  giveKit(src, rank)
  st.rank = rank.id
  K.say(src, ('استلمت معدات رتبة %s.'):format(rank.label), 'good')
end)

-- ===== القبض =====
local function seize(cop, target)
  local tp = P[target]
  if tp and tp.bag then
    local bag = tp.bag
    local cut = math.floor(bag.value * Config.CopCut)
    K.addHist(bag, 'الشرطة', 'صادرتها (' .. Bridge.name(cop) .. ')', '')
    K.incident('arrest', Sec.coords(target) or { 0, 0, 0 }, { bag = bag, serial = bag.serial, who = tp.alias or 'مدني', heat = 25 })
    K.archiveBag(bag)
    K.addCash(cop, cut)
    tp.bag = nil
    K.addXP(cop, Config.XP.seize)
    K.say(cop, ('صادرت الحقيبة %s. حصتك %d. اقرأ سجلها من «الأدلة».'):format(bag.serial, cut), 'good')
  else K.say(cop, 'ما كان معه حقيبة.') end
  if tp then tp.masked, tp.alias = false, nil end
  K.say(target, 'انقبض عليك وخسرت الحقيبة. معداتك بقت معك.', 'alert')
end

K.action('police', 'arrest', function(src, args)
  if not Sec.rate(src, 'arrest', 1500) or not K.isCop(src) then return end
  local target = tonumber(args[1])
  if not target or target == src or not GetPlayerName(target) or pending[target] then return end
  if not Sec.between(src, target, Config.HandRange) then return K.say(src, 'قرّب منه أكثر.') end
  local tp = K.getP(target)
  if not tp.masked and not tp.bag then return K.say(src, 'ما عليه شبهة.') end
  local token = {}
  pending[target] = { cop = src, token = token }
  K.say(src, 'بدأ القبض. ينتظر قراره...')
  K.say(target, ('الشرطة تقبض عليك! خلال %d ثانية: استسلم، أو اعرض رشوة، أو اهرب.'):format(Config.ArrestWindow), 'alert')
  SetTimeout(Config.ArrestWindow * 1000, function()
    local pd = pending[target]
    if not pd or pd.token ~= token then return end
    pending[target] = nil
    if Sec.between(src, target, Config.EscapeDist) then seize(src, target)
    else K.say(src, 'هرب منك.'); K.say(target, 'هربت!', 'good') end
  end)
end)

K.action('police', 'surrender', function(src)
  local pd = pending[src]
  if not pd then return end
  pending[src] = nil
  seize(pd.cop, src)
end)

K.action('police', 'bribe', function(src, args)
  local pd, amt = pending[src], tonumber(args[1])
  if not pd or not amt then return end
  amt = math.floor(amt)
  if amt < Config.BribeMin or amt > Config.BribeMax then return K.say(src, ('الرشوة بين %d و %d'):format(Config.BribeMin, Config.BribeMax)) end
  if Bridge.getBank(src) < amt then return K.say(src, 'رصيدك البنكي ما يكفي.') end
  pd.offer = amt
  K.say(src, 'عرضت الرشوة. انتظر قرار الشرطي.')
  K.say(pd.cop, ('المطلوب يعرض عليك رشوة %d. اقبل أو ارفض.'):format(amt), 'alert')
end)

K.action('police', 'acceptbribe', function(src)
  if not K.isCop(src) then return end
  local target, pd = findPendingByCop(src)
  if not target or not Sec.between(src, target, Config.EscapeDist) then return end
  if not Bridge.removeBank(target, pd.offer) then return K.say(src, 'فشلت الرشوة.') end
  if not K.addCash(src, pd.offer) then Bridge.addCash(target, pd.offer); return K.say(src, 'فشلت الرشوة.') end
  local tp = P[target]
  ledger[#ledger + 1] = { t = os.time(), cop = Bridge.name(src), amount = pd.offer, serial = tp and tp.bag and tp.bag.serial or nil }
  if tp and tp.bag then K.addHist(tp.bag, 'شرطي فاسد', 'قبض رشوة وأطلقه', '') end
  K.incident('bribe', Sec.coords(src) or { 0, 0, 0 }, { bag = tp and tp.bag, serial = tp and tp.bag and tp.bag.serial, cop = Bridge.name(src), who = tp and tp.alias or 'مدني', heat = 40 })
  pending[target] = nil
  K.say(src, 'قبلت الرشوة. الله يستر عليك...')
  K.say(target, 'الشرطي قبل الرشوة وتركك تمشي بالحقيبة.', 'good')
end)

K.action('police', 'refusebribe', function(src)
  if not K.isCop(src) then return end
  local target = findPendingByCop(src)
  if not target then return end
  pending[target] = nil
  seize(src, target)
end)

-- ===== الأدلة والفحص وسجل الفساد =====
K.action('police', 'evidence', function(src, args)
  if not K.isCop(src) then return end
  local serial = args[1] and args[1]:upper()
  local bag = serial and K.archive[serial]
  if not bag then return K.say(src, 'ما لقيت دليل بهذا الرقم.') end
  K.doc(src, ('سجل الحقيبة %s (قيمة %d)'):format(bag.serial, bag.value), K.chainLines(bag))
end)

K.action('police', 'evidencelist', function(src)
  if not K.isCop(src) then return end
  local items = {}
  for i = #K.archiveOrder, math.max(1, #K.archiveOrder - 14), -1 do
    local bag = K.archive[K.archiveOrder[i]]
    if bag then items[#items + 1] = { serial = bag.serial, value = bag.value, note = bag.history[#bag.history] and bag.history[#bag.history].what or '' } end
  end
  if #items == 0 then return K.say(src, 'لا توجد أدلة بعد.') end
  K.list(src, 'قائمة الأدلة', items)
end)

K.action('police', 'scan', function(src, args)
  if not K.isCop(src) or not Sec.rate(src, 'scan', 2000) then return end
  local target = tonumber(args[1])
  if not target or not GetPlayerName(target) or not Sec.between(src, target, Config.HandRange) then return end
  local tr = K.traces[Bridge.id(target)]
  if tr and os.time() - tr.t <= Config.TraceWindow then
    K.say(src, ('فلوسه عليها أثر الحقيبة %s قبل %d دقيقة. افتح سجلها من «الأدلة».'):format(tr.serial, math.floor((os.time() - tr.t) / 60)), 'good')
  else K.say(src, 'ما لقيت أثر.') end
end)

K.action('police', 'ledger', function(src)
  if not IsPlayerAceAllowed(src, 'mdk.ia') then return end
  if #ledger == 0 then return K.say(src, 'السجل نظيف... حالياً.') end
  local lines = {}
  for i = math.max(1, #ledger - 9), #ledger do
    local e = ledger[i]
    lines[#lines + 1] = ('[%s] %s استلم %d %s'):format(os.date('%m/%d %H:%M', e.t), e.cop, e.amount, e.serial and ('(حقيبة ' .. e.serial .. ')') or '')
  end
  K.doc(src, 'سجل الرشاوى', lines)
end)

-- ===== المداهمة =====
RegisterNetEvent('mdk:server:startRaid', function(id)
  local src = source
  if type(id) ~= 'string' or not Sec.rate(src, 'rstart', 1500) or not K.enabled('police') then return end
  local pl = K.spots[id]
  if not pl or (pl.type ~= 'house' and pl.type ~= 'gang') then return Sec.strike(src, 'bad raid place') end
  if not K.isCop(src) then return K.say(src, 'للشرطة فقط.') end
  local p = K.getP(src)
  if p.raid or p.rob or p.laund then return end
  if (raidCooldown[id] or 0) > os.time() then return K.say(src, 'المكان انداهم قبل شوي.') end
  if not K.nearSpot(src, pl, 'main') then return Sec.strike(src, 'raid out of range') end
  p.raid = { id = id, t = os.time() }
  for ownerId in pairs(K.stashes[id] or {}) do
    local o = K.srcByIdentifier(ownerId)
    if o then K.say(o, ('الشرطة تقتحم %s! الحق عليهم!'):format(pl.label), 'alert') end
  end
  TriggerClientEvent('mdk:client:raidStart', src, id, Config.Stash.raidTime)
end)
RegisterNetEvent('mdk:server:cancelRaid', function() local p = P[source]; if p then p.raid = nil end end)
RegisterNetEvent('mdk:server:finishRaid', function()
  local src = source
  local p = P[src]
  if not p or not p.raid then return end
  local pl = K.spots[p.raid.id]
  local elapsed = os.time() - p.raid.t
  p.raid = nil
  if elapsed < Config.Stash.raidTime - 1 then return Sec.strike(src, 'raid finished too early') end
  if not K.nearSpot(src, pl, 'main', pl.radius + 3.0) then return K.say(src, 'ابتعدت، انلغت المداهمة.') end
  raidCooldown[pl.id] = os.time() + Config.Stash.raidCooldown
  local count, total = 0, 0
  for ownerId, e in pairs(K.stashes[pl.id] or {}) do
    K.addHist(e.bag, 'الشرطة', 'صودرت بمداهمة ' .. pl.label, '(' .. Bridge.name(src) .. ')')
    K.archiveBag(e.bag)
    count, total = count + 1, total + e.bag.value
    local o = K.srcByIdentifier(ownerId)
    if o then K.say(o, ('انداهم مخبأك وخسرت الحقيبة %s'):format(e.bag.serial), 'alert') end
  end
  K.stashes[pl.id] = {}
  local cut = math.floor(total * Config.CopCut)
  if cut > 0 then K.addCash(src, cut) end
  K.incident('raid', K.spotPoint(pl, 'main'), { heat = 30 })
  if count > 0 then K.addXP(src, Config.XP.raid) end
  K.say(src, count > 0 and ('داهمت المكان وصادرت %d حقيبة. حصتك %d.'):format(count, cut) or 'داهمت المكان وما لقيت شي.', count > 0 and 'good' or 'info')
end)

-- ===== للواجهة =====
K.provide('police', function(src)
  local standalone = Bridge.fw == 'standalone'
  local _, offerPd = findPendingByCop(src)
  local pd = pending[src]
  local st = cfgStore(src)
  return {
    cop = K.isCop(src), allowed = (not standalone) or IsPlayerAceAllowed(src, 'mdk.cop'), ia = IsPlayerAceAllowed(src, 'mdk.ia'),
    xp = st.xp or 0, rank = st.rank, arrested = pd and { offer = pd.offer } or nil, bribeOffer = offerPd and { amount = offerPd.offer } or nil,
    ranks = (function() local l = {}; for _, r in ipairs(Config.PoliceRanks) do local kit = {}; for _, k in ipairs(r.kit) do kit[#kit + 1] = k.label end; l[#l + 1] = { id = r.id, label = r.label, xp = r.xp, armor = r.armor, kit = kit } end; return l end)(),
  }
end)
K.statsOf.police = function()
  local cops, n = 0, 0
  for _, idStr in ipairs(GetPlayers()) do if K.isCop(tonumber(idStr)) then cops = cops + 1 end end
  for _ in pairs(pending) do n = n + 1 end
  return { onDuty = cops, arrestsPending = n, bribes = #ledger }
end

if __MDK_TEST then __MT.police = { duty = duty, pending = pending, ledger = ledger } end
