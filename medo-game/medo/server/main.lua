-- ميدو: السيرفر هو الحاكم. العميل (واجهة + لعبة) يطلب فقط، والسيرفر يتحقق ويقرر.
MD = {}                       -- جسر بين ملفات السيرفر
local P = {}                  -- حالة كل لاعب (مؤقتة)
local cache = {}              -- التقدم المحفوظ (KVP)
local Actions = {}
local overrides = {}          -- نقاط حددها المطوّر
do local saved = GetResourceKvpString('points'); if saved then overrides = json.decode(saved) or {} end end

local spotById, lootById, weaponById, itemById, nodeById, rankById = {}, {}, {}, {}, {}, {}
for _, s in ipairs(Config.Spots) do spotById[s.id] = s end
for _, l in ipairs(Config.Loot) do lootById[l.id] = l end
for _, w in ipairs(Config.Weapons) do w.cat = 'weapon'; weaponById[w.id] = w; itemById[w.id] = w end
for _, g in ipairs(Config.Gear) do itemById[g.id] = g end
for _, n in ipairs(Config.Tree) do nodeById[n.id] = n end
for _, r in ipairs(Config.PoliceRanks) do rankById[r.id] = r end
MD.spotById, MD.lootById, MD.weaponById, MD.itemById, MD.nodeById, MD.rankById = spotById, lootById, weaponById, itemById, nodeById, rankById

-- ===== أدوات =====
local function clamp(v, a, b) return math.max(a, math.min(b, v)) end

function MD.point(spot)
  local o = overrides[spot.id]
  if o and o.main then return { o.main[1], o.main[2], o.main[3] } end
  return spot.coords
end
function MD.clerkPoint(spot)
  local o = overrides[spot.id]
  if o and o.clerk then return { o.clerk[1], o.clerk[2], o.clerk[3], (spot.clerk and spot.clerk[4]) or 0.0 } end
  return spot.clerk
end

local function getP(src)
  local p = P[src]
  if not p then p = { role = 'civilian', masked = false, crim = false, streak = 0 }; P[src] = p end
  return p
end
MD.getP, MD.P = getP, P

-- ===== المواسم (المسح الأسبوعي) =====
local season = { no = tonumber(GetResourceKvpString('season_no')) or 1, start = tonumber(GetResourceKvpString('season_start')) or os.time() }
if not GetResourceKvpString('season_start') then SetResourceKvp('season_start', tostring(season.start)); SetResourceKvp('season_no', tostring(season.no)) end
MD.season = season

local function key(src) return 'prog_' .. Bridge.id(src) end

-- المسح: الثروة المتبقية تتحول إلى سلطة ووقت سجن (دائمين)، والباقي يتصفّر
local function wipeProg(src, pr)
  local value = Bridge.getCash(src)
  for slot, id in pairs(pr.equip) do local it = MD.itemById[id]; if it then value = value + (it.price or 0) end end
  for _, it in ipairs(pr.inv) do local d = MD.lootById[it.id]; if d then value = value + d.value end end
  for _, id in ipairs(pr.stash) do local d = MD.lootById[id]; if d then value = value + d.value end end
  pr.authority = (pr.authority or 0) + math.floor(value / Config.Wipe.valuePerAuthority)
  pr.jail = (pr.jail or 0) + math.floor(value / Config.Wipe.jailSecondsPer) * 10
  Bridge.removeCash(src, Bridge.getCash(src))
  pr.unlocked, pr.equip, pr.mods, pr.inv, pr.stash = {}, {}, {}, {}, {}
  pr.season = season.no
  return value
end
function MD.prog(src)
  local pr = cache[src]
  if pr then return pr end
  local raw = GetResourceKvpString(key(src))
  pr = raw and json.decode(raw) or nil
  if type(pr) ~= 'table' then pr = {} end
  pr.unlocked, pr.equip, pr.mods, pr.inv = pr.unlocked or {}, pr.equip or {}, pr.mods or {}, pr.inv or {}
  pr.xp, pr.jail, pr.stash, pr.authority = pr.xp or 0, pr.jail or 0, pr.stash or {}, pr.authority or 0
  cache[src] = pr
  if (pr.season or season.no) ~= season.no then wipeProg(src, pr); MD.save(src) end   -- غارة فاتتك وأنت برا اللعبة
  pr.season = season.no
  return pr
end
function MD.save(src) if cache[src] then SetResourceKvp(key(src), json.encode(cache[src])) end end

function MD.nearSpot(src, spot, extra)
  return Sec.near(src, MD.point(spot), (spot.radius or 3.0) + (extra or 2.0))
end
function MD.nearType(src, stype, extra)
  for _, s in ipairs(Config.Spots) do
    if s.type == stype and MD.nearSpot(src, s, extra) then return s end
  end
end

local function inCity(src)
  local c = Sec.coords(src)
  if not c then return true end
  local dx, dy = c[1] - Config.City.center[1], c[2] - Config.City.center[2]
  return dx * dx + dy * dy <= Config.City.radius * Config.City.radius
end
MD.inCity = inCity

-- ===== الحقيبة (شبكة الخانات) =====
local W = Config.GridWidth
function MD.squares(src)
  local pr = MD.prog(src)
  local bag = pr.equip.bag and itemById[pr.equip.bag]
  return Config.BaseSquares + (bag and bag.squares or 0)
end

local function occupancy(items)
  local occ = {}
  for _, it in ipairs(items) do
    local d = lootById[it.id]
    if d then for dy = 0, d.size[2] - 1 do for dx = 0, d.size[1] - 1 do occ[(it.y + dy) * W + it.x + dx] = true end end end
  end
  return occ
end

local function findSpot(items, n, def)
  local occ, rows = occupancy(items), math.ceil(n / W)
  for y = 0, rows - 1 do
    for x = 0, W - 1 do
      local ok = true
      for dy = 0, def.size[2] - 1 do
        for dx = 0, def.size[1] - 1 do
          local idx = (y + dy) * W + x + dx
          if x + dx >= W or idx >= n or occ[idx] then ok = false end
        end
      end
      if ok then return x, y end
    end
  end
end

function MD.addLoot(src, id)
  local def = lootById[id]
  if not def then return false end
  local pr = MD.prog(src)
  local x, y = findSpot(pr.inv, MD.squares(src), def)
  if not x then return false end
  pr.inv[#pr.inv + 1] = { id = id, x = x, y = y }
  return true
end
function MD.lootValue(src)
  local v = 0
  for _, it in ipairs(MD.prog(src).inv) do v = v + (lootById[it.id] and lootById[it.id].value or 0) end
  return v
end
function MD.takeAllLoot(src)
  local pr = MD.prog(src)
  local items = pr.inv
  pr.inv = {}
  return items
end
function MD.notoriety(src) return clamp(getP(src).streak, 0, Config.MaxNotoriety) end

-- ===== الحالة للواجهة =====
local function pushState(src)
  local p, pr = getP(src), MD.prog(src)
  local bag = pr.equip.bag and itemById[pr.equip.bag]
  local unlocked = {}
  for id in pairs(pr.unlocked) do unlocked[#unlocked + 1] = id end
  local party = MD.partyOf and MD.partyOf(src) or nil
  TriggerClientEvent('medo:client:state', src, {
    id = src, name = Bridge.name(src), role = p.role, masked = p.masked, crim = p.crim,
    notoriety = MD.notoriety(src), inv = pr.inv, squares = MD.squares(src), width = W,
    lootValue = MD.lootValue(src), cash = Bridge.getCash(src),
    equip = pr.equip, mods = pr.mods, unlocked = unlocked, xp = pr.xp, rank = pr.rank, jail = pr.jail, authority = pr.authority, season = season.no,
    dev = IsPlayerAceAllowed(src, 'medo.dev'), party = party,
    stashCount = #pr.stash, jailLeft = p.jailUntil and math.max(0, p.jailUntil - os.time()) or 0,
  })
end
local function say(src, msg, kind)
  TriggerClientEvent('medo:client:notify', src, msg, kind or 'info')
  pushState(src)
end
local function banner(title, text, kind) TriggerClientEvent('medo:client:banner', -1, title, text, kind or 'news') end
MD.pushState, MD.say, MD.banner, MD.Actions, MD.clamp = pushState, say, banner, Actions, clamp

-- حالة الدور تنتشر لكل اللاعبين عبر statebag (الشرطة ترى من هو مجرم)
function MD.setRole(src, role, crim)
  local p = getP(src)
  p.role, p.crim = role, crim or false
  local pl = Player and Player(src)
  if pl and pl.state then pl.state:set('medo_role', role, true); pl.state:set('medo_crim', p.crim, true) end
  TriggerClientEvent('medo:client:role', src, role)
end

-- ===== القناع والوسم الجنائي =====
function Actions.mask(src)
  if not Sec.rate(src, 'mask', 1500) then return end
  local p = getP(src)
  if p.role == 'police' then return say(src, 'الشرطي ما يلبس قناع.') end
  if p.jailUntil then return end
  p.masked = not p.masked
  if p.masked then MD.setRole(src, 'criminal', true) end   -- القناع يخليك مجرماً
  say(src, p.masked and 'لبست القناع. صرت مجرماً: الشرطة واللاعبين الثانين يقدرون يهاجمونك، وما يزول إلا لما تطلع من المدينة.' or 'شلت القناع، لكن وسمك الجنائي باقي حتى تطلع من المدينة.')
end

-- ===== النقاط (تحديد من القائمة) =====
function Actions.setpoint(src, args)
  if not IsPlayerAceAllowed(src, 'medo.dev') then return end
  local spot, kind = spotById[args[1] or ''], args[2]
  if not spot or not Sec.rate(src, 'setpoint', 1000) then return end
  if kind ~= 'main' and not (kind == 'clerk' and spot.clerk) then return end
  local c = Sec.coords(src)
  if not c then return end
  overrides[spot.id] = overrides[spot.id] or {}
  overrides[spot.id][kind] = { c[1], c[2], c[3] }
  SetResourceKvp('points', json.encode(overrides))
  TriggerClientEvent('medo:client:points', -1, overrides)
  say(src, ('تم حفظ النقطة لـ %s.'):format(spot.label), 'good')
end
function Actions.tp(src, args)
  if not IsPlayerAceAllowed(src, 'medo.dev') then return end
  local spot = spotById[args[1] or '']
  if not spot then return end
  local pt = MD.point(spot)
  TriggerClientEvent('medo:client:tp', src, pt[1], pt[2], pt[3] + 1.0)
end
function Actions.give(src, args)   -- تجربة: كاش
  if not IsPlayerAceAllowed(src, 'medo.dev') then return end
  Bridge.addCash(src, tonumber(args[1]) or 100000)
  say(src, 'تمت إضافة كاش (وضع التجربة).', 'good')
end

-- ===== بوابة الإجراءات الوحيدة =====
RegisterNetEvent('medo:server:action', function(name, args)
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

RegisterNetEvent('medo:server:state', function()
  local src = source
  if Sec.rate(src, 'state', 500) then
    pushState(src)
    TriggerClientEvent('medo:client:points', src, overrides)
  end
end)

-- ===== الدورات =====
local zoneOut = {}
local function zoneTick()
  local z = Config.Zone
  for _, idStr in ipairs(GetPlayers()) do
    local src = tonumber(idStr)
    local c = src and Sec.coords(src)
    if c and not (math.abs(c[1]) < 1 and math.abs(c[2]) < 1) then
      local dx, dy = c[1] - z.center[1], c[2] - z.center[2]
      if dx * dx + dy * dy > z.radius * z.radius then
        zoneOut[src] = (zoneOut[src] or 0) + 3
        local left = z.warnSeconds - zoneOut[src]
        if left <= 0 then
          zoneOut[src] = nil
          TriggerClientEvent('medo:client:zone', src, 0)
          TriggerClientEvent('medo:client:tp', src, z.safe[1], z.safe[2], z.safe[3])
          say(src, 'تجاوزت حدود اللعب، رجعناك.', 'alert')
        else TriggerClientEvent('medo:client:zone', src, left) end
      elseif zoneOut[src] then zoneOut[src] = nil; TriggerClientEvent('medo:client:zone', src, 0) end
      -- الوسم الجنائي يزول لما تطلع من المدينة
      local p = P[src]
      if p and p.crim and p.role == 'criminal' and not inCity(src) then
        p.masked, p.streak = false, 0
        MD.setRole(src, 'civilian', false)
        say(src, 'طلعت من المدينة، زال وسمك الجنائي.', 'good')
      end
    end
  end
end
MD.zoneTick = zoneTick

local function wipeTick()
  if os.time() - season.start < Config.Wipe.days * 86400 then return false end
  season.no, season.start = season.no + 1, os.time()
  SetResourceKvp('season_no', tostring(season.no)); SetResourceKvp('season_start', tostring(season.start))
  MD.banner('الغارة الأسبوعية', 'تم مسح الكاش والمعدات. الثروة المتبقية تحولت إلى سلطة ووقت سجن.', 'alert')
  for _, idStr in ipairs(GetPlayers()) do
    local src = tonumber(idStr)
    if src then
      local pr = MD.prog(src)
      if pr.season ~= season.no then wipeProg(src, pr); MD.save(src) end   -- (لو تحمّل جديد يكون انمسح وقت التحميل)
      TriggerClientEvent('medo:client:wipe', src)
      pushState(src)
    end
  end
  return true
end
MD.wipeTick = wipeTick
CreateThread(function() while true do Wait(60000); wipeTick() end end)
CreateThread(function() while true do Wait(3000); zoneTick() end end)

AddEventHandler('onResourceStop', function(res)
  if res == GetCurrentResourceName() then for src in pairs(cache) do MD.save(src) end end
end)

AddEventHandler('playerDropped', function()
  local src = source
  if MD.onDrop then MD.onDrop(src) end
  MD.save(src)
  P[src], cache[src], zoneOut[src] = nil, nil, nil
  Sec.clear(src)
end)

if __MEDO_TEST then __MT = { P = P, cache = cache, overrides = overrides, wipeTick = wipeTick, season = season } end
