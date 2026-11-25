-- السوق السوداء + الشحنات (شجرة التقدم) + رتب الشرطة. السيرفر يقرر كل شيء: السعر، القفل، القرب، الملكية.

local Actions, say, pushState, isCop, getP = BC.Actions, BC.say, BC.pushState, BC.isCop, BC.getP
local nearBench, nearPlaceType = BC.nearBench, BC.nearPlaceType

local itemById, nodeById, rankById = {}, {}, {}
for _, w in ipairs(Config.Weapons) do w.cat = 'weapon'; itemById[w.id] = w end
for _, g in ipairs(Config.Gear) do itemById[g.id] = g end
for _, n in ipairs(Config.Tree) do nodeById[n.id] = n end
for _, r in ipairs(Config.PoliceRanks) do rankById[r.id] = r end

-- ===== تقدم اللاعب (يتحفظ بالـ KVP باسم هويته) =====
local cache = {}          -- src -> prog
local cooldown = {}       -- src -> { key = time }

local function key(src) return 'prog_' .. Bridge.id(src) end

local function getProg(src)
  local pr = cache[src]
  if pr then return pr end
  local raw = GetResourceKvpString(key(src))
  pr = raw and json.decode(raw) or nil
  if type(pr) ~= 'table' then pr = {} end
  pr.unlocked, pr.equip, pr.mods = pr.unlocked or {}, pr.equip or {}, pr.mods or {}
  pr.xp = pr.xp or 0
  cache[src] = pr
  return pr
end

local function save(src)
  if cache[src] then SetResourceKvp(key(src), json.encode(cache[src])) end
end

local function isUnlocked(pr, nodeId)
  local n = nodeById[nodeId]
  return n ~= nil and (n.root == true or pr.unlocked[nodeId] == true)
end

local function wait(src, k, seconds)
  cooldown[src] = cooldown[src] or {}
  local left = (cooldown[src][k] or 0) - os.time()
  if left > 0 then return left end
  cooldown[src][k] = os.time() + seconds
  return 0
end

local function give(src, weapon, ammo, comps) TriggerClientEvent('bagcity:client:gear', src, { weapon = weapon, ammo = ammo, comps = comps or {} }) end

-- ===== واجهة للسيرفر الرئيسي =====
function BC.capacity(src)
  local b = getProg(src).equip.bag
  local item = b and itemById[b]
  return item and item.capacity or Config.BaseCapacity
end

function BC.progState(src)
  local pr = getProg(src)
  local unlocked = {}
  for id in pairs(pr.unlocked) do unlocked[#unlocked + 1] = id end
  local bag = pr.equip.bag and itemById[pr.equip.bag]
  return {
    unlocked = unlocked, equip = pr.equip, mods = pr.mods, xp = pr.xp, rank = pr.rank,
    squares = Config.BaseSquares + (bag and bag.squares or 0),
  }
end

function BC.addXP(src, n)
  local pr = getProg(src)
  local before = pr.rank
  pr.xp = pr.xp + n
  save(src)
  for _, r in ipairs(Config.PoliceRanks) do
    if pr.xp >= r.xp and pr.xp - n < r.xp and r.xp > 0 then say(src, ('ترقية! صرت مؤهلاً لرتبة %s.'):format(r.label), 'good') end
  end
end

-- ===== الإجراءات =====
local function slotOf(item)
  if item.cat == 'weapon' then return item.slot or 'primary' end
  return item.cat   -- armor | bag
end

function Actions.m_buy(src, args)
  if not Sec.rate(src, 'market', 700) then return end
  local item = itemById[args[1] or '']
  if not item then return Sec.strike(src, 'bad market item') end
  if not nearBench(src) then return say(src, 'لازم تكون عند طاولة الأدوات في مقر عصابة.') end
  local pr = getProg(src)
  if item.node and not isUnlocked(pr, item.node) then return say(src, 'مقفل. افتحه أول من «الشحنات».') end
  local slot = slotOf(item)
  if pr.equip[slot] == item.id then return say(src, 'مجهّز أصلاً.') end
  if Bridge.getCash(src) < item.price then return say(src, ('تحتاج %d كاش.'):format(item.price)) end
  if item.limitSeconds then
    local left = wait(src, 'item_' .. item.id, item.limitSeconds)
    if left > 0 then return say(src, ('ما تقدر تشتري %s الحين. انتظر %d دقيقة.'):format(item.label, math.ceil(left / 60))) end
  end
  if not Bridge.removeCash(src, item.price) then return say(src, 'فشل الدفع.', 'alert') end
  pr.equip[slot] = item.id
  if item.cat == 'weapon' then give(src, item.weapon, item.ammo, {})
  elseif item.cat == 'armor' then TriggerClientEvent('bagcity:client:gear', src, { armor = item.armor }) end
  save(src)
  say(src, ('اشتريت %s وجهّزتها.'):format(item.label), 'good')
end

function Actions.m_unlock(src, args)
  if not Sec.rate(src, 'market', 700) then return end
  local node = nodeById[args[1] or '']
  if not node then return Sec.strike(src, 'bad tree node') end
  if not nearBench(src) then return say(src, 'الشحنات تُفتح من طاولة الأدوات في مقر عصابة.') end
  local pr = getProg(src)
  if isUnlocked(pr, node.id) then return say(src, 'مفتوحة أصلاً.') end
  for _, req in ipairs(node.requires or {}) do
    if not isUnlocked(pr, req) then return say(src, ('افتح «%s» أول.'):format(nodeById[req].label)) end
  end
  if Bridge.getCash(src) < node.cost then return say(src, ('تحتاج %d كاش.'):format(node.cost)) end
  if not Bridge.removeCash(src, node.cost) then return say(src, 'فشل الدفع.', 'alert') end
  pr.unlocked[node.id] = true
  save(src)
  say(src, ('فتحت شحنة «%s».'):format(node.label), 'good')
end

local function equippedWeapon(pr, id)
  local w = itemById[id or '']
  if w and w.cat == 'weapon' and pr.equip[slotOf(w)] == w.id then return w end
end

function Actions.m_ammo(src, args)
  if not Sec.rate(src, 'market', 700) then return end
  local pr = getProg(src)
  local w = equippedWeapon(pr, args[1])
  if not w then return say(src, 'جهّز السلاح أول.') end
  if not nearBench(src) then return say(src, 'لازم تكون عند طاولة الأدوات في مقر عصابة.') end
  local qty = math.floor(tonumber(args[2]) or 0)
  if qty < 1 or qty > 360 then return Sec.strike(src, 'bad ammo qty') end
  local total = qty * math.max(1, math.floor(w.price * Config.RefillRate / w.ammo))
  if Bridge.getCash(src) < total then return say(src, ('تحتاج %d كاش.'):format(total)) end
  if not Bridge.removeCash(src, total) then return say(src, 'فشل الدفع.', 'alert') end
  give(src, w.weapon, qty, {})
  say(src, ('اشتريت %d طلقة (%d).'):format(qty, total), 'good')
end

function Actions.m_mod(src, args)
  if not Sec.rate(src, 'market', 700) then return end
  local pr = getProg(src)
  local w = equippedWeapon(pr, args[1])
  if not w then return say(src, 'جهّز السلاح أول.') end
  if not nearBench(src) then return say(src, 'لازم تكون عند طاولة الأدوات في مقر عصابة.') end
  local mod
  for _, m in ipairs(w.mods or {}) do if m.id == args[2] then mod = m end end
  if not mod then return Sec.strike(src, 'bad mod id') end
  pr.mods[w.id] = pr.mods[w.id] or {}
  if pr.mods[w.id][mod.id] then return say(src, 'مركّب أصلاً.') end
  if Bridge.getCash(src) < mod.price then return say(src, ('تحتاج %d كاش.'):format(mod.price)) end
  if not Bridge.removeCash(src, mod.price) then return say(src, 'فشل الدفع.', 'alert') end
  pr.mods[w.id][mod.id] = true
  give(src, w.weapon, 0, { mod.comp })
  save(src)
  say(src, ('ركّبت %s على %s.'):format(mod.label, w.label), 'good')
end

-- بعد ما تدخل السيرفر من جديد: يرجّع معداتك المجهّزة (نصف الذخيرة الأساسية فقط، مرة كل 10 دقائق)
function Actions.m_restore(src)
  if not Sec.rate(src, 'market', 700) then return end
  local left = wait(src, 'restore', 600)
  if left > 0 then return say(src, ('انتظر %d دقيقة.'):format(math.ceil(left / 60))) end
  local pr = getProg(src)
  for _, slot in ipairs({ 'primary', 'secondary' }) do
    local w = itemById[pr.equip[slot] or '']
    if w and w.cat == 'weapon' then
      local comps = {}
      for modId in pairs(pr.mods[w.id] or {}) do
        for _, m in ipairs(w.mods or {}) do if m.id == modId then comps[#comps + 1] = m.comp end end
      end
      give(src, w.weapon, math.floor(w.ammo / 2), comps)
    end
  end
  local armor = pr.equip.armor and itemById[pr.equip.armor]
  if armor then TriggerClientEvent('bagcity:client:gear', src, { armor = armor.armor }) end
  say(src, 'رجّعت معداتك المجهّزة.', 'good')
end

-- ===== رتب الشرطة =====
function Actions.rank_select(src, args)
  if not Sec.rate(src, 'market', 700) or not isCop(src) then return end
  local rank = rankById[args[1] or '']
  if not rank then return Sec.strike(src, 'bad rank id') end
  if not nearPlaceType(src, 'police') then return say(src, 'اختيار المعدات من مركز الشرطة فقط.') end
  local pr = getProg(src)
  if pr.xp < rank.xp then return say(src, ('هذي الرتبة تحتاج %d نقطة (عندك %d).'):format(rank.xp, pr.xp)) end
  local left = wait(src, 'kit', Config.RankKitCooldown)
  if left > 0 then return say(src, ('استلمت معداتك قبل شوي. انتظر %d ثانية.'):format(left)) end
  for _, k in ipairs(rank.kit) do give(src, k.weapon, k.ammo, {}) end
  TriggerClientEvent('bagcity:client:gear', src, { armor = rank.armor })
  pr.rank = rank.id
  save(src)
  say(src, ('استلمت معدات رتبة %s.'):format(rank.label), 'good')
end

AddEventHandler('playerDropped', function()
  local src = source
  save(src)
  cache[src], cooldown[src] = nil, nil
end)

if __BAGCITY_TEST then __BT.cache = cache end
