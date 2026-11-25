-- العميل: يعرض ويطلب فقط. كل القرارات في السيرفر. الواجهة كلها NUI (عربي كامل).

local S = {}                         -- آخر حالة من السيرفر
local hasBag, bagWeight = false, 0.0
local bagProp, prevMask = nil, nil
local menuOpen, working = false, false
local drops, dropObjs = {}, {}
local lastPrompt, lastDeath = '', 0
local overrides = {}                 -- نقاط حددها المطوّر (من السيرفر)
local blips = {}
local nearInfo = { bench = false, armory = false }

local function nui(t) SendNUIMessage(t) end

local function loadModel(name)
  local hash = joaat(name)
  RequestModel(hash)
  local t = 0
  while not HasModelLoaded(hash) and t < 100 do Wait(50); t = t + 1 end
  return hash
end

-- نقطة المكان: ما حدده المطوّر، وإلا الإعدادات (نفس منطق السيرفر)
local function pt(spot, kind)
  local o = overrides[spot.id] and overrides[spot.id][kind]
  if o then return vec3(o[1], o[2], o[3]) end
  local c
  if kind == 'main' then c = spot.coords
  elseif kind == 'inside' then c = spot.inside or spot.coords
  elseif kind == 'bench' then
    if spot.bench then c = spot.bench elseif spot.type == 'gang' then return pt(spot, 'inside') end
  end
  return c and vec3(c[1], c[2], c[3]) or nil
end

local function findSpot(list, id) for _, e in ipairs(list) do if e.id == id then return e end end end

-- ===== القناع =====
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

-- ===== الحقيبة على الظهر =====
local function syncBag()
  local ped = PlayerPedId()
  if hasBag and (not bagProp or not DoesEntityExist(bagProp)) then
    local hash = loadModel('prop_money_bag_01')
    bagProp = CreateObject(hash, 0.0, 0.0, 0.0, false, false, false)
    AttachEntityToEntity(bagProp, ped, GetPedBoneIndex(ped, 24816), 0.1, -0.2, 0.0, 0.0, 90.0, 180.0, true, true, false, true, 1, true)
    SetModelAsNoLongerNeeded(hash)
  elseif not hasBag and bagProp then
    if DoesEntityExist(bagProp) then DeleteEntity(bagProp) end
    bagProp = nil
    SetRunSprintMultiplierForPlayer(PlayerId(), 1.0)
  end
end

-- ===== حالة اللاعب من السيرفر =====
local menuOpenFn
RegisterNetEvent('bagcity:client:state', function(s)
  local wasMasked = S.masked
  S = s
  hasBag = s.bag ~= nil
  bagWeight = hasBag and math.min(1.0, s.bag.value / Config.MaxBagValue) or 0.0
  if s.masked ~= (wasMasked or false) then applyMask(s.masked) end
  syncBag()
  nui({ type = 'state', state = s })
  if s.arrested and not menuOpen then menuOpenFn('chase') end
end)

-- إبطاء الجري وأنت شايل حقيبة، ويصحّح القناع/الحقيبة لو ضاعوا بعد الموت أو تغيّر الشخصية
CreateThread(function()
  local tick = 0
  while true do
    if hasBag then
      SetRunSprintMultiplierForPlayer(PlayerId(), 1.0 - (1.0 - Config.MinSpeed) * bagWeight)
      tick = tick + 1
      if tick >= 120 then
        tick = 0
        syncBag()
        if S.masked and GetPedDrawableVariation(PlayerPedId(), 1) ~= Config.MaskDrawable then applyMask(true) end
      end
      Wait(0)
    else
      Wait(1000)
      if S.masked and GetPedDrawableVariation(PlayerPedId(), 1) ~= Config.MaskDrawable then applyMask(true) end
    end
  end
end)

AddEventHandler('onResourceStop', function(res)
  if res ~= GetCurrentResourceName() then return end
  if bagProp and DoesEntityExist(bagProp) then DeleteEntity(bagProp) end
  for _, o in pairs(dropObjs) do DeleteEntity(o) end
  SetRunSprintMultiplierForPlayer(PlayerId(), 1.0)
  SetNuiFocus(false, false)
end)

-- ===== رسائل الواجهة =====
RegisterNetEvent('bagcity:client:notify', function(text, kind) nui({ type = 'toast', text = text, kind = kind }) end)
RegisterNetEvent('bagcity:client:banner', function(title, text, kind) nui({ type = 'banner', title = title, text = text, kind = kind }) end)
RegisterNetEvent('bagcity:client:doc', function(title, lines) nui({ type = 'doc', title = title, lines = lines }) end)
RegisterNetEvent('bagcity:client:list', function(title, items) nui({ type = 'list', title = title, items = items }) end)
RegisterNetEvent('bagcity:client:zone', function(left) nui({ type = 'zone', left = left }) end)

RegisterNetEvent('bagcity:client:tp', function(x, y, z)
  local ped = PlayerPedId()
  if IsPedInAnyVehicle(ped, false) then SetPedCoordsKeepVehicle(ped, x, y, z) else SetEntityCoords(ped, x, y, z, false, false, false, false) end
end)

-- ===== الأسلحة (السيرفر يقرر، والعميل ينفّذ) =====
RegisterNetEvent('bagcity:client:gear', function(g)
  local ped = PlayerPedId()
  if g.armor then SetPedArmour(ped, g.armor) end
  if g.weapon then
    local hash = joaat(g.weapon)
    if not HasPedGotWeapon(ped, hash, false) then
      GiveWeaponToPed(ped, hash, g.ammo or 0, false, false)
    elseif (g.ammo or 0) > 0 then
      AddAmmoToPed(ped, hash, g.ammo)
    end
    for _, comp in ipairs(g.comps or {}) do GiveWeaponComponentToPed(ped, hash, joaat(comp)) end
  end
end)

-- ===== الحقائب المرمية =====
RegisterNetEvent('bagcity:client:drops', function(list)
  local seen = {}
  for _, d in ipairs(list) do
    seen[d.id] = true
    if not dropObjs[d.id] then
      local hash = loadModel('prop_money_bag_01')
      local obj = CreateObject(hash, d.x, d.y, d.z, false, false, false)
      PlaceObjectOnGroundProperly(obj)
      FreezeEntityPosition(obj, true)
      SetModelAsNoLongerNeeded(hash)
      dropObjs[d.id] = obj
    end
  end
  for id, obj in pairs(dropObjs) do
    if not seen[id] then DeleteEntity(obj); dropObjs[id] = nil end
  end
  drops = list
end)

-- ===== بلاغ الشرطة =====
RegisterNetEvent('bagcity:client:alert', function(x, y, z, tag)
  PlaySoundFrontend(-1, 'Event_Message_Purple', 'GTAO_FM_Events_Soundset', true)
  local area = AddBlipForRadius(x, y, z, 120.0)
  SetBlipColour(area, 1)
  SetBlipAlpha(area, 120)
  local icon = AddBlipForCoord(x, y, z)
  SetBlipSprite(icon, 161)
  SetBlipColour(icon, 1)
  SetBlipFlashes(icon, true)
  BeginTextCommandSetBlipName('STRING')
  AddTextComponentSubstringPlayerName(tag or 'Robbery')
  EndTextCommandSetBlipName(icon)
  SetTimeout(60000, function() RemoveBlip(area); RemoveBlip(icon) end)
end)

-- ===== علامات الخريطة (أسماء إنجليزية لأن خط GTA ما يعرض العربي) =====
local function addBlip(c, sprite, color, name, scale)
  local b = AddBlipForCoord(c.x, c.y, c.z)
  SetBlipSprite(b, sprite)
  SetBlipColour(b, color)
  SetBlipScale(b, scale or 0.8)
  SetBlipAsShortRange(b, true)
  BeginTextCommandSetBlipName('STRING')
  AddTextComponentSubstringPlayerName(name)
  EndTextCommandSetBlipName(b)
  blips[#blips + 1] = b
end

local zoneBlip
local function buildBlips()
  for _, b in ipairs(blips) do RemoveBlip(b) end
  blips = {}
  if not zoneBlip then
    local z = Config.Zone
    zoneBlip = AddBlipForRadius(z.center[1], z.center[2], z.center[3], z.radius)
    SetBlipColour(zoneBlip, 1)
    SetBlipAlpha(zoneBlip, 70)
  end
  for _, h in ipairs(Config.Heists) do addBlip(pt(h, 'main'), 52, 5, h.tag, 0.7) end
  for _, l in ipairs(Config.Launderers) do addBlip(pt(l, 'main'), 108, 2, 'Launderer', 0.8) end
  for _, p in ipairs(Config.Places) do
    local sprite, color = 40, 2
    if p.type == 'police' then sprite, color = 60, 3 elseif p.type == 'gang' then sprite, color = 84, 1 end
    addBlip(pt(p, 'main'), sprite, color, p.tag, 0.9)
  end
end

RegisterNetEvent('bagcity:client:points', function(o)
  overrides = o or {}
  buildBlips()
end)
CreateThread(buildBlips)

-- ===== الأنيميشن =====
local function playAnim(dict, name, flag)
  RequestAnimDict(dict)
  local t = 0
  while not HasAnimDictLoaded(dict) and t < 40 do Wait(50); t = t + 1 end
  if HasAnimDictLoaded(dict) then
    TaskPlayAnim(PlayerPedId(), dict, name, 3.0, -3.0, -1, flag or 1, 0.0, false, false, false)
    return true
  end
  return false
end
local function scenario(name) TaskStartScenarioInPlace(PlayerPedId(), name, 0, true) end

-- مراحل الاقتحام: طرق الباب، كسره، ثم تفتيش المكان
local function stepKnock()
  if not playAnim('timetable@jimmy@doorknock@', 'knockdoor_idle', 1) then scenario('WORLD_HUMAN_STAND_IMPATIENT') end
end
local function stepRam()
  scenario('WORLD_HUMAN_HAMMERING')
  PlaySoundFrontend(-1, 'Drill_Pin_Break', 'DLC_HEIST_FLEECA_SOUNDSET', true)
  ShakeGameplayCam('SMALL_EXPLOSION_SHAKE', 0.05)
end
local function stepSearch()
  StopGameplayCamShaking(true)
  if not playAnim('anim@gangops@facility@servers@bodysearch@', 'player_search', 1) then scenario('PROP_HUMAN_BUM_BIN') end
end

-- ===== شريط التقدم (سرقة / تبييض / مداهمة) =====
-- phases (اختياري): { {frac=0.3, label='..', run=function() end}, ... }
local function progress(label, seconds, center, radius, onDone, onCancel, phases)
  working = true
  local function cancel(reason)
    local ped = PlayerPedId()
    ClearPedTasks(ped); StopGameplayCamShaking(true)
    working = false; nui({ type = 'progress', hide = true }); onCancel()
  end
  if phases then phases[1].run() else scenario('PROP_HUMAN_BUM_BIN') end
  nui({ type = 'progress', label = phases and phases[1].label or label, seconds = seconds })
  CreateThread(function()
    local left, phase, nextAt = seconds, 1, phases and math.floor(seconds * phases[1].frac) or nil
    local elapsed = 0
    while left > 0 do
      local ped = PlayerPedId()
      if #(GetEntityCoords(ped) - center) > radius + 1.5 or IsPedDeadOrDying(ped, true) then return cancel() end
      if phases and phase < #phases and elapsed >= nextAt then
        phase = phase + 1
        ClearPedTasks(ped)
        phases[phase].run()
        nui({ type = 'progresslabel', label = phases[phase].label })
        nextAt = nextAt + math.floor(seconds * phases[phase].frac)
      end
      Wait(1000)
      left, elapsed = left - 1, elapsed + 1
      if phases and phase == 2 then ShakeGameplayCam('SMALL_EXPLOSION_SHAKE', 0.05) end
    end
    ClearPedTasks(PlayerPedId()); StopGameplayCamShaking(true)
    working = false; nui({ type = 'progress', hide = true }); onDone()
  end)
end

RegisterNetEvent('bagcity:client:robStart', function(id, duration)
  local h = findSpot(Config.Heists, id)
  if h then
    progress('جاري السطو على ' .. h.label, duration, pt(h, 'main'), h.radius,
      function() TriggerServerEvent('bagcity:server:finishRob') end,
      function() TriggerServerEvent('bagcity:server:cancelRob') end)
  end
end)

RegisterNetEvent('bagcity:client:launderStart', function(id, duration)
  local l = findSpot(Config.Launderers, id)
  if l then
    progress('جاري تبييض الحقيبة', duration, pt(l, 'main'), l.radius,
      function() TriggerServerEvent('bagcity:server:finishLaunder') end,
      function() TriggerServerEvent('bagcity:server:cancelLaunder') end)
  end
end)

RegisterNetEvent('bagcity:client:raidStart', function(id, duration)
  local pl = findSpot(Config.Places, id)
  if pl then
    progress('جاري اقتحام ' .. pl.label, duration, pt(pl, 'main'), pl.radius,
      function() TriggerServerEvent('bagcity:server:finishRaid') end,
      function() TriggerServerEvent('bagcity:server:cancelRaid') end,
      {
        { frac = 0.3, label = 'تطرق الباب وتنادي: شرطة!', run = stepKnock },
        { frac = 0.3, label = 'تكسر الباب...', run = stepRam },
        { frac = 0.4, label = 'تفتّش المكان...', run = stepSearch },
      })
  end
end)

-- ===== القائمة الدائرية =====
local function nearbyPlayers()
  local out, me, pos = {}, PlayerId(), GetEntityCoords(PlayerPedId())
  for _, pl in ipairs(GetActivePlayers()) do
    if pl ~= me or S.solo then
      local d = #(GetEntityCoords(GetPlayerPed(pl)) - pos)
      if d <= 12.0 then
        out[#out + 1] = { id = GetPlayerServerId(pl), name = GetPlayerName(pl), dist = math.floor(d * 10) / 10 }
      end
    end
  end
  table.sort(out, function(a, b) return a.dist < b.dist end)
  return out
end

local function placesForMenu()
  local list = {}
  for _, p in ipairs(Config.Places) do
    list[#list + 1] = { id = p.id, label = p.label, icon = p.type == 'police' and '🛡️' or (p.type == 'gang' and '🏴' or '🏠'),
      kind = p.type == 'police' and 'الشرطة' or (p.type == 'gang' and 'مقر عصابة' or 'بيت آمن'), place = true, gang = p.type == 'gang' }
  end
  for _, h in ipairs(Config.Heists) do list[#list + 1] = { id = h.id, label = h.label, icon = '🛒', kind = 'سطو' } end
  for _, l in ipairs(Config.Launderers) do list[#list + 1] = { id = l.id, label = l.label, icon = '💱', kind = 'تبييض' } end
  return list
end

local function gearForMenu()
  local list = {}
  for _, w in ipairs(Config.Weapons) do
    local m = {}
    for _, x in ipairs(w.mods) do m[#m + 1] = { id = x.id, label = x.label, price = x.price } end
    list[#list + 1] = { id = w.id, label = w.label, price = w.price, ammo = w.ammo, refill = math.floor(w.price * Config.RefillRate), mods = m }
  end
  return { weapons = list, armor = Config.ArmorPrice }
end

local function computeNear()
  local pos = GetEntityCoords(PlayerPedId())
  local bench, armory = false, false
  for _, p in ipairs(Config.Places) do
    if p.type == 'gang' then
      local b = pt(p, 'bench')
      if b and #(pos - b) <= 4.0 then bench = true end
    elseif p.type == 'police' and #(pos - pt(p, 'main')) <= p.radius + 3.0 then
      armory = true
    end
  end
  return { bench = bench, armory = armory }
end

local function openMenu(tab)
  if menuOpen or working then return end
  menuOpen = true
  SetNuiFocus(true, true)
  nearInfo = computeNear()
  nui({ type = 'open', tab = tab or 'citizen', nearby = nearbyPlayers(), places = placesForMenu(), gear = gearForMenu(), near = nearInfo,
    bribe = { min = Config.BribeMin, max = Config.BribeMax }, state = S })
  TriggerServerEvent('bagcity:server:state')
  CreateThread(function()
    while menuOpen do
      Wait(1500)
      if menuOpen then nui({ type = 'nearby', list = nearbyPlayers() }) end
    end
  end)
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
    TriggerServerEvent('bagcity:server:action', data.name, type(data.args) == 'table' and data.args or {})
  end
  cb('ok')
end)
RegisterNUICallback('waypoint', function(data, cb)
  local p = data and (findSpot(Config.Places, data.id) or findSpot(Config.Heists, data.id) or findSpot(Config.Launderers, data.id))
  if p then
    local c = pt(p, 'main')
    SetNewWaypoint(c.x + 0.0, c.y + 0.0)
    nui({ type = 'toast', text = 'تم وضع العلامة على الخريطة', kind = 'good' })
  end
  cb('ok')
end)
RegisterNUICallback('online', function(_, cb)
  nui({ type = 'toast', text = ('اللاعبين المتصلين: %d'):format(#GetActivePlayers()), kind = 'info' })
  cb('ok')
end)

-- مفاتيح قابلة للتغيير من إعدادات FiveM (Settings > Key Bindings > FiveM)
RegisterCommand('bagcity_menu', function() if menuOpen then closeMenu(); nui({ type = 'close' }) else openMenu('citizen') end end, false)
RegisterKeyMapping('bagcity_menu', 'Bag City: فتح القائمة', 'keyboard', 'F5')
RegisterCommand('bagcity_mask', function() TriggerServerEvent('bagcity:server:action', 'mask', {}) end, false)
RegisterKeyMapping('bagcity_mask', 'Bag City: القناع', 'keyboard', 'F6')
RegisterCommand('bagcity_drop', function() TriggerServerEvent('bagcity:server:action', 'dropbag', {}) end, false)
RegisterKeyMapping('bagcity_drop', 'Bag City: رمي الحقيبة', 'keyboard', 'F7')

-- ===== التفاعل مع الأماكن: ينام 700ms وأنت بعيد، ويصحى لما تقترب =====
CreateThread(function()
  Wait(3000)
  TriggerServerEvent('bagcity:server:requestSync')
  TriggerServerEvent('bagcity:server:state')

  while true do
    local wait = 700
    local lines, onE, onG, onH = {}, nil, nil, nil

    if not working and not menuOpen then
      local ped = PlayerPedId()
      local pos = GetEntityCoords(ped)

      if hasBag and IsPedDeadOrDying(ped, true) and GetGameTimer() - lastDeath > 1000 then
        lastDeath = GetGameTimer()
        TriggerServerEvent('bagcity:server:died')
      end

      local function near(c, r)
        if not c then return false end
        local d = #(pos - c)
        if d < 30.0 then wait = math.min(wait, 250) end
        if d <= r then wait = 0; return true end
        return false
      end

      for _, h in ipairs(Config.Heists) do
        if near(pt(h, 'main'), h.radius) then
          if S.masked then
            lines[#lines + 1] = 'E — سطو: ' .. h.label
            onE = function() TriggerServerEvent('bagcity:server:startRob', h.id) end
          else
            lines[#lines + 1] = 'البس القناع (F6) عشان تسرق'
          end
        end
      end
      for _, l in ipairs(Config.Launderers) do
        if hasBag and near(pt(l, 'main'), l.radius) then
          lines[#lines + 1] = 'E — تبييض الحقيبة'
          onE = function() TriggerServerEvent('bagcity:server:startLaunder', l.id) end
        end
      end
      for _, p in ipairs(Config.Places) do
        -- الباب
        if near(pt(p, 'main'), p.radius) then
          if p.type == 'police' then
            if S.copAllowed then
              lines[#lines + 1] = 'E — دخول / خروج الخدمة'
              onE = function() TriggerServerEvent('bagcity:server:action', 'duty', {}) end
            end
          elseif S.cop then
            lines[#lines + 1] = 'G — اقتحام الباب (' .. p.label .. ')'
            onG = function() TriggerServerEvent('bagcity:server:startRaid', p.id) end
          end
        end
        -- داخل الغرفة: التخبئة
        if p.type ~= 'police' and near(pt(p, 'inside'), 2.5) then
          lines[#lines + 1] = hasBag and ('E — تخبئة الحقيبة (' .. p.label .. ')') or ('E — استرجاع حقيبتك المخبأة (' .. p.label .. ')')
          onE = function() TriggerServerEvent('bagcity:server:action', 'stash', { p.id }) end
        end
        -- طاولة الأدوات
        if p.type == 'gang' and near(pt(p, 'bench'), 2.0) then
          lines[#lines + 1] = 'H — طاولة الأدوات (أسلحة وتعديل وتعبئة)'
          onH = function() openMenu('bench') end
        end
      end
      if not hasBag then
        for _, d in ipairs(drops) do
          if near(vec3(d.x, d.y, d.z), 2.0) then
            lines[#lines + 1] = 'E — التقاط الحقيبة'
            onE = function() TriggerServerEvent('bagcity:server:pickup', d.id) end
          end
        end
      end

      if wait == 0 then
        if onE and IsControlJustReleased(0, 38) then onE(); Wait(800) end
        if onG and IsControlJustReleased(0, 47) then onG(); Wait(800) end
        if onH and IsControlJustReleased(0, 74) then onH(); Wait(800) end
      end
    end

    local text = table.concat(lines, '\n')
    if text ~= lastPrompt then lastPrompt = text; nui({ type = 'prompt', text = text }) end
    Wait(wait)
  end
end)
