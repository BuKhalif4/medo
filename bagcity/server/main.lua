-- Bag City: السيرفر هو الحاكم. العميل يطلب فقط، والسيرفر يقرر ويتحقق.

local P = {}            -- حالة كل لاعب
local drops = {}        -- حقائب مرمية بالشارع
local dropSeq = 0
local heat = {}         -- حرارة الأحياء
local cooldowns = {}    -- تبريد المحلات
local pending = {}      -- اعتقالات جارية (المفتاح = المطلوب)
local copsDuty = {}     -- شرطة الوضع المستقل
local traces = {}       -- آخر تبييض لكل لاعب (للفحص الجنائي)
local archive = {}      -- سجلات الحقائب المصادرة/المبيّضة
local archiveOrder = {}
local ledger = {}       -- سجل الرشاوى (للشؤون الداخلية)

local heistById, launderById = {}, {}
for _, h in ipairs(Config.Heists) do heistById[h.id] = h end
for _, l in ipairs(Config.Launderers) do launderById[l.id] = l end

-- ===== أدوات =====
local function say(src, msg)
  TriggerClientEvent('chat:addMessage', src, { color = { 255, 176, 46 }, multiline = true, args = { 'المدينة', msg } })
end

local function getP(src)
  local p = P[src]
  if not p then p = { masked = false }; P[src] = p end
  return p
end

local function isCop(src) return copsDuty[src] == true or Bridge.isCop(src) end

local function clamp(v, a, b) return math.max(a, math.min(b, v)) end

local function heatLabel(h)
  if h < 30 then return 'بارد' elseif h < 60 then return 'دافئ' else return 'ساخن' end
end

-- ===== الحقيبة وسجلها =====
local function newBag()
  return { serial = ('BC-%06d'):format(math.random(0, 999999)), value = 0, heat = 0, history = {} }
end

local function addHist(bag, who, what, where)
  bag.history[#bag.history + 1] = { t = os.time(), who = who or '?', what = what, where = where or '' }
end

local function chainText(bag)
  local lines = {}
  for _, e in ipairs(bag.history) do
    lines[#lines + 1] = ('[%s] %s - %s %s'):format(os.date('%H:%M', e.t), e.who, e.what, e.where ~= '' and ('(' .. e.where .. ')') or '')
  end
  return table.concat(lines, '\n')
end

local function archiveBag(bag)
  archive[bag.serial] = bag
  archiveOrder[#archiveOrder + 1] = bag.serial
  if #archiveOrder > Config.ArchiveMax then
    archive[table.remove(archiveOrder, 1)] = nil
  end
end

local function pushBag(src)
  local p = P[src]
  if not p then return end
  local w = p.bag and clamp(p.bag.value / Config.MaxBagValue, 0, 1) or 0
  TriggerClientEvent('bagcity:client:bag', src, p.bag ~= nil, w, Config.MinSpeed)
end

-- ===== الحقائب المرمية =====
local function broadcastDrops()
  local list = {}
  for id, d in pairs(drops) do list[#list + 1] = { id = id, x = d.c[1], y = d.c[2], z = d.c[3] } end
  TriggerClientEvent('bagcity:client:drops', -1, list)
end

local function createDrop(c, bag)
  dropSeq = dropSeq + 1
  drops[dropSeq] = { c = c, bag = bag, exp = os.time() + Config.DropTTL }
  broadcastDrops()
  return dropSeq
end

-- ===== الأحياء والبلاغات =====
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
    TriggerClientEvent('chat:addMessage', -1, { color = { 255, 80, 80 }, args = { 'عاجل', ('بلاغ عن سطو مسلح على %s'):format(h.label) } })
  end
end

-- ===== القناع =====
RegisterCommand('mask', function(src)
  if src == 0 or not Sec.rate(src, 'mask', 1500) then return end
  local p = getP(src)
  p.masked = not p.masked
  p.alias = p.masked and ('قناع-%04d'):format(math.random(1000, 9999)) or nil
  TriggerClientEvent('bagcity:client:mask', src, p.masked)
  say(src, p.masked and ('لبست القناع. اسمك للعصابات: ' .. p.alias .. '. الحين تقدر تسرق وتنسرق.') or 'شلت القناع. ما أحد يقدر يهاجمك.')
end, false)

-- ===== السطو =====
RegisterNetEvent('bagcity:server:startRob', function(id)
  local src = source
  if type(id) ~= 'string' or not Sec.rate(src, 'start', 1500) then return end
  local h = heistById[id]
  if not h then return Sec.strike(src, 'bad heist id') end
  local p = getP(src)
  if p.rob or p.laund then return end
  if not p.masked then return say(src, 'لازم تلبس القناع أول: /mask') end
  if (cooldowns[id] or 0) > os.time() then return say(src, 'المحل انسرق قبل شوي.') end
  if not Sec.near(src, h.coords, h.radius + 2.0) then return Sec.strike(src, 'rob out of range') end
  if p.bag and p.bag.value >= Config.MaxBagValue then return say(src, 'حقيبتك ممتلئة.') end

  p.rob = { id = id, t = os.time() }
  cooldowns[id] = os.time() + h.cooldown
  heat[h.district] = (heat[h.district] or 0) + h.heat
  alertCops(h)
  TriggerClientEvent('bagcity:client:robStart', src, id, h.duration)
end)

RegisterNetEvent('bagcity:server:cancelRob', function()
  local src = source
  local p = P[src]
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
  p.bag = bag
  pushBag(src)
  say(src, ('أخذت الغنيمة! حقيبتك الحين %s (حرارة: %s). الشرطة في طريقها، اهرب وبيّضها عند التاجر.'):format(bag.serial, heatLabel(bag.heat)))
end)

-- ===== أوامر الحقيبة =====
RegisterCommand('bag', function(src)
  if src == 0 then return end
  local p = P[src]
  if not p or not p.bag then return say(src, 'ما معك حقيبة.') end
  say(src, ('الحقيبة %s | القيمة %d | الحرارة %s'):format(p.bag.serial, p.bag.value, heatLabel(p.bag.heat)))
end, false)

RegisterCommand('dropbag', function(src)
  if src == 0 or not Sec.rate(src, 'drop', 1000) then return end
  local p = P[src]
  if not p or not p.bag then return end
  local c = Sec.coords(src)
  if not c then return end
  addHist(p.bag, p.alias or 'مدني', 'رماها بالشارع', '')
  createDrop(c, p.bag)
  p.bag = nil
  pushBag(src)
  say(src, 'رميت الحقيبة. أي أحد يقدر يلتقطها.')
end, false)

RegisterCommand('passbag', function(src, args)
  if src == 0 or not Sec.rate(src, 'pass', 1000) then return end
  local p = P[src]
  local target = tonumber(args[1])
  if not p or not p.bag or not target or target == src or not GetPlayerName(target) then return end
  if not Sec.between(src, target, Config.HandRange) then return say(src, 'قرّب منه أكثر.') end
  local tp = getP(target)
  if tp.bag then return say(src, 'معه حقيبة أصلاً.') end
  p.bag.heat = math.min(100, p.bag.heat + Config.PassHeat)
  addHist(p.bag, p.alias or 'مدني', 'مرّرها إلى ' .. (tp.alias or 'مدني'), '')
  tp.bag, p.bag = p.bag, nil
  pushBag(src); pushBag(target)
  say(src, 'مرّرت الحقيبة.'); say(target, 'استلمت حقيبة! اكتب /bag لتشوف تفاصيلها.')
end, false)

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
  pushBag(src)
  say(src, ('التقطت حقيبة %s. دبّر نفسك!'):format(p.bag.serial))
end)

RegisterNetEvent('bagcity:server:requestSync', function()
  local src = source
  if not Sec.rate(src, 'sync', 3000) then return end
  local list = {}
  for id, d in pairs(drops) do list[#list + 1] = { id = id, x = d.c[1], y = d.c[2], z = d.c[3] } end
  TriggerClientEvent('bagcity:client:drops', src, list)
end)

-- ===== التبييض =====
RegisterNetEvent('bagcity:server:startLaunder', function(id)
  local src = source
  if type(id) ~= 'string' or not Sec.rate(src, 'lstart', 1500) then return end
  local l = launderById[id]
  if not l then return Sec.strike(src, 'bad launderer id') end
  local p = getP(src)
  if p.rob or p.laund or not p.bag then return end
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
  addHist(bag, p.alias or 'مدني', 'بيّضها عند ' .. l.label, '')
  archiveBag(bag)
  traces[Bridge.id(src)] = { serial = bag.serial, t = os.time() }
  p.bag = nil
  pushBag(src)
  Bridge.addCash(src, payout)
  say(src, ('بيّضت الحقيبة. التاجر أخذ %d%% وصلك %d. انتبه: الفلوس تحمل أثر الحقيبة لمدة نصف ساعة.'):format(math.floor(cut * 100), payout))
end)

-- ===== الشرطة =====
RegisterCommand('duty', function(src)
  if src == 0 or Bridge.fw ~= 'standalone' then return end
  if not IsPlayerAceAllowed(src, 'bagcity.cop') then return end
  copsDuty[src] = not copsDuty[src] or nil
  say(src, copsDuty[src] and 'دخلت الخدمة كشرطي.' or 'خرجت من الخدمة.')
end, false)

local function seize(copSrc, target)
  local tp = P[target]
  if tp and tp.bag then
    local bag = tp.bag
    local cut = math.floor(bag.value * Config.CopCut)
    addHist(bag, 'الشرطة', 'صادرتها (' .. Bridge.name(copSrc) .. ')', '')
    archiveBag(bag)
    Bridge.addCash(copSrc, cut)
    tp.bag = nil
    pushBag(target)
    say(copSrc, ('صادرت الحقيبة %s. حصتك %d. اكتب /evidence %s لقراءة سجلها.'):format(bag.serial, cut, bag.serial))
  else
    say(copSrc, 'ما كان معه حقيبة.')
  end
  if tp then
    tp.masked, tp.alias = false, nil
    TriggerClientEvent('bagcity:client:mask', target, false)
  end
  say(target, 'انقبض عليك وخسرت الحقيبة. معداتك بقت معك.')
end

RegisterCommand('arrest', function(src, args)
  if src == 0 or not Sec.rate(src, 'arrest', 1500) or not isCop(src) then return end
  local target = tonumber(args[1])
  if not target or target == src or not GetPlayerName(target) or pending[target] then return end
  if not Sec.between(src, target, Config.HandRange) then return say(src, 'قرّب منه أكثر.') end
  local tp = getP(target)
  if not tp.masked and not tp.bag then return say(src, 'ما عليه شبهة.') end
  local token = {}
  pending[target] = { cop = src, token = token }
  say(src, 'بدأ القبض. ينتظر قراره...')
  say(target, ('الشرطة تقبض عليك! خلال %d ثانية: /surrender تستسلم وتخسر الحقيبة فقط، أو /bribe [مبلغ] تعرض رشوة، أو اهرب.'):format(Config.ArrestWindow))
  SetTimeout(Config.ArrestWindow * 1000, function()
    local pd = pending[target]
    if not pd or pd.token ~= token then return end
    pending[target] = nil
    if Sec.between(src, target, Config.EscapeDist) then
      seize(src, target)
    else
      say(src, 'هرب منك.'); say(target, 'هربت!')
    end
  end)
end, false)

RegisterCommand('surrender', function(src)
  local pd = src ~= 0 and pending[src]
  if not pd then return end
  pending[src] = nil
  seize(pd.cop, src)
end, false)

RegisterCommand('bribe', function(src, args)
  local pd = src ~= 0 and pending[src]
  local amt = tonumber(args[1])
  if not pd or not amt then return end
  amt = math.floor(amt)
  if amt < Config.BribeMin or amt > Config.BribeMax then
    return say(src, ('الرشوة بين %d و %d'):format(Config.BribeMin, Config.BribeMax))
  end
  if Bridge.getBank(src) < amt then return say(src, 'رصيدك البنكي ما يكفي.') end
  pd.offer = amt
  say(src, 'عرضت الرشوة. انتظر قرار الشرطي.')
  say(pd.cop, ('المطلوب يعرض عليك رشوة %d. /acceptbribe أو /refusebribe'):format(amt))
end, false)

local function findPendingByCop(cop)
  for target, pd in pairs(pending) do
    if pd.cop == cop and pd.offer then return target, pd end
  end
end

RegisterCommand('acceptbribe', function(src)
  if src == 0 or not isCop(src) then return end
  local target, pd = findPendingByCop(src)
  if not target then return end
  if not Sec.between(src, target, Config.EscapeDist) then return end
  if not Bridge.removeBank(target, pd.offer) then return say(src, 'فشلت الرشوة.') end
  Bridge.addCash(src, pd.offer)
  local tp = P[target]
  ledger[#ledger + 1] = { t = os.time(), cop = Bridge.name(src), amount = pd.offer, serial = tp and tp.bag and tp.bag.serial or nil }
  if tp and tp.bag then addHist(tp.bag, 'شرطي فاسد', 'قبض رشوة وأطلقه', '') end
  pending[target] = nil
  say(src, 'قبلت الرشوة. الله يستر عليك...')
  say(target, 'الشرطي قبل الرشوة وتركك تمشي بالحقيبة.')
end, false)

RegisterCommand('refusebribe', function(src)
  if src == 0 or not isCop(src) then return end
  local target = findPendingByCop(src)
  if not target then return end
  pending[target] = nil
  seize(src, target)
end, false)

RegisterCommand('evidence', function(src, args)
  if src == 0 or not isCop(src) then return end
  local serial = args[1] and args[1]:upper()
  local bag = serial and archive[serial]
  if not bag then return say(src, 'ما لقيت دليل بهذا الرقم. الأرقام الأخيرة: ' .. table.concat(archiveOrder, ', ', math.max(1, #archiveOrder - 4), #archiveOrder)) end
  say(src, ('^*سجل الحقيبة %s (قيمة %d)\n%s'):format(bag.serial, bag.value, chainText(bag)))
end, false)

RegisterCommand('scan', function(src, args)
  if src == 0 or not isCop(src) or not Sec.rate(src, 'scan', 2000) then return end
  local target = tonumber(args[1])
  if not target or not GetPlayerName(target) or not Sec.between(src, target, Config.HandRange) then return end
  local tr = traces[Bridge.id(target)]
  if tr and os.time() - tr.t <= Config.TraceWindow then
    say(src, ('فلوسه عليها أثر الحقيبة %s قبل %d دقيقة. اكتب /evidence %s'):format(tr.serial, math.floor((os.time() - tr.t) / 60), tr.serial))
  else
    say(src, 'ما لقيت أثر.')
  end
end, false)

RegisterCommand('ledger', function(src)
  if src == 0 or not IsPlayerAceAllowed(src, 'bagcity.ia') then return end
  if #ledger == 0 then return say(src, 'السجل نظيف... حالياً.') end
  local lines = {}
  for i = math.max(1, #ledger - 9), #ledger do
    local e = ledger[i]
    lines[#lines + 1] = ('[%s] %s استلم %d %s'):format(os.date('%m/%d %H:%M', e.t), e.cop, e.amount, e.serial and ('(حقيبة ' .. e.serial .. ')') or '')
  end
  say(src, '^*سجل الرشاوى\n' .. table.concat(lines, '\n'))
end, false)

RegisterCommand('balance', function(src)
  if src ~= 0 and Bridge.fw == 'standalone' then say(src, 'رصيدك: ' .. Bridge.getBank(src)) end
end, false)

RegisterCommand('bc_here', function(src)
  if src == 0 or not IsPlayerAceAllowed(src, 'bagcity.dev') then return end
  local c = Sec.coords(src)
  if c then say(src, ('{%.1f, %.1f, %.1f}'):format(c[1], c[2], c[3])) end
end, false)

-- ===== التنظيف والحفظ =====
CreateThread(function()
  while true do
    Wait(60000)
    for d, v in pairs(heat) do heat[d] = math.max(0, v - Config.HeatDecay) end
  end
end)

CreateThread(function()
  while true do
    Wait(30000)
    local changed, now = false, os.time()
    for id, d in pairs(drops) do
      if d.exp <= now then drops[id] = nil; changed = true end
    end
    if changed then broadcastDrops() end
  end
end)

-- آخر موقع لمن يحمل حقيبة (حتى لو خرج فجأة تنزل حقيبته بمكانه)
CreateThread(function()
  while true do
    Wait(3000)
    for src, p in pairs(P) do
      if p.bag then p.last = Sec.coords(src) or p.last end
    end
  end
end)

local function save()
  SetResourceKvp('ledger', json.encode(ledger))
  local arc = {}
  for _, s in ipairs(archiveOrder) do arc[#arc + 1] = archive[s] end
  SetResourceKvp('archive', json.encode(arc))
end

CreateThread(function()
  local l = GetResourceKvpString('ledger')
  if l then ledger = json.decode(l) or {} end
  local a = GetResourceKvpString('archive')
  for _, bag in ipairs(a and json.decode(a) or {}) do archive[bag.serial] = bag; archiveOrder[#archiveOrder + 1] = bag.serial end
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
  P[src], pending[src], copsDuty[src] = nil, nil, nil
  for t, pd in pairs(pending) do if pd.cop == src then pending[t] = nil end end
  Sec.clear(src)
end)
