-- السطو (محلات) + بيع الغنائم + السمعة. التهديد بالسلاح يؤخر بلاغ الشرطة.
local Actions, say = MD.Actions, MD.say
local getP, prog, lootById = MD.getP, MD.prog, MD.lootById
local cooldowns = {}
local lootTables = Config.LootTables

local function roll(tier)
  local list = lootTables[tier] or lootTables[1]
  local total = 0
  for _, e in ipairs(list) do total = total + e[2] end
  local r = math.random() * total
  for _, e in ipairs(list) do
    r = r - e[2]
    if r <= 0 then return e[1] end
  end
  return list[1][1]
end

local function alarm(spot)
  if not Config.Heist.alert then return end
  for _, idStr in ipairs(GetPlayers()) do
    local s = tonumber(idStr)
    if s and MD.isPolice(s) then
      local pt = MD.point(spot)
      local f = 60.0
      TriggerClientEvent('medo:client:alert', s, pt[1] + (math.random() * 2 - 1) * f, pt[2] + (math.random() * 2 - 1) * f, pt[3], spot.tag)
    end
  end
  MD.banner('عاجل', ('إنذار سرقة في %s'):format(spot.label), 'alert')
end

RegisterNetEvent('medo:server:startHeist', function(id, quiet)
  local src = source
  if type(id) ~= 'string' or not Sec.rate(src, 'heist', 1500) then return end
  local spot = MD.spotById[id]
  if not spot or spot.type ~= 'store' then return Sec.strike(src, 'bad store id') end
  local p = getP(src)
  if p.rob or p.jailUntil then return end
  if p.role == 'police' then return say(src, 'الشرطي ما يسرق.') end
  if not (p.masked and p.crim) then return say(src, 'البس القناع أول (تصير مجرماً).') end
  if (cooldowns[id] or 0) > os.time() then return say(src, 'المحل انسرق قبل شوي.') end
  if not MD.nearSpot(src, spot, 2.0) then return Sec.strike(src, 'heist out of range') end
  p.rob = { id = id, t = os.time(), quiet = quiet == true }
  cooldowns[id] = os.time() + spot.cooldown
  if p.rob.quiet then p.rob.alarmAt = os.time() + Config.Heist.alarmDelay else p.rob.alarmed = true; alarm(spot) end
  TriggerClientEvent('medo:client:heistStart', src, id, Config.Heist.duration)
end)

RegisterNetEvent('medo:server:cancelHeist', function()
  local p = MD.P[source]
  if not p or not p.rob then return end
  cooldowns[p.rob.id] = os.time() + 30
  p.rob = nil
end)

RegisterNetEvent('medo:server:finishHeist', function()
  local src = source
  local p = MD.P[src]
  if not p or not p.rob then return end
  local spot = MD.spotById[p.rob.id]
  local elapsed = os.time() - p.rob.t
  local rob = p.rob
  p.rob = nil
  if elapsed < Config.Heist.duration - 1 then return Sec.strike(src, 'heist finished too early') end
  if not MD.nearSpot(src, spot, 3.0) then cooldowns[spot.id] = os.time() + 30; return say(src, 'ابتعدت عن المحل، فشلت السرقة.') end
  if not rob.alarmed then alarm(spot) end
  local got, lost = {}, 0
  for _ = 1, spot.rolls do
    local id = roll(spot.tier)
    if MD.addLoot(src, id) then got[#got + 1] = lootById[id].label else lost = lost + 1 end
  end
  p.streak = p.streak + 1
  MD.save(src)
  say(src, ('نهبت: %s%s. سمعتك %d.'):format(#got > 0 and table.concat(got, '، ') or 'ولا شي', lost > 0 and (' (ضاع %d: حقيبتك ممتلئة)'):format(lost) or '', MD.notoriety(src)), #got > 0 and 'good' or 'alert')
end)

-- تأخير البلاغ للسطو الهادئ
local function heistTick()
  local now = os.time()
  for _, p in pairs(MD.P) do
    local r = p.rob
    if r and r.alarmAt and not r.alarmed and r.alarmAt <= now then r.alarmed = true; alarm(MD.spotById[r.id]) end
  end
end
MD.heistTick = heistTick
CreateThread(function() while true do Wait(2000); heistTick() end end)

-- ===== البيع =====
function Actions.sell(src)
  if not Sec.rate(src, 'sell', 1000) then return end
  local p = getP(src)
  if p.role == 'police' then return end
  local spot
  for _, s in ipairs(Config.Spots) do if s.type == 'sell' and MD.nearSpot(src, s) then spot = s end end
  if not spot then return say(src, 'لازم تكون عند نقطة بيع.') end
  local base = MD.lootValue(src)
  if base <= 0 then return say(src, 'ما عندك غنائم للبيع.') end
  local bonus = 1 + Config.SellBonus * MD.notoriety(src)
  local pay = math.floor(base * bonus)
  if not Bridge.addCash(src, pay) then return say(src, 'تعذّر الدفع، حاول مرة ثانية.', 'alert') end
  MD.takeAllLoot(src)
  MD.save(src)
  say(src, ('بعت غنائمك بـ %d (القيمة %d، زيادة السمعة %d%%).'):format(pay, base, math.floor((bonus - 1) * 100 + 0.5)), 'good')
end

AddEventHandler('playerDropped', function() end)
