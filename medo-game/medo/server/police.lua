-- الشرطة: الانضمام، الرتب والمعدات، القبض والاستسلام، السجن، وموت اللاعب.
local Actions, say = MD.Actions, MD.say
local getP, prog = MD.getP, MD.prog
local rankById = MD.rankById

local function isPolice(src) return getP(src).role == 'police' end
MD.isPolice = isPolice

-- ===== الانضمام والمغادرة =====
function Actions.join_police(src)
  if not Sec.rate(src, 'role', 1500) then return end
  local p = getP(src)
  if p.role == 'police' then return end
  if p.crim then return say(src, 'وسمك الجنائي باقي. اطلع من المدينة أول أو سلّم نفسك.') end
  if p.jailUntil then return end
  if not MD.nearType(src, 'police') then return say(src, 'الانضمام من مكتب الشرطة.') end
  MD.setRole(src, 'police', false)
  p.masked = false
  local pr = prog(src)
  local rank = rankById[pr.rank or 'patrol']
  pr.rank = rank.id
  for _, k in ipairs(rank.kit) do MD.give(src, k.weapon, k.ammo, {}) end
  TriggerClientEvent('medo:client:gear', src, { armor = rank.armor })
  MD.save(src)
  say(src, ('انضممت للشرطة برتبة %s. استلمت معداتك.'):format(rank.label), 'good')
end

function Actions.leave_police(src)
  if not Sec.rate(src, 'role', 1500) or not isPolice(src) then return end
  MD.setRole(src, 'civilian', false)
  say(src, 'تركت الخدمة.', 'info')
end

function MD.addXP(src, n)
  local pr = prog(src)
  pr.xp = pr.xp + n
  MD.save(src)
  for _, r in ipairs(Config.PoliceRanks) do
    if pr.xp >= r.xp and pr.xp - n < r.xp and r.xp > 0 then say(src, ('ترقية! صرت مؤهلاً لرتبة %s.'):format(r.label), 'good') end
  end
end

function Actions.rank_select(src, args)
  if not Sec.rate(src, 'rank', 700) or not isPolice(src) then return end
  local rank = rankById[args[1] or '']
  if not rank then return Sec.strike(src, 'bad rank id') end
  if not MD.nearType(src, 'police') then return say(src, 'اختيار المعدات من مكتب الشرطة.') end
  local pr = prog(src)
  if pr.xp < rank.xp then return say(src, ('هذي الرتبة تحتاج %d نقطة (عندك %d).'):format(rank.xp, pr.xp)) end
  local left = MD.wait(src, 'kit', Config.RankKitCooldown)
  if left > 0 then return say(src, ('استلمت معداتك قبل شوي. انتظر %d ثانية.'):format(left)) end
  for _, k in ipairs(rank.kit) do MD.give(src, k.weapon, k.ammo, {}) end
  TriggerClientEvent('medo:client:gear', src, { armor = rank.armor })
  pr.rank = rank.id
  MD.save(src)
  say(src, ('استلمت معدات رتبة %s.'):format(rank.label), 'good')
end

-- ===== السجن =====
local function jail(target, cop)
  local p = getP(target)
  local secs = Config.Jail.seconds + MD.notoriety(target) * 10
  p.jailUntil = os.time() + secs
  p.masked, p.streak = false, 0
  MD.setRole(target, 'civilian', false)
  local pr = prog(target)
  pr.jail = pr.jail + secs
  MD.save(target)
  TriggerClientEvent('medo:client:tp', target, Config.Jail.cell[1], Config.Jail.cell[2], Config.Jail.cell[3])
  say(target, ('انسجنت %d ثانية. فقدت غنائمك وبقيت معداتك.'):format(secs), 'alert')
end

local function release(target)
  local p = getP(target)
  p.jailUntil = nil
  TriggerClientEvent('medo:client:tp', target, Config.Jail.release[1], Config.Jail.release[2], Config.Jail.release[3])
  say(target, 'انتهت مدة السجن. أنت حر.', 'good')
end

local function jailTick()
  local now = os.time()
  for src, p in pairs(MD.P) do
    if p.jailUntil and p.jailUntil <= now then release(src) end
  end
end
MD.jailTick = jailTick
CreateThread(function() while true do Wait(3000); jailTick() end end)

-- مصادرة الغنائم: الشرطي ياخذ نسبة، والباقي يروح
local function seize(cop, target)
  local value = MD.lootValue(target)
  MD.takeAllLoot(target)
  if cop then
    local cut = math.floor(value * Config.PoliceCut)
    if cut > 0 then Bridge.addCash(cop, cut) end
    MD.addXP(cop, value > 0 and Config.XP.seize or Config.XP.arrest)
    say(cop, value > 0 and ('قبضت عليه وصادرت غنائم بقيمة %d. حصتك %d.'):format(value, cut) or 'قبضت عليه (ما كان معه غنائم).', 'good')
  end
  jail(target, cop)
end

function Actions.arrest(src, args)
  if not Sec.rate(src, 'arrest', 1500) or not isPolice(src) then return end
  local target = tonumber(args[1])
  if not target or target == src or not GetPlayerName(target) then return end
  local tp = getP(target)
  if not tp.crim then return say(src, 'مو مجرم، ما تقدر تقبض عليه.') end
  if tp.jailUntil then return end
  if not Sec.between(src, target, Config.ArrestRange) then return say(src, 'قرّب منه أكثر.') end
  seize(src, target)
end

function Actions.surrender(src)
  if not Sec.rate(src, 'surrender', 1500) then return end
  local p = getP(src)
  if not p.crim or p.jailUntil then return end
  local cop
  for _, idStr in ipairs(GetPlayers()) do
    local s = tonumber(idStr)
    if s and s ~= src and isPolice(s) and Sec.between(src, s, Config.SurrenderRange) then cop = s; break end
  end
  if not cop then return say(src, 'ما فيه شرطي قريب تسلّم له نفسك.') end
  seize(cop, src)
end

-- ===== موت اللاعب: قواعد حسب الدور والمكان =====
local drops, dropSeq = {}, 0
local function dropList()
  local l = {}
  for id, d in pairs(drops) do l[#l + 1] = { id = id, x = d.c[1], y = d.c[2], z = d.c[3], n = #d.items } end
  return l
end
local function broadcastDrops() TriggerClientEvent('medo:client:drops', -1, dropList()) end

RegisterNetEvent('medo:server:died', function()
  local src = source
  if not Sec.rate(src, 'died', 800) then return end
  local p = getP(src)
  local ped = GetPlayerPed(src)
  if not ped or ped == 0 or GetEntityHealth(ped) > 105 then return end
  if p.rob then p.rob = nil end
  local lose = (p.role == 'criminal' and p.crim) or not MD.inCity(src)   -- مجرم، أو أي أحد بالصحراء
  if p.role == 'police' then lose = false end
  if lose and #prog(src).inv > 0 then
    local c = Sec.coords(src)
    if c then
      dropSeq = dropSeq + 1
      drops[dropSeq] = { c = c, items = MD.takeAllLoot(src), exp = os.time() + 600 }
      broadcastDrops()
      MD.save(src)
      say(src, 'متّ وسقطت غنائمك. معداتك المجهّزة بقيت معك.', 'alert')
      return
    end
  end
  say(src, 'متّ. ما خسرت غنائمك.', 'info')
end)

RegisterNetEvent('medo:server:pickup', function(id)
  local src = source
  if type(id) ~= 'number' or not Sec.rate(src, 'pickup', 800) then return end
  local d = drops[id]
  if not d or not Sec.near(src, d.c, 3.0) then return end
  local got, left = 0, {}
  for _, it in ipairs(d.items) do
    if MD.addLoot(src, it.id) then got = got + 1 else left[#left + 1] = it end
  end
  d.items = left
  if #left == 0 then drops[id] = nil end
  broadcastDrops()
  MD.save(src)
  say(src, got > 0 and ('التقطت %d غرض.'):format(got) or 'حقيبتك ممتلئة.', got > 0 and 'good' or 'alert')
end)

RegisterNetEvent('medo:server:requestDrops', function()
  local src = source
  if Sec.rate(src, 'sync', 3000) then TriggerClientEvent('medo:client:drops', src, dropList()) end
end)

CreateThread(function()
  while true do
    Wait(30000)
    local changed, now = false, os.time()
    for id, d in pairs(drops) do if d.exp <= now then drops[id] = nil; changed = true end end
    if changed then broadcastDrops() end
  end
end)

function MD.onDrop(src)
  local p = MD.P[src]
  if p and p.crim and #prog(src).inv > 0 then
    local c = Sec.coords(src)
    if c then dropSeq = dropSeq + 1; drops[dropSeq] = { c = c, items = MD.takeAllLoot(src), exp = os.time() + 600 }; broadcastDrops() end
  end
end

if __MEDO_TEST then __MT.drops = drops end
