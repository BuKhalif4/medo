-- عميل الحقيبة: القناع، الحقيبة على الظهر، إبطاء الجري، الحقائب المرمية، السطو والتبييض، والتخبئة.
local hasBag, bagWeight, masked = false, 0.0, false
local bagProp, prevMask = nil, nil
local drops, dropObjs = {}, {}
local lastDeath = 0

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

local function syncBag()
  local ped = PlayerPedId()
  if hasBag and (not bagProp or not DoesEntityExist(bagProp)) then
    local hash = KC.loadModel('prop_money_bag_01')
    bagProp = CreateObject(hash, 0.0, 0.0, 0.0, false, false, false)
    AttachEntityToEntity(bagProp, ped, GetPedBoneIndex(ped, 24816), 0.1, -0.2, 0.0, 0.0, 90.0, 180.0, true, true, false, true, 1, true)
    SetModelAsNoLongerNeeded(hash)
  elseif not hasBag and bagProp then
    if DoesEntityExist(bagProp) then DeleteEntity(bagProp) end
    bagProp = nil
    SetRunSprintMultiplierForPlayer(PlayerId(), 1.0)
  end
end

KC.onState[#KC.onState + 1] = function(s)
  local b = s.mods and s.mods.bag
  if not b then hasBag = false; syncBag(); return end
  hasBag = b.bag ~= nil
  bagWeight = hasBag and math.min(1.0, b.bag.value / (b.cap or Config.BaseCapacity)) or 0.0
  if b.masked ~= masked then masked = b.masked; applyMask(masked) end
  syncBag()
end

-- إبطاء الجري وأنت شايل حقيبة، ويصحّح القناع/الحقيبة بعد الموت
CreateThread(function()
  local tick = 0
  while true do
    if hasBag then
      SetRunSprintMultiplierForPlayer(PlayerId(), 1.0 - (1.0 - Config.MinSpeed) * bagWeight)
      tick = tick + 1
      if tick >= 120 then
        tick = 0
        syncBag()
        if masked and GetPedDrawableVariation(PlayerPedId(), 1) ~= Config.MaskDrawable then applyMask(true) end
      end
      Wait(0)
    else
      Wait(1000)
      if masked and GetPedDrawableVariation(PlayerPedId(), 1) ~= Config.MaskDrawable then applyMask(true) end
    end
  end
end)

RegisterNetEvent('mdk:client:drops', function(list)
  local seen = {}
  for _, d in ipairs(list) do
    seen[d.id] = true
    if not dropObjs[d.id] then
      local hash = KC.loadModel('prop_money_bag_01')
      local obj = CreateObject(hash, d.x, d.y, d.z, false, false, false)
      PlaceObjectOnGroundProperly(obj); FreezeEntityPosition(obj, true); SetModelAsNoLongerNeeded(hash)
      dropObjs[d.id] = obj
    end
  end
  for id, obj in pairs(dropObjs) do if not seen[id] then DeleteEntity(obj); dropObjs[id] = nil end end
  drops = list
end)

RegisterNetEvent('mdk:client:robStart', function(id, duration)
  local h = KC.spots[id]
  if h then
    KC.progress('جاري السطو على ' .. h.label, duration, KC.pt(h, 'main'), h.radius,
      function() TriggerServerEvent('mdk:server:finishRob') end,
      function() TriggerServerEvent('mdk:server:cancelRob') end)
  end
end)
RegisterNetEvent('mdk:client:launderStart', function(id, duration)
  local l = KC.spots[id]
  if l then
    KC.progress('جاري تبييض الحقيبة', duration, KC.pt(l, 'main'), l.radius,
      function() TriggerServerEvent('mdk:server:finishLaunder') end,
      function() TriggerServerEvent('mdk:server:cancelLaunder') end)
  end
end)

KC.addInteraction(function(ctx)
  if not KC.enabled('bag') then return end
  -- الموت: يرسل للسيرفر (السيرفر يتحقق بنفسه)
  if hasBag and IsPedDeadOrDying(ctx.ped, true) and GetGameTimer() - lastDeath > 1000 then
    lastDeath = GetGameTimer()
    TriggerServerEvent('mdk:server:died')
  end
  for _, h in ipairs(Config.Heists) do
    if ctx.near(KC.pt(h, 'main'), h.radius) then
      if masked then ctx.line({ key = 'E', text = 'سطو: ' .. h.label }, function() TriggerServerEvent('mdk:server:startRob', h.id) end)
      else ctx.line({ key = 'F6', text = 'البس القناع عشان تسرق' }) end
    end
  end
  for _, l in ipairs(Config.Launderers) do
    if hasBag and ctx.near(KC.pt(l, 'main'), l.radius) then ctx.line({ key = 'E', text = 'تبييض الحقيبة' }, function() TriggerServerEvent('mdk:server:startLaunder', l.id) end) end
  end
  for _, p in ipairs(Config.Places) do
    if p.type ~= 'police' and ctx.near(KC.pt(p, 'inside'), 2.5) then
      ctx.line({ key = 'E', text = hasBag and ('تخبئة الحقيبة (' .. p.label .. ')') or ('استرجاع حقيبتك المخبأة (' .. p.label .. ')') },
        function() KC.act('stash', { p.id }) end)
    end
  end
  if not hasBag then
    for _, d in ipairs(drops) do
      if ctx.near(vec3(d.x, d.y, d.z), 2.0) then ctx.line({ key = 'E', text = 'التقاط الحقيبة' }, function() TriggerServerEvent('mdk:server:pickup', d.id) end) end
    end
  end
end)

KC.cleanup[#KC.cleanup + 1] = function()
  if bagProp and DoesEntityExist(bagProp) then DeleteEntity(bagProp) end
  for _, o in pairs(dropObjs) do DeleteEntity(o) end
  SetRunSprintMultiplierForPlayer(PlayerId(), 1.0)
end
