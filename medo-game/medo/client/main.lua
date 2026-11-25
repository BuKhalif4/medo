-- ميدو (العميل): يعرض ويطلب فقط. السيرفر هو الحاكم. كل الواجهة NUI (عربي كامل).

local S = { role = 'civilian', inv = {}, equip = {} }   -- آخر حالة من السيرفر
local overrides = {}
local menuOpen, busy = false, false
local drops, dropObjs = {}, {}
local spotPeds, blips = {}, {}
local origModel, prevMask = nil, nil
local partyBlips = {}
local lastPrompt, lastDeath = '', 0
local aimedMs, aimClerk = 0, nil
local changingModel = false

local function nui(t) SendNUIMessage(t) end
local function vec(c) return vec3(c[1], c[2], c[3]) end

local function loadModel(name)
  local hash = joaat(name)
  RequestModel(hash)
  local t = 0
  while not HasModelLoaded(hash) and t < 100 do Wait(50); t = t + 1 end
  return hash
end

-- نقطة المكان: ما حدده المطوّر وإلا الإعدادات (نفس منطق السيرفر)
local function pt(spot)
  local o = overrides[spot.id] and overrides[spot.id].main
  if o then return vec3(o[1], o[2], o[3]) end
  return vec(spot.coords)
end
local function clerkPt(spot)
  local o = overrides[spot.id] and overrides[spot.id].clerk
  local h = spot.clerk and spot.clerk[4] or 0.0
  if o then return vec3(o[1], o[2], o[3]), h end
  if spot.clerk then return vec3(spot.clerk[1], spot.clerk[2], spot.clerk[3]), h end
end

-- ===== الدور: الشرطي يلبس زي الشرطة، والمجرم القناع =====
local function setModel(name)
  local hash = loadModel(name)
  SetPlayerModel(PlayerId(), hash)
  SetModelAsNoLongerNeeded(hash)
  SetPedDefaultComponentVariation(PlayerPedId())
end

local function applyMask(on)
  local ped = PlayerPedId()
  if on then
    prevMask = { GetPedDrawableVariation(ped, 1), GetPedTextureVariation(ped, 1) }
    SetPedComponentVariation(ped, 1, Config.MaskDrawable, Config.MaskTexture, 0)
  elseif prevMask then
    SetPedComponentVariation(ped, 1, prevMask[1], prevMask[2], 0)
    prevMask = nil
  end
end

RegisterNetEvent('medo:client:role', function(role)
  changingModel = true
  if role == 'police' then
    if not origModel then origModel = GetEntityModel(PlayerPedId()) end
    setModel(Config.PoliceModel)
  elseif origModel then
    SetPlayerModel(PlayerId(), origModel)
    SetModelAsNoLongerNeeded(origModel)
    SetPedDefaultComponentVariation(PlayerPedId())
    origModel = nil
  end
  Wait(300)
  changingModel = false
end)

local menuOpenFn
RegisterNetEvent('medo:client:state', function(s)
  local wasMasked = S.masked
  S = s
  if s.masked ~= (wasMasked or false) and s.role ~= 'police' then applyMask(s.masked) end
  nui({ type = 'state', state = s })
end)

-- ===== رسائل =====
RegisterNetEvent('medo:client:notify', function(text, kind) nui({ type = 'toast', text = text, kind = kind }) end)
RegisterNetEvent('medo:client:banner', function(title, text, kind) nui({ type = 'banner', title = title, text = text, kind = kind }) end)
RegisterNetEvent('medo:client:zone', function(left) nui({ type = 'zone', left = left }) end)
RegisterNetEvent('medo:client:players', function(list) nui({ type = 'players', list = list }) end)

-- المسح الأسبوعي: تنشال كل الأسلحة
RegisterNetEvent('medo:client:wipe', function()
  RemoveAllPedWeapons(PlayerPedId(), true)
  SetPedArmour(PlayerPedId(), 0)
end)

-- المركبة المستأجرة: نركب فيها لما تظهر عندنا
RegisterNetEvent('medo:client:vehicle', function(netId)
  CreateThread(function()
    local t = 0
    while not NetworkDoesNetworkIdExist(netId) and t < 100 do Wait(100); t = t + 1 end
    local veh = NetworkDoesNetworkIdExist(netId) and NetToVeh(netId) or 0
    if veh ~= 0 then SetPedIntoVehicle(PlayerPedId(), veh, -1) end
  end)
end)

RegisterNetEvent('medo:client:tp', function(x, y, z)
  local ped = PlayerPedId()
  if IsPedInAnyVehicle(ped, false) then SetPedCoordsKeepVehicle(ped, x, y, z) else SetEntityCoords(ped, x, y, z, false, false, false, false) end
end)

RegisterNetEvent('medo:client:gear', function(g)
  while changingModel do Wait(50) end    -- لا تعطي سلاح قبل ما يخلص تغيير الشخصية (زي الشرطة)
  local ped = PlayerPedId()
  if g.armor then SetPedArmour(ped, g.armor) end
  if g.weapon then
    local hash = joaat(g.weapon)
    if not HasPedGotWeapon(ped, hash, false) then GiveWeaponToPed(ped, hash, g.ammo or 0, false, false)
    elseif (g.ammo or 0) > 0 then AddAmmoToPed(ped, hash, g.ammo) end
    for _, comp in ipairs(g.comps or {}) do GiveWeaponComponentToPed(ped, hash, joaat(comp)) end
  end
end)

-- ===== بلاغ الشرطة =====
RegisterNetEvent('medo:client:alert', function(x, y, z, tag)
  PlaySoundFrontend(-1, 'Event_Message_Purple', 'GTAO_FM_Events_Soundset', true)
  local area = AddBlipForRadius(x, y, z, 120.0)
  SetBlipColour(area, 1); SetBlipAlpha(area, 120)
  local icon = AddBlipForCoord(x, y, z)
  SetBlipSprite(icon, 161); SetBlipColour(icon, 1); SetBlipFlashes(icon, true)
  BeginTextCommandSetBlipName('STRING'); AddTextComponentSubstringPlayerName(tag or 'Alarm'); EndTextCommandSetBlipName(icon)
  SetTimeout(60000, function() RemoveBlip(area); RemoveBlip(icon) end)
end)

-- ===== علامات الخريطة =====
local function addBlip(c, sprite, color, name, scale)
  local b = AddBlipForCoord(c.x, c.y, c.z)
  SetBlipSprite(b, sprite); SetBlipColour(b, color); SetBlipScale(b, scale or 0.85); SetBlipAsShortRange(b, true)
  BeginTextCommandSetBlipName('STRING'); AddTextComponentSubstringPlayerName(name); EndTextCommandSetBlipName(b)
  blips[#blips + 1] = b
end
local zoneBlip
local function buildBlips()
  for _, b in ipairs(blips) do RemoveBlip(b) end
  blips = {}
  if not zoneBlip then
    local z = Config.Zone
    zoneBlip = AddBlipForRadius(z.center[1], z.center[2], z.center[3], z.radius)
    SetBlipColour(zoneBlip, 1); SetBlipAlpha(zoneBlip, 60)
  end
  for _, s in ipairs(Config.Spots) do
    local sprite, color = 52, 5
    if s.type == 'terminal' then sprite, color = 357, 3 elseif s.type == 'dealer' then sprite, color = 110, 25 elseif s.type == 'police' then sprite, color = 60, 3 elseif s.type == 'sell' then sprite, color = 108, 2 end
    addBlip(pt(s), sprite, color, s.tag, 0.8)
  end
end

-- ===== أشخاص الأماكن (التاجر، مكتب الشرطة، المشترين، موظفو المحلات) =====
local function removePed(id) if spotPeds[id] and DoesEntityExist(spotPeds[id]) then DeleteEntity(spotPeds[id]) end; spotPeds[id] = nil end
local function spawnPed(id, model, c, h)
  removePed(id)
  local hash = loadModel(model)
  local ped = CreatePed(4, hash, c.x, c.y, c.z - 1.0, h or 0.0, false, true)
  SetModelAsNoLongerNeeded(hash)
  SetEntityInvincible(ped, true); FreezeEntityPosition(ped, true); SetBlockingOfNonTemporaryEvents(ped, true)
  SetPedCanRagdoll(ped, false); SetPedFleeAttributes(ped, 0, false)
  spotPeds[id] = ped
  return ped
end

CreateThread(function()
  Wait(2500)
  while true do
    local pos = GetEntityCoords(PlayerPedId())
    for _, s in ipairs(Config.Spots) do
      local c, h = pt(s), s.heading
      if s.type == 'store' then c, h = clerkPt(s) end
      local model = s.type == 'store' and 'mp_m_shopkeep_01' or s.ped
      if c and model then
        local near = #(pos - c) < 90.0
        if near and not (spotPeds[s.id] and DoesEntityExist(spotPeds[s.id])) then spawnPed(s.id, model, c, h)
        elseif not near and spotPeds[s.id] then removePed(s.id) end
      end
    end
    Wait(2000)
  end
end)

RegisterNetEvent('medo:client:points', function(o)
  overrides = o or {}
  buildBlips()
  for id in pairs(spotPeds) do removePed(id) end   -- يتعاد إنشاؤهم بالمكان الجديد
end)
CreateThread(buildBlips)

-- ===== الحقائب المرمية =====
RegisterNetEvent('medo:client:drops', function(list)
  local seen = {}
  for _, d in ipairs(list) do
    seen[d.id] = true
    if not dropObjs[d.id] then
      local hash = loadModel('prop_money_bag_01')
      local obj = CreateObject(hash, d.x, d.y, d.z, false, false, false)
      PlaceObjectOnGroundProperly(obj); FreezeEntityPosition(obj, true); SetModelAsNoLongerNeeded(hash)
      dropObjs[d.id] = obj
    end
  end
  for id, obj in pairs(dropObjs) do if not seen[id] then DeleteEntity(obj); dropObjs[id] = nil end end
  drops = list
end)

-- ===== المجموعة: علامات الأعضاء =====
RegisterNetEvent('medo:client:party', function(members)
  for _, b in ipairs(partyBlips) do RemoveBlip(b) end
  partyBlips = {}
  for _, m in ipairs(members or {}) do
    if m.id ~= GetPlayerServerId(PlayerId()) then
      local pl = GetPlayerFromServerId(m.id)
      if pl and pl ~= -1 then
        local b = AddBlipForEntity(GetPlayerPed(pl))
        SetBlipColour(b, 38); SetBlipScale(b, 0.8)
        BeginTextCommandSetBlipName('STRING'); AddTextComponentSubstringPlayerName(m.name or 'Party'); EndTextCommandSetBlipName(b)
        partyBlips[#partyBlips + 1] = b
      end
    end
  end
end)

-- ===== السطو: شريط تقدم يتحقق من المسافة =====
local function progress(label, seconds, center, radius, onDone, onCancel)
  busy = true
  TaskStartScenarioInPlace(PlayerPedId(), 'PROP_HUMAN_BUM_BIN', 0, true)
  nui({ type = 'progress', label = label, seconds = seconds })
  CreateThread(function()
    local left = seconds
    while left > 0 do
      local ped = PlayerPedId()
      if #(GetEntityCoords(ped) - center) > radius + 2.0 or IsPedDeadOrDying(ped, true) then
        ClearPedTasks(ped); busy = false; nui({ type = 'progress', hide = true }); return onCancel()
      end
      Wait(1000); left = left - 1
    end
    ClearPedTasks(PlayerPedId()); busy = false; nui({ type = 'progress', hide = true }); onDone()
  end)
end

RegisterNetEvent('medo:client:heistStart', function(id, duration)
  for _, s in ipairs(Config.Spots) do
    if s.id == id then
      progress('جاري السطو على ' .. s.label, duration, pt(s), s.radius,
        function() TriggerServerEvent('medo:server:finishHeist') end,
        function() TriggerServerEvent('medo:server:cancelHeist') end)
    end
  end
end)

-- ===== القوائم =====
local function placesForMenu()
  local list = {}
  for _, s in ipairs(Config.Spots) do list[#list + 1] = { id = s.id, label = s.label, type = s.type, clerk = s.clerk ~= nil } end
  return list
end

local function catalogForMenu()
  local weapons = {}
  for _, w in ipairs(Config.Weapons) do
    local m = {}
    for _, x in ipairs(w.mods or {}) do m[#m + 1] = { id = x.id, label = x.label, price = x.price } end
    weapons[#weapons + 1] = { id = w.id, cat = 'weapon', slot = w.slot, tier = w.tier, node = w.node, label = w.label, price = w.price, ammo = w.ammo, mods = m }
  end
  local gear = {}
  for _, g in ipairs(Config.Gear) do
    gear[#gear + 1] = { id = g.id, cat = g.cat, tier = g.tier, node = g.node, label = g.label, price = g.price, armor = g.armor, squares = g.squares }
  end
  local ranks = {}
  for _, r in ipairs(Config.PoliceRanks) do
    local kit = {}
    for _, k in ipairs(r.kit) do kit[#kit + 1] = { label = k.label } end
    ranks[#ranks + 1] = { id = r.id, label = r.label, xp = r.xp, armor = r.armor, kit = kit }
  end
  local loot = {}
  for _, l in ipairs(Config.Loot) do loot[#loot + 1] = { id = l.id, label = l.label, size = l.size, value = l.value } end
  return { weapons = weapons, gear = gear, tree = Config.Tree, ranks = ranks, loot = loot, partyMax = Config.PartyMax,
    base = { squares = Config.BaseSquares, width = Config.GridWidth }, refill = Config.RefillRate }
end

local function openMenu(panel)
  if menuOpen or busy then return end
  menuOpen = true
  SetNuiFocus(true, true)
  nui({ type = 'open', panel = panel, places = placesForMenu(), catalog = catalogForMenu(), state = S })
  TriggerServerEvent('medo:server:state')
end
menuOpenFn = openMenu

local function closeMenu()
  if not menuOpen then return end
  menuOpen = false
  SetNuiFocus(false, false)
end

RegisterNUICallback('close', function(_, cb) closeMenu(); cb('ok') end)
RegisterNUICallback('action', function(data, cb)
  if type(data) == 'table' and type(data.name) == 'string' then
    TriggerServerEvent('medo:server:action', data.name, type(data.args) == 'table' and data.args or {})
  end
  cb('ok')
end)

RegisterCommand('medo_menu', function() if menuOpen then closeMenu(); nui({ type = 'close' }) else openMenu('inventory') end end, false)
RegisterKeyMapping('medo_menu', 'MEDO: الحقيبة والمجموعة', 'keyboard', 'F5')
RegisterCommand('medo_mask', function() TriggerServerEvent('medo:server:action', 'mask', {}) end, false)
RegisterKeyMapping('medo_mask', 'MEDO: القناع', 'keyboard', 'F6')

-- ===== شريط الحالة (HUD) =====
local function weaponSlots()
  local slots, ped = {}, PlayerPedId()
  local function add(label, weaponName)
    local hash = joaat(weaponName)
    if HasPedGotWeapon(ped, hash, false) then
      local _, max = GetMaxAmmo(ped, hash)
      slots[#slots + 1] = { label = label, ammo = GetAmmoInPedWeapon(ped, hash), max = max or 0 }
    end
  end
  if S.role == 'police' then
    for _, r in ipairs(Config.PoliceRanks) do
      if r.id == (S.rank or 'patrol') then for _, k in ipairs(r.kit) do add(k.label, k.weapon) end end
    end
  else
    for _, w in ipairs(Config.Weapons) do
      if S.equip and (S.equip.primary == w.id or S.equip.secondary == w.id) then add(w.label, w.weapon) end
    end
  end
  return slots
end

local function objective()
  if S.role == 'police' then return 'قبض على المجرمين وحماية المدينة' end
  if S.lootValue and S.lootValue > 0 then return 'بع غنائمك عند نقطة بيع' end
  if S.crim then return 'اسرق محلاً وانتبه من الشرطة' end
  return 'البس القناع (F6) لتصير مجرماً، أو انضم للشرطة'
end

CreateThread(function()
  while true do
    local ped = PlayerPedId()
    local c = GetEntityCoords(ped)
    local street = GetStreetNameFromHashKey((GetStreetNameAtCoord(c.x, c.y, c.z)))
    local area = GetLabelText(GetNameOfZone(c.x, c.y, c.z))
    nui({ type = 'hud', street = street, area = area, hp = math.max(0, math.min(100, GetEntityHealth(ped) - 100)),
      armor = GetPedArmour(ped), slots = weaponSlots(), objective = objective() })
    Wait(300)
  end
end)

-- ===== علامة الهدف (سهم أخضر مزرق فوق المكان) =====
local function nearestSpot(stype)
  local pos, best, bd = GetEntityCoords(PlayerPedId()), nil, 1e9
  for _, s in ipairs(Config.Spots) do
    if s.type == stype then
      local d = #(pos - pt(s))
      if d < bd then best, bd = s, d end
    end
  end
  return best, bd
end

CreateThread(function()
  while true do
    local target
    if S.role ~= 'police' then
      if S.lootValue and S.lootValue > 0 then target = nearestSpot('sell')
      elseif S.crim and S.masked then target = nearestSpot('store') end
    end
    if target then
      local c = pt(target)
      if #(GetEntityCoords(PlayerPedId()) - c) < 600.0 then
        DrawMarker(20, c.x, c.y, c.z + 1.6, 0.0, 0.0, 0.0, 180.0, 0.0, 0.0, 0.9, 0.9, 0.9, 60, 230, 200, 210, true, false, 2, false, nil, nil, false)
        Wait(0)
      else Wait(500) end
    else Wait(800) end
  end
end)

-- ===== التفاعل: ضغطة (E) أو مطوّلة (hold) مع حلقة تقدم =====
local HOLD = {}            -- id -> ms مضغوط
local function holdProgress(id, key, ms, dt)
  if IsControlPressed(0, key) then HOLD[id] = (HOLD[id] or 0) + dt else HOLD[id] = 0 end
  return math.min(1.0, (HOLD[id] or 0) / ms), (HOLD[id] or 0) >= ms
end

local function nearestPlayerWhere(maxDist, pred)
  local me, pos, best, bd = PlayerId(), GetEntityCoords(PlayerPedId()), nil, maxDist
  for _, pl in ipairs(GetActivePlayers()) do
    if pl ~= me then
      local sid = GetPlayerServerId(pl)
      local st = Player(sid).state
      if pred(st) then
        local d = #(GetEntityCoords(GetPlayerPed(pl)) - pos)
        if d < bd then best, bd = sid, d end
      end
    end
  end
  return best
end

CreateThread(function()
  Wait(3000)
  TriggerServerEvent('medo:server:state')
  TriggerServerEvent('medo:server:requestDrops')
  local last = GetGameTimer()

  while true do
    local now = GetGameTimer()
    local dt = now - last
    last = now
    local wait = 600
    local lines, onTap, hold = {}, nil, nil     -- hold = { id, key, ms, fn }

    if not busy and not menuOpen then
      local ped = PlayerPedId()
      local pos = GetEntityCoords(ped)

      if (S.inv and #S.inv > 0) and IsPedDeadOrDying(ped, true) and now - lastDeath > 1000 then
        lastDeath = now
        TriggerServerEvent('medo:server:died')
      end

      local function near(c, r)
        local d = #(pos - c)
        if d < 30.0 then wait = math.min(wait, 250) end
        if d <= r then wait = 0; return true end
        return false
      end

      for _, s in ipairs(Config.Spots) do
        local c = pt(s)
        if s.type == 'dealer' and near(c, s.radius) then
          lines[#lines + 1] = { key = 'E', text = 'تحدث مع التاجر' }
          onTap = function() openMenu('dealer') end
        elseif s.type == 'police' and near(c, s.radius) then
          if S.role == 'police' then
            lines[#lines + 1] = { key = 'E', text = 'اختيار المعدات' }
            onTap = function() openMenu('ranks') end
            lines[#lines + 1] = { key = 'G', text = 'ترك الخدمة', hold = true }
            hold = { id = 'leave', key = 47, ms = 1200, fn = function() TriggerServerEvent('medo:server:action', 'leave_police', {}) end }
          elseif S.crim then
            lines[#lines + 1] = { text = 'وسمك الجنائي باقي: اطلع من المدينة أو استسلم' }
          else
            lines[#lines + 1] = { key = 'E', text = 'انضم للشرطة (اضغط مطوّلاً)', hold = true }
            hold = { id = 'join', key = 38, ms = 1500, fn = function() TriggerServerEvent('medo:server:action', 'join_police', {}) end }
          end
        elseif s.type == 'terminal' and near(c, s.radius) then
          lines[#lines + 1] = { key = 'E', text = 'استئجار مركبة (مجاناً)' }
          onTap = function() TriggerServerEvent('medo:server:action', 'rent', {}) end
        elseif s.type == 'sell' and near(c, s.radius) then
          if S.lootValue and S.lootValue > 0 then
            lines[#lines + 1] = { key = 'E', text = ('بيع الغنائم (%d)'):format(S.lootValue) }
            onTap = function() TriggerServerEvent('medo:server:action', 'sell', {}) end
          else lines[#lines + 1] = { text = 'ما عندك غنائم للبيع' } end
        elseif s.type == 'store' and near(c, s.radius) then
          if S.role == 'police' then
          elseif S.masked and S.crim then
            local clerk = spotPeds[s.id]
            -- التهديد بالسلاح: صوّب على الموظف 3 ثواني فيرفع يديه والسطو يصير هادئاً
            if clerk and IsPlayerFreeAiming(PlayerId()) then
              local _, target = GetEntityPlayerIsFreeAimingAt(PlayerId())
              if target == clerk then
                aimedMs = aimedMs + dt
                if aimedMs >= Config.Heist.aimSeconds * 1000 and aimClerk ~= clerk then
                  aimClerk = clerk
                  TaskHandsUp(clerk, 15000, ped, -1, true)
                end
              end
            else aimedMs = 0; aimClerk = nil end
            lines[#lines + 1] = { key = 'E', text = (aimedMs >= Config.Heist.aimSeconds * 1000) and 'سطو هادئ (الموظف رفع يديه)' or 'سطو (وجّه سلاحك على الموظف لسطو هادئ)' }
            local quiet = aimedMs >= Config.Heist.aimSeconds * 1000
            onTap = function() TriggerServerEvent('medo:server:startHeist', s.id, quiet) end
          else
            lines[#lines + 1] = { key = 'F6', text = 'البس القناع عشان تسرق' }
          end
        end
      end

      if S.crim and S.role == 'criminal' and not hold then
        local cop = nearestPlayerWhere(Config.SurrenderRange, function(st) return st.medo_role == 'police' end)
        if cop then
          lines[#lines + 1] = { key = 'X', text = 'استسلم (اضغط مطوّلاً)', hold = true }
          hold = { id = 'surrender', key = 73, ms = 1500, fn = function() TriggerServerEvent('medo:server:action', 'surrender', {}) end }
        end
      end
      if S.role == 'police' then
        local crim = nearestPlayerWhere(Config.ArrestRange, function(st) return st.medo_crim == true end)
        if crim then
          lines[#lines + 1] = { key = 'E', text = 'قبض على المجرم' }
          onTap = function() TriggerServerEvent('medo:server:action', 'arrest', { crim }) end
          wait = 0
        end
      end
      for _, d in ipairs(drops) do
        if near(vec3(d.x, d.y, d.z), 2.0) then
          lines[#lines + 1] = { key = 'E', text = 'التقاط الغنائم' }
          onTap = function() TriggerServerEvent('medo:server:pickup', d.id) end
        end
      end

      if wait == 0 then
        if onTap and IsControlJustReleased(0, 38) then onTap(); Wait(800) end
      end
      if hold then
        wait = 0
        local frac, done = holdProgress(hold.id, hold.key, hold.ms, dt)
        for _, l in ipairs(lines) do if l.hold then l.progress = frac end end
        if done then HOLD[hold.id] = 0; hold.fn(); Wait(1500) end
      end
    end

    local sig = ''
    for _, l in ipairs(lines) do sig = sig .. (l.key or '') .. l.text .. math.floor((l.progress or 0) * 10) .. '|' end
    if sig ~= lastPrompt then lastPrompt = sig; nui({ type = 'prompt', lines = lines }) end
    Wait(wait)
  end
end)

AddEventHandler('onResourceStop', function(res)
  if res ~= GetCurrentResourceName() then return end
  for id in pairs(spotPeds) do removePed(id) end
  for _, o in pairs(dropObjs) do DeleteEntity(o) end
  SetNuiFocus(false, false)
end)
