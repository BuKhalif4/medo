-- سكربت الحقيبة: القناع، السطو، الحقيبة وسجل حياتها، التبييض، التخبئة، وسقوط الحقيبة عند الموت.
local P = K.P
local clamp = K.clamp
local drops, dropSeq = {}, 0
local heat, cooldowns = {}, {}
local stashes = {}                 -- placeId -> { [identifier] = { bag, t } }
K.archive, K.archiveOrder, K.traces = {}, {}, {}

local function heatLabel(h) if h < 30 then return 'بارد' elseif h < 60 then return 'دافئ' else return 'ساخن' end end
K.heatLabel = heatLabel

-- السعة: من الحقيبة المجهّزة (market) وإلا الأساسية
K.capacityFn = function() return Config.BaseCapacity end
function K.capacity(src) return K.capacityFn(src) end

-- ===== الحقيبة وسجلها =====
function K.newBag() return { serial = ('BC-%06d'):format(math.random(0, 999999)), value = 0, heat = 0, history = {} } end
function K.addHist(bag, who, what, where) bag.history[#bag.history + 1] = { t = os.time(), who = who or '?', what = what, where = where or '' } end
function K.chainLines(bag)
  local lines = {}
  for _, e in ipairs(bag.history) do
    lines[#lines + 1] = ('[%s] %s - %s %s'):format(os.date('%H:%M', e.t), e.who, e.what, e.where ~= '' and ('(' .. e.where .. ')') or '')
  end
  return lines
end
function K.archiveBag(bag)
  K.archive[bag.serial] = bag
  K.archiveOrder[#K.archiveOrder + 1] = bag.serial
  if #K.archiveOrder > Config.ArchiveMax then K.archive[table.remove(K.archiveOrder, 1)] = nil end
end

-- ===== الحقائب المرمية =====
local function dropList()
  local l = {}
  for id, d in pairs(drops) do l[#l + 1] = { id = id, x = d.c[1], y = d.c[2], z = d.c[3] } end
  return l
end
local function broadcastDrops() TriggerClientEvent('mdk:client:drops', -1, dropList()) end
function K.createDrop(c, bag)
  dropSeq = dropSeq + 1
  drops[dropSeq] = { c = c, bag = bag, exp = os.time() + Config.DropTTL }
  broadcastDrops()
  return dropSeq
end

-- ===== بلاغات =====
local function alertCops(h)
  for _, idStr in ipairs(GetPlayers()) do
    local s = tonumber(idStr)
    if s and K.isCop(s) then
      local pt, f = K.spotPoint(h, 'main'), Config.AlertFuzz
      TriggerClientEvent('mdk:client:alert', s, pt[1] + (math.random() * 2 - 1) * f, pt[2] + (math.random() * 2 - 1) * f, pt[3], h.tag)
    end
  end
  if Config.News and h.heat >= Config.NewsMinHeat then K.banner('عاجل', ('بلاغ عن سطو مسلح على %s'):format(h.label), 'alert') end
end

-- ===== القناع والحقيبة =====
K.action('bag', 'mask', function(src)
  if not Sec.rate(src, 'mask', 1500) then return end
  local p = K.getP(src)
  p.masked = not p.masked
  p.alias = p.masked and ('قناع-%04d'):format(math.random(1000, 9999)) or nil
  K.say(src, p.masked and ('لبست القناع. اسمك للعصابات: ' .. p.alias) or 'شلت القناع. ما أحد يقدر يهاجمك.')
end)

K.action('bag', 'dropbag', function(src)
  if not Sec.rate(src, 'drop', 1000) then return end
  local p = P[src]
  if not p or not p.bag then return end
  local c = Sec.coords(src)
  if not c then return end
  K.addHist(p.bag, p.alias or 'مدني', 'رماها بالشارع', '')
  K.incident('drop', c, { bag = p.bag, serial = p.bag.serial, who = p.alias or 'مدني', heat = p.bag.heat })
  K.createDrop(c, p.bag)
  p.bag = nil
  K.say(src, 'رميت الحقيبة. أي أحد يقدر يلتقطها.')
end)

K.action('bag', 'passbag', function(src, args)
  if not Sec.rate(src, 'pass', 1000) then return end
  local p = P[src]
  local target = tonumber(args[1])
  if not p or not p.bag or not target or target == src or not GetPlayerName(target) then return end
  if not Sec.between(src, target, Config.HandRange) then return K.say(src, 'قرّب منه أكثر.') end
  local tp = K.getP(target)
  if tp.bag then return K.say(src, 'معه حقيبة أصلاً.') end
  p.bag.heat = math.min(100, p.bag.heat + Config.PassHeat)
  K.addHist(p.bag, p.alias or 'مدني', 'مرّرها إلى ' .. (tp.alias or 'مدني'), '')
  tp.bag, p.bag = p.bag, nil
  K.say(src, 'مرّرت الحقيبة.')
  K.say(target, 'استلمت حقيبة! افتح قائمة «الحقيبة» لتشوف تفاصيلها.', 'good')
end)

-- ===== السطو =====
RegisterNetEvent('mdk:server:startRob', function(id)
  local src = source
  if type(id) ~= 'string' or not Sec.rate(src, 'start', 1500) or not K.enabled('bag') then return end
  local h = K.spots[id]
  if not h or h.type ~= 'heist' then return Sec.strike(src, 'bad heist id') end
  local p = K.getP(src)
  if p.rob or p.laund or p.raid then return end
  if not p.masked then return K.say(src, 'لازم تلبس القناع أول.') end
  if (cooldowns[id] or 0) > os.time() then return K.say(src, 'المحل انسرق قبل شوي.') end
  if not K.nearSpot(src, h, 'main') then return Sec.strike(src, 'rob out of range') end
  if p.bag and p.bag.value >= K.capacity(src) then return K.say(src, 'حقيبتك ممتلئة. جهّز حقيبة أكبر.') end
  p.rob = { id = id, t = os.time(), inc = K.incident('heist', K.spotPoint(h, 'main'), { who = p.alias, label = h.label, heat = h.heat }) }
  cooldowns[id] = os.time() + h.cooldown
  heat[h.district] = (heat[h.district] or 0) + h.heat
  alertCops(h)
  TriggerClientEvent('mdk:client:robStart', src, id, h.duration)
end)

RegisterNetEvent('mdk:server:cancelRob', function()
  local p = P[source]
  if not p or not p.rob then return end
  cooldowns[p.rob.id] = os.time() + 30
  p.rob = nil
end)

RegisterNetEvent('mdk:server:finishRob', function()
  local src = source
  local p = P[src]
  if not p or not p.rob then return end
  local h = K.spots[p.rob.id]
  local elapsed, incId = os.time() - p.rob.t, p.rob.inc
  p.rob = nil
  if elapsed < h.duration - 1 then return Sec.strike(src, 'rob finished too early') end
  if not K.nearSpot(src, h, 'main', h.radius + 3.0) then
    cooldowns[h.id] = os.time() + 30
    return K.say(src, 'ابتعدت عن المحل، فشلت السرقة.')
  end
  local mult = 1 + clamp(heat[h.district] or 0, 0, 100) / 200
  local amount = math.floor(math.random(h.loot[1], h.loot[2]) * mult)
  local bag = p.bag or K.newBag()
  bag.value = math.min(K.capacity(src), bag.value + amount)
  bag.heat = math.min(100, bag.heat + h.heat)
  K.addHist(bag, p.alias, 'سرقها من', h.label)
  K.incidentBag(incId, bag)
  p.bag = bag
  K.say(src, ('أخذت الغنيمة! حقيبتك %s (حرارة: %s). الشرطة في طريقها، اهرب وبيّضها عند التاجر.'):format(bag.serial, heatLabel(bag.heat)), 'good')
end)
function K.incidentBag(id, bag) if K.hooks.incidentBag then K.hooks.incidentBag(id, bag) end end

-- ===== التقاط الحقائب =====
RegisterNetEvent('mdk:server:pickup', function(id)
  local src = source
  if type(id) ~= 'number' or not Sec.rate(src, 'pickup', 800) or not K.enabled('bag') then return end
  local d = drops[id]
  if not d then return end
  if not Sec.near(src, d.c, 3.0) then return Sec.strike(src, 'pickup out of range') end
  local p = K.getP(src)
  if p.bag then return K.say(src, 'معك حقيبة أصلاً.') end
  K.addHist(d.bag, p.alias or 'مدني', 'التقطها', '')
  d.bag.heat = math.min(100, d.bag.heat + Config.PassHeat)
  p.bag = d.bag
  drops[id] = nil
  broadcastDrops()
  K.say(src, ('التقطت حقيبة %s. دبّر نفسك!'):format(p.bag.serial), 'good')
end)

RegisterNetEvent('mdk:server:requestDrops', function()
  local src = source
  if Sec.rate(src, 'sync', 3000) then TriggerClientEvent('mdk:client:drops', src, dropList()) end
end)

-- الموت: السيرفر يتحقق من الصحة بنفسه
RegisterNetEvent('mdk:server:died', function()
  local src = source
  if not Sec.rate(src, 'died', 800) or not K.enabled('bag') then return end
  local p = P[src]
  if not p or not p.bag then return end
  local ped = GetPlayerPed(src)
  if not ped or ped == 0 or GetEntityHealth(ped) > 105 then return end
  local c = Sec.coords(src) or p.last
  if not c then return end
  K.addHist(p.bag, p.alias or 'مدني', 'سقطت منه (مات)', '')
  K.incident('drop', c, { bag = p.bag, serial = p.bag.serial, who = p.alias or 'مدني', heat = p.bag.heat })
  K.createDrop(c, p.bag)
  p.bag, p.rob, p.laund, p.raid = nil, nil, nil, nil
  K.say(src, 'متّ وسقطت منك الحقيبة.', 'alert')
end)

-- ===== التبييض =====
RegisterNetEvent('mdk:server:startLaunder', function(id)
  local src = source
  if type(id) ~= 'string' or not Sec.rate(src, 'lstart', 1500) or not K.enabled('bag') then return end
  local l = K.spots[id]
  if not l or l.type ~= 'launderer' then return Sec.strike(src, 'bad launderer id') end
  local p = K.getP(src)
  if p.rob or p.laund or p.raid or not p.bag then return end
  if not K.nearSpot(src, l, 'main') then return Sec.strike(src, 'launder out of range') end
  p.laund = { id = id, t = os.time() }
  TriggerClientEvent('mdk:client:launderStart', src, id, Config.LaunderTime)
end)

RegisterNetEvent('mdk:server:cancelLaunder', function() local p = P[source]; if p then p.laund = nil end end)

RegisterNetEvent('mdk:server:finishLaunder', function()
  local src = source
  local p = P[src]
  if not p or not p.laund or not p.bag then return end
  local l = K.spots[p.laund.id]
  local elapsed = os.time() - p.laund.t
  p.laund = nil
  if elapsed < Config.LaunderTime - 1 then return Sec.strike(src, 'launder finished too early') end
  if not K.nearSpot(src, l, 'main', l.radius + 3.0) then return K.say(src, 'ابتعدت، انلغى التبييض.') end
  local bag = p.bag
  local cut = clamp(Config.LaunderMinCut + bag.heat / 100 * (Config.LaunderMaxCut - Config.LaunderMinCut), Config.LaunderMinCut, Config.LaunderMaxCut)
  local payout = math.floor(bag.value * (1 - cut))
  if not K.addCash(src, payout) then return K.say(src, 'تعذّر الدفع الحين، حاول مرة ثانية.', 'alert') end   -- الفلوس أول، وبعدين نشيل الحقيبة
  K.addHist(bag, p.alias or 'مدني', 'بيّضها عند ' .. l.label, '')
  K.archiveBag(bag)
  K.traces[Bridge.id(src)] = { serial = bag.serial, t = os.time() }
  p.bag = nil
  K.say(src, ('بيّضت الحقيبة. التاجر أخذ %d%% وصلك %d. انتبه: الفلوس تحمل أثر الحقيبة نصف ساعة.'):format(math.floor(cut * 100), payout), 'good')
end)

-- ===== التخبئة (داخل الغرفة) =====
local function srcByIdentifier(id)
  for _, s in ipairs(GetPlayers()) do local n = tonumber(s); if n and Bridge.id(n) == id then return n end end
end
K.srcByIdentifier = srcByIdentifier
K.stashes = stashes

K.action('bag', 'stash', function(src, args)
  if not Sec.rate(src, 'stash', 1000) then return end
  local pl = K.spots[args[1] or '']
  if not pl or (pl.type ~= 'house' and pl.type ~= 'gang') then return Sec.strike(src, 'bad stash place') end
  if not K.nearSpot(src, pl, 'inside', 4.0) then return K.say(src, 'ادخل الغرفة أول (التخبئة من داخل المكان).') end
  local p, id = K.getP(src), Bridge.id(src)
  local s = stashes[pl.id]
  if not s then s = {}; stashes[pl.id] = s end
  if p.bag then
    if s[id] then return K.say(src, 'عندك حقيبة مخبأة هنا أصلاً.') end
    K.addHist(p.bag, p.alias or 'مدني', 'خبّأها في', pl.label)
    s[id] = { bag = p.bag, t = os.time() }
    p.bag = nil
    K.say(src, 'خبّأت الحقيبة. حرارتها بتبرد مع الوقت، بس انتبه من المداهمة!', 'good')
  else
    local e = s[id]
    if not e then return K.say(src, 'ما عندك حقيبة مخبأة هنا.') end
    K.addHist(e.bag, p.alias or 'مدني', 'استرجعها من', pl.label)
    p.bag, s[id] = e.bag, nil
    K.say(src, ('استرجعت حقيبتك %s (الحرارة: %s).'):format(p.bag.serial, heatLabel(p.bag.heat)), 'good')
  end
end)

-- ===== دورات =====
local function minuteTick()
  for d, v in pairs(heat) do heat[d] = math.max(0, v - Config.HeatDecay) end
  for placeId, s in pairs(stashes) do
    local pl = K.spots[placeId]
    local cool = (pl and pl.cool or 1) * Config.Stash.coolPerMinute
    for _, e in pairs(s) do e.bag.heat = math.max(0, e.bag.heat - cool) end
  end
end
local function dropCleanup()
  local changed, now = false, os.time()
  for id, d in pairs(drops) do if d.exp <= now then drops[id] = nil; changed = true end end
  if changed then broadcastDrops() end
end
local function trackHolders() for src, p in pairs(P) do if p.bag then p.last = Sec.coords(src) or p.last end end end
K.thread(60000, minuteTick)
K.thread(30000, dropCleanup)
K.thread(3000, trackHolders)

K.onPlayerDrop(function(src)
  local p = P[src]
  if p and p.bag and p.last then
    K.addHist(p.bag, p.alias or 'مدني', 'وقعت منه (خرج من اللعبة)', '')
    K.createDrop(p.last, p.bag)
  end
end)

-- ===== حفظ المخابئ والأرشيف =====
do
  local st = GetResourceKvpString('mdk_stashes')
  for placeId, s in pairs(st and json.decode(st) or {}) do stashes[placeId] = s end
  local a = GetResourceKvpString('mdk_archive')
  for _, bag in ipairs(a and json.decode(a) or {}) do K.archive[bag.serial] = bag; K.archiveOrder[#K.archiveOrder + 1] = bag.serial end
end
K.onSave(function()
  SetResourceKvp('mdk_stashes', json.encode(stashes))
  local arc = {}
  for _, s in ipairs(K.archiveOrder) do arc[#arc + 1] = K.archive[s] end
  SetResourceKvp('mdk_archive', json.encode(arc))
end)

-- ===== للواجهة =====
K.provide('bag', function(src)
  local p = P[src]
  local bag = p and p.bag
  return {
    masked = p and p.masked or false, alias = p and p.alias or nil, cap = K.capacity(src),
    bag = bag and { serial = bag.serial, value = bag.value, heat = bag.heat, label = heatLabel(bag.heat), history = K.chainLines(bag) } or nil,
  }
end)
K.statsOf.bag = function()
  local holding, value = 0, 0
  for _, p in pairs(P) do if p.bag then holding = holding + 1; value = value + p.bag.value end end
  local stashed = 0
  for _, s in pairs(stashes) do for _ in pairs(s) do stashed = stashed + 1 end end
  return { holding = holding, value = value, drops = #dropList(), stashed = stashed, archived = #K.archiveOrder }
end

if __MDK_TEST then __MT.bag = { minuteTick = minuteTick, dropCleanup = dropCleanup, drops = drops, stashes = stashes, heat = heat } end
