-- السوق السوداء + الشحنات (شجرة التقدم). السيرفر يقرر: السعر، القفل، القرب، الملكية.
local Actions, say = MD.Actions, MD.say
local itemById, nodeById, weaponById = MD.itemById, MD.nodeById, MD.weaponById
local cooldown = {}

local function wait(src, k, seconds)
  cooldown[src] = cooldown[src] or {}
  local left = (cooldown[src][k] or 0) - os.time()
  if left > 0 then return left end
  cooldown[src][k] = os.time() + seconds
  return 0
end
local function give(src, weapon, ammo, comps) TriggerClientEvent('medo:client:gear', src, { weapon = weapon, ammo = ammo, comps = comps or {} }) end
local function isUnlocked(pr, nodeId) local n = nodeById[nodeId]; return n ~= nil and (n.root == true or pr.unlocked[nodeId] == true) end
local function slotOf(item) if item.cat == 'weapon' then return item.slot or 'primary' end; return item.cat end
local function atDealer(src) return MD.nearType(src, 'dealer') ~= nil end

function Actions.m_buy(src, args)
  if not Sec.rate(src, 'market', 700) then return end
  local item = itemById[args[1] or '']
  if not item then return Sec.strike(src, 'bad market item') end
  if not atDealer(src) then return say(src, 'لازم تكون عند تاجر السوق السوداء.') end
  local pr = MD.prog(src)
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
  elseif item.cat == 'armor' then TriggerClientEvent('medo:client:gear', src, { armor = item.armor }) end
  MD.save(src)
  say(src, ('اشتريت %s وجهّزتها.'):format(item.label), 'good')
end

function Actions.m_unlock(src, args)
  if not Sec.rate(src, 'market', 700) then return end
  local node = nodeById[args[1] or '']
  if not node then return Sec.strike(src, 'bad tree node') end
  if not atDealer(src) then return say(src, 'الشحنات تُفتح من تاجر السوق السوداء.') end
  local pr = MD.prog(src)
  if isUnlocked(pr, node.id) then return say(src, 'مفتوحة أصلاً.') end
  for _, req in ipairs(node.requires or {}) do
    if not isUnlocked(pr, req) then return say(src, ('افتح «%s» أول.'):format(nodeById[req].label)) end
  end
  if Bridge.getCash(src) < node.cost then return say(src, ('تحتاج %d كاش.'):format(node.cost)) end
  if not Bridge.removeCash(src, node.cost) then return say(src, 'فشل الدفع.', 'alert') end
  pr.unlocked[node.id] = true
  MD.save(src)
  say(src, ('فتحت شحنة «%s».'):format(node.label), 'good')
end

local function equippedWeapon(pr, id)
  local w = weaponById[id or '']
  if w and pr.equip[slotOf(w)] == w.id then return w end
end

function Actions.m_ammo(src, args)
  if not Sec.rate(src, 'market', 700) then return end
  local pr = MD.prog(src)
  local w = equippedWeapon(pr, args[1])
  if not w then return say(src, 'جهّز السلاح أول.') end
  if not atDealer(src) then return say(src, 'لازم تكون عند تاجر السوق السوداء.') end
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
  local pr = MD.prog(src)
  local w = equippedWeapon(pr, args[1])
  if not w then return say(src, 'جهّز السلاح أول.') end
  if not atDealer(src) then return say(src, 'لازم تكون عند تاجر السوق السوداء.') end
  local mod
  for _, m in ipairs(w.mods or {}) do if m.id == args[2] then mod = m end end
  if not mod then return Sec.strike(src, 'bad mod id') end
  pr.mods[w.id] = pr.mods[w.id] or {}
  if pr.mods[w.id][mod.id] then return say(src, 'مركّب أصلاً.') end
  if Bridge.getCash(src) < mod.price then return say(src, ('تحتاج %d كاش.'):format(mod.price)) end
  if not Bridge.removeCash(src, mod.price) then return say(src, 'فشل الدفع.', 'alert') end
  pr.mods[w.id][mod.id] = true
  give(src, w.weapon, 0, { mod.comp })
  MD.save(src)
  say(src, ('ركّبت %s على %s.'):format(mod.label, w.label), 'good')
end

-- بعد الدخول من جديد: يرجّع معداتك المجهّزة (نصف الذخيرة الأساسية، مرة كل 10 دقائق)
function Actions.m_restore(src)
  if not Sec.rate(src, 'market', 700) then return end
  local p = MD.getP(src)
  if p.role == 'police' then return say(src, 'الشرطي يستلم معداته من مكتب الشرطة.') end
  local left = wait(src, 'restore', 600)
  if left > 0 then return say(src, ('انتظر %d دقيقة.'):format(math.ceil(left / 60))) end
  local pr = MD.prog(src)
  for _, slot in ipairs({ 'primary', 'secondary' }) do
    local w = weaponById[pr.equip[slot] or '']
    if w then
      local comps = {}
      for modId in pairs(pr.mods[w.id] or {}) do
        for _, m in ipairs(w.mods or {}) do if m.id == modId then comps[#comps + 1] = m.comp end end
      end
      give(src, w.weapon, math.floor(w.ammo / 2), comps)
    end
  end
  local armor = pr.equip.armor and itemById[pr.equip.armor]
  if armor then TriggerClientEvent('medo:client:gear', src, { armor = armor.armor }) end
  say(src, 'رجّعت معداتك المجهّزة.', 'good')
end

-- ===== التخزين (عند التاجر) =====
local STASH_MAX = 60
function Actions.stash_deposit(src)
  if not Sec.rate(src, 'stash', 800) then return end
  if not atDealer(src) then return say(src, 'التخزين عند التاجر.') end
  local pr = MD.prog(src)
  local moved = 0
  for i = #pr.inv, 1, -1 do
    if #pr.stash >= STASH_MAX then break end
    pr.stash[#pr.stash + 1] = pr.inv[i].id
    table.remove(pr.inv, i)
    moved = moved + 1
  end
  MD.save(src)
  say(src, moved > 0 and ('خزّنت %d غرض.'):format(moved) or 'ما عندك شي تخزنه.', moved > 0 and 'good' or 'info')
end
function Actions.stash_withdraw(src)
  if not Sec.rate(src, 'stash', 800) then return end
  if not atDealer(src) then return say(src, 'التخزين عند التاجر.') end
  local pr = MD.prog(src)
  local moved = 0
  for i = #pr.stash, 1, -1 do
    if not MD.addLoot(src, pr.stash[i]) then break end
    table.remove(pr.stash, i)
    moved = moved + 1
  end
  MD.save(src)
  say(src, moved > 0 and ('سحبت %d غرض.'):format(moved) or 'حقيبتك ممتلئة أو التخزين فاضي.', moved > 0 and 'good' or 'info')
end

AddEventHandler('playerDropped', function() cooldown[source] = nil end)
MD.wait, MD.give = wait, give
