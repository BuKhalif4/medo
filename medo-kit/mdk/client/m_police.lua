-- عميل الشرطة: الخدمة عند المركز، اقتحام الأبواب بمراحل واقعية، وفتح اختيار المعدات.
local function playAnim(dict, name, flag)
  RequestAnimDict(dict)
  local t = 0
  while not HasAnimDictLoaded(dict) and t < 40 do Wait(50); t = t + 1 end
  if HasAnimDictLoaded(dict) then TaskPlayAnim(PlayerPedId(), dict, name, 3.0, -3.0, -1, flag or 1, 0.0, false, false, false); return true end
  return false
end
local function scenario(name) TaskStartScenarioInPlace(PlayerPedId(), name, 0, true) end

local function stepKnock() if not playAnim('timetable@jimmy@doorknock@', 'knockdoor_idle', 1) then scenario('WORLD_HUMAN_STAND_IMPATIENT') end end
local function stepRam()
  scenario('WORLD_HUMAN_HAMMERING')
  PlaySoundFrontend(-1, 'Drill_Pin_Break', 'DLC_HEIST_FLEECA_SOUNDSET', true)
  ShakeGameplayCam('SMALL_EXPLOSION_SHAKE', 0.05)
end
local function stepSearch()
  StopGameplayCamShaking(true)
  if not playAnim('anim@gangops@facility@servers@bodysearch@', 'player_search', 1) then scenario('PROP_HUMAN_BUM_BIN') end
end

RegisterNetEvent('mdk:client:raidStart', function(id, duration)
  local pl = KC.spots[id]
  if pl then
    KC.progress('جاري اقتحام ' .. pl.label, duration, KC.pt(pl, 'main'), pl.radius,
      function() TriggerServerEvent('mdk:server:finishRaid') end,
      function() TriggerServerEvent('mdk:server:cancelRaid') end,
      { { frac = 0.3, label = 'تطرق الباب وتنادي: شرطة!', run = stepKnock },
        { frac = 0.3, label = 'تكسر الباب...', run = stepRam },
        { frac = 0.4, label = 'تفتّش المكان...', run = stepSearch } })
  end
end)

KC.addInteraction(function(ctx)
  if not KC.enabled('police') then return end
  local pol = KC.mod('police') or {}
  for _, p in ipairs(Config.Places) do
    if ctx.near(KC.pt(p, 'main'), p.radius) then
      if p.type == 'police' then
        if pol.allowed then
          ctx.line({ key = 'E', text = pol.cop and 'الخروج من الخدمة' or 'دخول الخدمة' }, function() KC.act('duty') end,
            nil, function() KC.open('police') end)
          ctx.line({ key = 'H', text = 'اختيار المعدات (رتبتك)' }, nil, nil, function() KC.open('police') end)
        end
      elseif pol.cop then
        ctx.line({ key = 'G', text = 'اقتحام الباب (' .. p.label .. ')' }, nil, function() TriggerServerEvent('mdk:server:startRaid', p.id) end)
      end
    end
  end
end)
