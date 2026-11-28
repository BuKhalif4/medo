-- نواة العميل: الواجهة (NUI)، المفاتيح، محرك التفاعل (E / مطوّل)، الرسائل، شريط التقدم.
KC = { S = { mods = {}, modules = {} }, interactions = {}, menuOpen = false, busy = false }
local overrides = {}
KC.overrides = overrides

function KC.nui(t) SendNUIMessage(t) end
function KC.vec(c) return vec3(c[1], c[2], c[3]) end
function KC.mod(id) return KC.S.mods and KC.S.mods[id] end
function KC.enabled(id) for _, m in ipairs(KC.S.modules or {}) do if m.id == id then return m.enabled end end return false end
function KC.act(name, args) TriggerServerEvent('mdk:server:action', name, args or {}) end
function KC.loadModel(name)
  local hash = joaat(name)
  RequestModel(hash)
  local t = 0
  while not HasModelLoaded(hash) and t < 100 do Wait(50); t = t + 1 end
  return hash
end
function KC.addInteraction(fn) KC.interactions[#KC.interactions + 1] = fn end

-- ===== الحالة =====
RegisterNetEvent('mdk:client:state', function(s)
  KC.S = s
  KC.nui({ type = 'state', state = s })
  if KC.onState then for _, fn in ipairs(KC.onState) do fn(s) end end
end)
KC.onState = {}

-- ===== إعدادات لوحة التحكم تتطبق على Config عندنا أيضاً =====
RegisterNetEvent('mdk:client:settings', function(list)
  for _, d in ipairs(Config.Settings) do
    for _, it in ipairs(list) do
      if it.id == d.id and it.value ~= nil then
        local t = Config
        for i = 1, #d.path - 1 do t = t[d.path[i]] end
        t[d.path[#d.path]] = it.value
      end
    end
  end
  KC.nui({ type = 'settings', list = list })
end)
RegisterNetEvent('mdk:client:stats', function(stats) KC.nui({ type = 'stats', stats = stats }) end)
RegisterNetEvent('mdk:client:modules', function() TriggerServerEvent('mdk:server:state') end)

-- ===== رسائل =====
RegisterNetEvent('mdk:client:notify', function(text, kind) KC.nui({ type = 'toast', text = text, kind = kind }) end)
RegisterNetEvent('mdk:client:banner', function(title, text, kind) KC.nui({ type = 'banner', title = title, text = text, kind = kind }) end)
RegisterNetEvent('mdk:client:doc', function(title, lines) KC.nui({ type = 'doc', title = title, lines = lines }) end)
RegisterNetEvent('mdk:client:list', function(title, items) KC.nui({ type = 'list', title = title, items = items }) end)
RegisterNetEvent('mdk:client:zone', function(left) KC.nui({ type = 'zone', left = left }) end)
RegisterNetEvent('mdk:client:players', function(list) KC.nui({ type = 'players', list = list }) end)

RegisterNetEvent('mdk:client:tp', function(x, y, z)
  local ped = PlayerPedId()
  if IsPedInAnyVehicle(ped, false) then SetPedCoordsKeepVehicle(ped, x, y, z) else SetEntityCoords(ped, x, y, z, false, false, false, false) end
end)

RegisterNetEvent('mdk:client:gear', function(g)
  local ped = PlayerPedId()
  if g.armor then SetPedArmour(ped, g.armor) end
  if g.weapon then
    local hash = joaat(g.weapon)
    if not HasPedGotWeapon(ped, hash, false) then GiveWeaponToPed(ped, hash, g.ammo or 0, false, false)
    elseif (g.ammo or 0) > 0 then AddAmmoToPed(ped, hash, g.ammo) end
    for _, comp in ipairs(g.comps or {}) do GiveWeaponComponentToPed(ped, hash, joaat(comp)) end
  end
end)

RegisterNetEvent('mdk:client:alert', function(x, y, z, tag)
  PlaySoundFrontend(-1, 'Event_Message_Purple', 'GTAO_FM_Events_Soundset', true)
  local area = AddBlipForRadius(x, y, z, 120.0)
  SetBlipColour(area, 1); SetBlipAlpha(area, 120)
  local icon = AddBlipForCoord(x, y, z)
  SetBlipSprite(icon, 161); SetBlipColour(icon, 1); SetBlipFlashes(icon, true)
  BeginTextCommandSetBlipName('STRING'); AddTextComponentSubstringPlayerName(tag or 'Alert'); EndTextCommandSetBlipName(icon)
  SetTimeout(60000, function() RemoveBlip(area); RemoveBlip(icon) end)
end)

-- ===== شريط التقدم (مع مراحل اختيارية) =====
function KC.progress(label, seconds, center, radius, onDone, onCancel, phases)
  KC.busy = true
  local function cancel()
    ClearPedTasks(PlayerPedId()); StopGameplayCamShaking(true)
    KC.busy = false; KC.nui({ type = 'progress', hide = true }); onCancel()
  end
  if phases then phases[1].run() else TaskStartScenarioInPlace(PlayerPedId(), 'PROP_HUMAN_BUM_BIN', 0, true) end
  KC.nui({ type = 'progress', label = phases and phases[1].label or label, seconds = seconds })
  CreateThread(function()
    local left, phase, elapsed = seconds, 1, 0
    local nextAt = phases and math.floor(seconds * phases[1].frac) or nil
    while left > 0 do
      local ped = PlayerPedId()
      if #(GetEntityCoords(ped) - center) > radius + 1.5 or IsPedDeadOrDying(ped, true) then return cancel() end
      if phases and phase < #phases and elapsed >= nextAt then
        phase = phase + 1
        ClearPedTasks(ped)
        phases[phase].run()
        KC.nui({ type = 'progresslabel', label = phases[phase].label })
        nextAt = nextAt + math.floor(seconds * phases[phase].frac)
      end
      Wait(1000)
      left, elapsed = left - 1, elapsed + 1
      if phases and phase == 2 then ShakeGameplayCam('SMALL_EXPLOSION_SHAKE', 0.05) end
    end
    ClearPedTasks(PlayerPedId()); StopGameplayCamShaking(true)
    KC.busy = false; KC.nui({ type = 'progress', hide = true }); onDone()
  end)
end

-- ===== لوحة التحكم (الهب) =====
function KC.open(page)
  if KC.menuOpen or KC.busy then return end
  KC.menuOpen = true
  SetNuiFocus(true, true)
  KC.nui({ type = 'open', page = page, state = KC.S, config = KC.configForUi() })
  TriggerServerEvent('mdk:server:state')
end
function KC.close()
  if not KC.menuOpen then return end
  KC.menuOpen = false
  SetNuiFocus(false, false)
end

-- كل بيانات الواجهة الثابتة من Config (أسعار، شجرة، رتب...). الإعدادات الحية تأتي من السيرفر.
function KC.configForUi()
  local weapons = {}
  for _, w in ipairs(Config.Weapons) do
    local m = {}
    for _, x in ipairs(w.mods or {}) do m[#m + 1] = { id = x.id, label = x.label, price = x.price } end
    weapons[#weapons + 1] = { id = w.id, cat = 'weapon', slot = w.slot, tier = w.tier, node = w.node, label = w.label, price = w.price, ammo = w.ammo, mods = m }
  end
  local gear = {}
  for _, g in ipairs(Config.Gear) do
    gear[#gear + 1] = { id = g.id, cat = g.cat, tier = g.tier, node = g.node, label = g.label, price = g.price, armor = g.armor, squares = g.squares, capacity = g.capacity }
  end
  return { weapons = weapons, gear = gear, tree = Config.Tree, refill = Config.RefillRate, baseSquares = Config.BaseSquares, partyMax = Config.PartyMax, bribe = { min = Config.BribeMin, max = Config.BribeMax } }
end

RegisterNUICallback('close', function(_, cb) KC.close(); cb('ok') end)
RegisterNUICallback('action', function(data, cb)
  if type(data) == 'table' and type(data.name) == 'string' then
    TriggerServerEvent('mdk:server:action', data.name, type(data.args) == 'table' and data.args or {})
  end
  cb('ok')
end)
RegisterNUICallback('nearby', function(_, cb)
  local out, me, pos = {}, PlayerId(), GetEntityCoords(PlayerPedId())
  for _, pl in ipairs(GetActivePlayers()) do
    if pl ~= me then
      local d = #(GetEntityCoords(GetPlayerPed(pl)) - pos)
      if d <= 12.0 then out[#out + 1] = { id = GetPlayerServerId(pl), name = GetPlayerName(pl), dist = math.floor(d * 10) / 10 } end
    end
  end
  table.sort(out, function(a, b) return a.dist < b.dist end)
  KC.nui({ type = 'nearby', list = out })
  cb('ok')
end)
RegisterNUICallback('waypoint', function(data, cb)
  local spot = data and KC.spots and KC.spots[data.id]
  if spot then
    local c = KC.pt(spot, 'main')
    SetNewWaypoint(c.x + 0.0, c.y + 0.0)
    KC.nui({ type = 'toast', text = 'تم وضع العلامة على الخريطة', kind = 'good' })
  end
  cb('ok')
end)

RegisterCommand('mdk_hub', function() if KC.menuOpen then KC.close(); KC.nui({ type = 'close' }) else KC.open('home') end end, false)
RegisterKeyMapping('mdk_hub', 'MDK: لوحة التحكم', 'keyboard', 'F5')
RegisterCommand('mdk_mask', function() KC.act('mask') end, false)
RegisterKeyMapping('mdk_mask', 'MDK: القناع', 'keyboard', 'F6')
RegisterCommand('mdk_drop', function() KC.act('dropbag') end, false)
RegisterKeyMapping('mdk_drop', 'MDK: رمي الحقيبة', 'keyboard', 'F7')

-- ===== محرك التفاعل: ينام 700ms وأنت بعيد، ويصحى لما تقترب =====
local HOLD, lastPrompt = {}, ''
local function holdProgress(id, key, ms, dt)
  if IsControlPressed(0, key) then HOLD[id] = (HOLD[id] or 0) + dt else HOLD[id] = 0 end
  return math.min(1.0, (HOLD[id] or 0) / ms), (HOLD[id] or 0) >= ms
end

CreateThread(function()
  Wait(3000)
  TriggerServerEvent('mdk:server:state')
  TriggerServerEvent('mdk:server:requestDrops')
  local last = GetGameTimer()
  while true do
    local now = GetGameTimer()
    local dt = now - last
    last = now
    local lines, onTap, onG, onH, hold = {}, nil, nil, nil, nil
    local ctx = { wait = 700, dt = dt }
    if not KC.busy and not KC.menuOpen then
      local pos = GetEntityCoords(PlayerPedId())
      ctx.pos, ctx.ped = pos, PlayerPedId()
      function ctx.near(c, r)
        local d = #(pos - c)
        if d < 30.0 and ctx.wait > 250 then ctx.wait = 250 end
        if d <= r then ctx.wait = 0; return true end
        return false
      end
      function ctx.line(l, tap, g, h) lines[#lines + 1] = l; if tap then onTap = tap end; if g then onG = g end; if h then onH = h end end
      function ctx.hold(id, key, ms, fn) hold = { id = id, key = key, ms = ms, fn = fn } end
      for _, fn in ipairs(KC.interactions) do fn(ctx) end
      if ctx.wait == 0 then
        if onTap and IsControlJustReleased(0, 38) then onTap(); Wait(800) end
        if onG and IsControlJustReleased(0, 47) then onG(); Wait(800) end
        if onH and IsControlJustReleased(0, 74) then onH(); Wait(800) end
      end
      if hold then
        ctx.wait = 0
        local frac, done = holdProgress(hold.id, hold.key, hold.ms, dt)
        for _, l in ipairs(lines) do if l.hold then l.progress = frac end end
        if done then HOLD[hold.id] = 0; hold.fn(); Wait(1500) end
      end
    end
    local sig = ''
    for _, l in ipairs(lines) do sig = sig .. (l.key or '') .. l.text .. math.floor((l.progress or 0) * 10) .. '|' end
    if sig ~= lastPrompt then lastPrompt = sig; KC.nui({ type = 'prompt', lines = lines }) end
    Wait(ctx.wait)
  end
end)

AddEventHandler('onResourceStop', function(res)
  if res ~= GetCurrentResourceName() then return end
  SetNuiFocus(false, false)
  if KC.cleanup then for _, fn in ipairs(KC.cleanup) do fn() end end
end)
KC.cleanup = {}
