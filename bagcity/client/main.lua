-- العميل: يعرض ويطلب فقط. كل القرارات في السيرفر.
-- ملاحظة: خط GTA ما يعرض العربي، لذلك النصوص العربية تطلع بالشات والنصوص فوق الشاشة بالإنجليزي.

local masked, hasBag, bagWeight, minSpeed = false, false, 0.0, 0.7
local bagProp = nil
local working = false          -- يسرق أو يبيّض الحين
local drops, dropObjs = {}, {}
local prevMask = nil

local function helpText(text)
  BeginTextCommandDisplayHelp('STRING')
  AddTextComponentSubstringPlayerName(text)
  EndTextCommandDisplayHelp(0, false, true, -1)
end

local function loadModel(name)
  local hash = joaat(name)
  RequestModel(hash)
  local t = 0
  while not HasModelLoaded(hash) and t < 100 do Wait(50); t = t + 1 end
  return hash
end

-- ===== القناع =====
RegisterNetEvent('bagcity:client:mask', function(on)
  local ped = PlayerPedId()
  masked = on
  if on then
    prevMask = { GetPedDrawableVariation(ped, 1), GetPedTextureVariation(ped, 1) }
    SetPedComponentVariation(ped, 1, Config.MaskDrawable, Config.MaskTexture, 0)
  elseif prevMask then
    SetPedComponentVariation(ped, 1, prevMask[1], prevMask[2], 0)
    prevMask = nil
  end
end)

-- ===== الحقيبة (الوزن + الشكل) =====
RegisterNetEvent('bagcity:client:bag', function(has, weight, minSp)
  hasBag, bagWeight, minSpeed = has, weight, minSp
  local ped = PlayerPedId()
  if has and not bagProp then
    local hash = loadModel('prop_money_bag_01')
    bagProp = CreateObject(hash, 0.0, 0.0, 0.0, false, false, false)
    AttachEntityToEntity(bagProp, ped, GetPedBoneIndex(ped, 24816), 0.1, -0.2, 0.0, 0.0, 90.0, 180.0, true, true, false, true, 1, true)
    SetModelAsNoLongerNeeded(hash)
  elseif not has and bagProp then
    DeleteEntity(bagProp)
    bagProp = nil
    SetRunSprintMultiplierForPlayer(PlayerId(), 1.0)
  end
end)

-- يشتغل كل فريم فقط وأنت شايل حقيبة
CreateThread(function()
  while true do
    if hasBag then
      SetRunSprintMultiplierForPlayer(PlayerId(), 1.0 - (1.0 - minSpeed) * bagWeight)
      Wait(0)
    else
      Wait(1000)
    end
  end
end)

AddEventHandler('onResourceStop', function(res)
  if res ~= GetCurrentResourceName() then return end
  if bagProp then DeleteEntity(bagProp) end
  for _, o in pairs(dropObjs) do DeleteEntity(o) end
  SetRunSprintMultiplierForPlayer(PlayerId(), 1.0)
end)

-- ===== انتقال التجربة (السيرفر يرسله فقط لمن عنده صلاحية المطوّر) =====
RegisterNetEvent('bagcity:client:tp', function(x, y, z)
  SetEntityCoords(PlayerPedId(), x, y, z, false, false, false, false)
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
  local blip = AddBlipForRadius(x, y, z, 120.0)
  SetBlipColour(blip, 1)
  SetBlipAlpha(blip, 120)
  local icon = AddBlipForCoord(x, y, z)
  SetBlipSprite(icon, 161)
  SetBlipColour(icon, 1)
  SetBlipFlashes(icon, true)
  BeginTextCommandSetBlipName('STRING')
  AddTextComponentSubstringPlayerName(tag or 'Robbery')
  EndTextCommandSetBlipName(icon)
  SetTimeout(60000, function() RemoveBlip(blip); RemoveBlip(icon) end)
end)

-- ===== السرقة والتبييض: شريط تقدم يتحقق من المسافة =====
local function progress(label, seconds, center, radius, onDone, onCancel)
  working = true
  local ped = PlayerPedId()
  TaskStartScenarioInPlace(ped, 'PROP_HUMAN_BUM_BIN', 0, true)
  local left = seconds
  CreateThread(function()
    while left > 0 do
      local c = GetEntityCoords(PlayerPedId())
      if #(c - center) > radius + 1.5 or IsPedDeadOrDying(PlayerPedId(), true) then
        ClearPedTasks(PlayerPedId()); working = false; onCancel(); return
      end
      helpText(('%s... %ds'):format(label, left))
      Wait(1000)
      left = left - 1
    end
    ClearPedTasks(PlayerPedId()); working = false; onDone()
  end)
end

RegisterNetEvent('bagcity:client:robStart', function(id, duration)
  for _, h in ipairs(Config.Heists) do
    if h.id == id then
      progress('Robbing', duration, vec3(h.coords[1], h.coords[2], h.coords[3]), h.radius,
        function() TriggerServerEvent('bagcity:server:finishRob') end,
        function() TriggerServerEvent('bagcity:server:cancelRob') end)
    end
  end
end)

RegisterNetEvent('bagcity:client:launderStart', function(id, duration)
  for _, l in ipairs(Config.Launderers) do
    if l.id == id then
      progress('Laundering', duration, vec3(l.coords[1], l.coords[2], l.coords[3]), l.radius,
        function() TriggerServerEvent('bagcity:server:finishLaunder') end,
        function() TriggerServerEvent('bagcity:server:cancelLaunder') end)
    end
  end
end)

-- ===== التفاعل: ينام 800ms بعيد، ويصحى لما تقترب =====
CreateThread(function()
  Wait(3000)
  TriggerServerEvent('bagcity:server:requestSync')
  local spots = {}
  for _, h in ipairs(Config.Heists) do
    spots[#spots + 1] = { kind = 'heist', id = h.id, c = vec3(h.coords[1], h.coords[2], h.coords[3]), r = h.radius, text = '[E] Rob ' .. h.tag }
  end
  for _, l in ipairs(Config.Launderers) do
    spots[#spots + 1] = { kind = 'laund', id = l.id, c = vec3(l.coords[1], l.coords[2], l.coords[3]), r = l.radius, text = '[E] Launder bag' }
  end

  while true do
    local wait = 800
    if not working then
      local pos = GetEntityCoords(PlayerPedId())
      for _, s in ipairs(spots) do
        local d = #(pos - s.c)
        if d < 25.0 then wait = 250 end
        if d <= s.r and ((s.kind == 'heist' and masked) or (s.kind == 'laund' and hasBag)) then
          wait = 0
          helpText(s.text)
          if IsControlJustReleased(0, 38) then
            TriggerServerEvent(s.kind == 'heist' and 'bagcity:server:startRob' or 'bagcity:server:startLaunder', s.id)
            Wait(1500)
          end
        end
      end
      if not hasBag then
        for _, d in ipairs(drops) do
          local dist = #(pos - vec3(d.x, d.y, d.z))
          if dist < 25.0 then wait = math.min(wait, 250) end
          if dist <= 2.0 then
            wait = 0
            helpText('[E] Pick up bag')
            if IsControlJustReleased(0, 38) then
              TriggerServerEvent('bagcity:server:pickup', d.id)
              Wait(1000)
            end
          end
        end
      end
    end
    Wait(wait)
  end
end)
