-- سكربت السوق والشحنات: السوق السوداء، شجرة الشحنات، التجهيز (سلاح، درع، حقيبة)، الذخيرة والتعديلات.
local itemById, nodeById, weaponById = {}, {}, {}
for _, w in ipairs(Config.Weapons) do w.cat = 'weapon'; itemById[w.id] = w; weaponById[w.id] = w end
for _, g in ipairs(Config.Gear) do itemById[g.id] = g end
for _, n in ipairs(Config.Tree) do nodeById[n.id] = n end

local function pr(src)
  local st = K.store('market', src)
  st.unlocked, st.equip, st.mods = st.unlocked or {}, st.equip or {}, st.mods or {}
  return st
end
local cd = {}
local function wait(src, k, seconds)
  cd[src] = cd[src] or {}
  local left = (cd[src][k] or 0) - os.time()
  if left > 0 then return left end
  cd[src][k] = os.time() + seconds
  return 0
end
K.onPlayerDrop(function(src) cd[src] = nil end)

local function give(src, weapon, ammo, comps) TriggerClientEvent('mdk:client:gear', src, { weapon = weapon, ammo = ammo, comps = comps or {} }) end
local function isUnlocked(st, id) local n = nodeById[id]; return n ~= nil and (n.root == true or st.unlocked[id] == true) end
local function slotOf(item) if item.cat == 'weapon' then return item.slot or 'primary' end; return item.cat end
local function nearBench(src)
  for _, pl in ipairs(Config.Places) do
    local pt = pl.type == 'gang' and K.spotPoint(pl, 'bench')
    if pt and Sec.near(src, pt, 4.0) then return pl end
  end
end

-- سعة الحقيبة المسروقة من الحقيبة المجهّزة
K.capacityFn = function(src)
  local b = pr(src).equip.bag
  local item = b and itemById[b]
  return item and item.capacity or Config.BaseCapacity
end

K.action('market', 'm_buy', function(src, args)
  if not Sec.rate(src, 'market', 700) then return end
  local item = itemById[args[1] or '']
  if not item then return Sec.strike(src, 'bad market item') end
  if not nearBench(src) then return K.say(src, 'لازم تكون عند طاولة الأدوات في مقر عصابة.') end
  local st = pr(src)
  if item.node and not isUnlocked(st, item.node) then return K.say(src, 'مقفل. افتحه أول من «الشحنات».') end
  local slot = slotOf(item)
  if st.equip[slot] == item.id then return K.say(src, 'مجهّز أصلاً.') end
  if K.getCash(src) < item.price then return K.say(src, ('تحتاج %d كاش.'):format(item.price)) end
  if item.limitSeconds then
    local left = wait(src, 'item_' .. item.id, item.limitSeconds)
    if left > 0 then return K.say(src, ('ما تقدر تشتري %s الحين. انتظر %d دقيقة.'):format(item.label, math.ceil(left / 60))) end
  end
  if not K.removeCash(src, item.price) then return K.say(src, 'فشل الدفع.', 'alert') end
  st.equip[slot] = item.id
  if item.cat == 'weapon' then give(src, item.weapon, item.ammo, {})
  elseif item.cat == 'armor' then TriggerClientEvent('mdk:client:gear', src, { armor = item.armor }) end
  K.saveStore('market', src)
  K.say(src, ('اشتريت %s وجهّزتها.'):format(item.label), 'good')
end)

K.action('market', 'm_unlock', function(src, args)
  if not Sec.rate(src, 'market', 700) then return end
  local node = nodeById[args[1] or '']
  if not node then return Sec.strike(src, 'bad tree node') end
  if not nearBench(src) then return K.say(src, 'الشحنات تُفتح من طاولة الأدوات في مقر عصابة.') end
  local st = pr(src)
  if isUnlocked(st, node.id) then return K.say(src, 'مفتوحة أصلاً.') end
  for _, req in ipairs(node.requires or {}) do
    if not isUnlocked(st, req) then return K.say(src, ('افتح «%s» أول.'):format(nodeById[req].label)) end
  end
  if K.getCash(src) < node.cost then return K.say(src, ('تحتاج %d كاش.'):format(node.cost)) end
  if not K.removeCash(src, node.cost) then return K.say(src, 'فشل الدفع.', 'alert') end
  st.unlocked[node.id] = true
  K.saveStore('market', src)
  K.say(src, ('فتحت شحنة «%s».'):format(node.label), 'good')
end)

local function equippedWeapon(st, id)
  local w = weaponById[id or '']
  if w and st.equip[slotOf(w)] == w.id then return w end
end

K.action('market', 'm_ammo', function(src, args)
  if not Sec.rate(src, 'market', 700) then return end
  local st = pr(src)
  local w = equippedWeapon(st, args[1])
  if not w then return K.say(src, 'جهّز السلاح أول.') end
  if not nearBench(src) then return K.say(src, 'لازم تكون عند طاولة الأدوات في مقر عصابة.') end
  local qty = math.floor(tonumber(args[2]) or 0)
  if qty < 1 or qty > 360 then return Sec.strike(src, 'bad ammo qty') end
  local total = qty * math.max(1, math.floor(w.price * Config.RefillRate / w.ammo))
  if K.getCash(src) < total then return K.say(src, ('تحتاج %d كاش.'):format(total)) end
  if not K.removeCash(src, total) then return K.say(src, 'فشل الدفع.', 'alert') end
  give(src, w.weapon, qty, {})
  K.say(src, ('اشتريت %d طلقة (%d).'):format(qty, total), 'good')
end)

K.action('market', 'm_mod', function(src, args)
  if not Sec.rate(src, 'market', 700) then return end
  local st = pr(src)
  local w = equippedWeapon(st, args[1])
  if not w then return K.say(src, 'جهّز السلاح أول.') end
  if not nearBench(src) then return K.say(src, 'لازم تكون عند طاولة الأدوات في مقر عصابة.') end
  local mod
  for _, m in ipairs(w.mods or {}) do if m.id == args[2] then mod = m end end
  if not mod then return Sec.strike(src, 'bad mod id') end
  st.mods[w.id] = st.mods[w.id] or {}
  if st.mods[w.id][mod.id] then return K.say(src, 'مركّب أصلاً.') end
  if K.getCash(src) < mod.price then return K.say(src, ('تحتاج %d كاش.'):format(mod.price)) end
  if not K.removeCash(src, mod.price) then return K.say(src, 'فشل الدفع.', 'alert') end
  st.mods[w.id][mod.id] = true
  give(src, w.weapon, 0, { mod.comp })
  K.saveStore('market', src)
  K.say(src, ('ركّبت %s على %s.'):format(mod.label, w.label), 'good')
end)

K.action('market', 'm_restore', function(src)
  if not Sec.rate(src, 'market', 700) then return end
  local left = wait(src, 'restore', 600)
  if left > 0 then return K.say(src, ('انتظر %d دقيقة.'):format(math.ceil(left / 60))) end
  local st = pr(src)
  for _, slot in ipairs({ 'primary', 'secondary' }) do
    local w = weaponById[st.equip[slot] or '']
    if w then
      local comps = {}
      for modId in pairs(st.mods[w.id] or {}) do for _, m in ipairs(w.mods or {}) do if m.id == modId then comps[#comps + 1] = m.comp end end end
      give(src, w.weapon, math.floor(w.ammo / 2), comps)
    end
  end
  local armor = st.equip.armor and itemById[st.equip.armor]
  if armor then TriggerClientEvent('mdk:client:gear', src, { armor = armor.armor }) end
  K.say(src, 'رجّعت معداتك المجهّزة.', 'good')
end)

K.provide('market', function(src)
  local st = pr(src)
  local unlocked = {}
  for id in pairs(st.unlocked) do unlocked[#unlocked + 1] = id end
  local bag = st.equip.bag and itemById[st.equip.bag]
  return { equip = st.equip, mods = st.mods, unlocked = unlocked, squares = Config.BaseSquares + (bag and bag.squares or 0), cap = K.capacity(src) }
end)
K.statsOf.market = function() return { items = #Config.Weapons + #Config.Gear, nodes = #Config.Tree } end
