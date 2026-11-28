-- سكربت الحدود والأماكن: حدود المدينة، ونقاط الأماكن (تُحدَّد من اللعبة وتنحفظ).
K.spots = {}
for _, h in ipairs(Config.Heists) do h.type = 'heist'; K.spots[h.id] = h end
for _, l in ipairs(Config.Launderers) do l.type = 'launderer'; K.spots[l.id] = l end
for _, p in ipairs(Config.Places) do K.spots[p.id] = p end

local points = {}   -- spotId -> { main=..., inside=..., bench=... }
do local saved = GetResourceKvpString('mdk_points'); if saved then points = json.decode(saved) or {} end end

function K.spotPoint(spot, kind)
  kind = kind or 'main'
  local o = points[spot.id] and points[spot.id][kind]
  if o then return { o[1], o[2], o[3] } end
  if kind == 'main' then return spot.coords end
  if kind == 'inside' then return spot.inside or spot.coords end
  if kind == 'bench' then return spot.bench or (spot.type == 'gang' and K.spotPoint(spot, 'inside')) or nil end
end
function K.nearSpot(src, spot, kind, range) return Sec.near(src, K.spotPoint(spot, kind) or spot.coords, range or ((spot.radius or 3.0) + 2.0)) end
function K.nearPlaceType(src, ptype)
  for _, pl in ipairs(Config.Places) do
    if pl.type == ptype and K.nearSpot(src, pl, 'main', pl.radius + 3.0) then return pl end
  end
end

function K.syncClient(src) TriggerClientEvent('mdk:client:points', src, points) end

-- ===== حدود المدينة =====
local zoneOut = {}
local function zoneTick()
  if not K.enabled('zone') then return end
  local z = Config.Zone
  for _, idStr in ipairs(GetPlayers()) do
    local src = tonumber(idStr)
    local c = src and Sec.coords(src)
    if c and not (math.abs(c[1]) < 1 and math.abs(c[2]) < 1) then
      local dx, dy = c[1] - z.center[1], c[2] - z.center[2]
      if dx * dx + dy * dy > z.radius * z.radius then
        zoneOut[src] = (zoneOut[src] or 0) + 3
        local left = z.warnSeconds - zoneOut[src]
        if left <= 0 then
          zoneOut[src] = nil
          TriggerClientEvent('mdk:client:zone', src, 0)
          TriggerClientEvent('mdk:client:tp', src, z.safe[1], z.safe[2], z.safe[3])
          K.say(src, 'تجاوزت حدود المدينة، رجعناك.', 'alert')
        else TriggerClientEvent('mdk:client:zone', src, left) end
      elseif zoneOut[src] then zoneOut[src] = nil; TriggerClientEvent('mdk:client:zone', src, 0) end
    end
  end
end
K.zoneTick = zoneTick
K.thread(3000, zoneTick)
K.onPlayerDrop(function(src) zoneOut[src] = nil end)

-- ===== أدوات المطوّر: انتقال وتحديد النقاط =====
local function devOnly(src) if not K.isDev(src) then Sec.strike(src, 'dev action without permission'); return false end; return true end

K.action('zone', 'tp', function(src, args)
  if not devOnly(src) then return end
  local spot = K.spots[args[1] or '']
  if not spot then return end
  local kind = (args[2] == 'inside' or args[2] == 'bench') and args[2] or 'main'
  local pt = K.spotPoint(spot, kind) or K.spotPoint(spot, 'main')
  TriggerClientEvent('mdk:client:tp', src, pt[1], pt[2], pt[3] + 1.0)
end)

K.action('zone', 'setpoint', function(src, args)
  if not devOnly(src) or not Sec.rate(src, 'setpoint', 1000) then return end
  local spot, kind = K.spots[args[1] or ''], args[2]
  if not spot then return end
  if kind ~= 'main' and kind ~= 'inside' and kind ~= 'bench' then return end
  if kind ~= 'main' and spot.type ~= 'gang' and spot.type ~= 'house' then return K.say(src, 'هذه النقطة للمقرات والبيوت فقط.') end
  if kind == 'bench' and spot.type ~= 'gang' then return K.say(src, 'الطاولة للمقرات فقط.') end
  local c = Sec.coords(src)
  if not c then return end
  points[spot.id] = points[spot.id] or {}
  points[spot.id][kind] = { c[1], c[2], c[3] }
  SetResourceKvp('mdk_points', json.encode(points))
  TriggerClientEvent('mdk:client:points', -1, points)
  K.say(src, ('تم حفظ النقطة (%s) لـ %s.'):format(kind == 'main' and 'الباب/الموقع' or (kind == 'inside' and 'داخل الغرفة' or 'الطاولة'), spot.label), 'good')
end)

K.action('zone', 'resetpoint', function(src, args)
  if not devOnly(src) then return end
  local spot = K.spots[args[1] or '']
  if not spot or not points[spot.id] then return end
  points[spot.id][args[2] or ''] = nil
  SetResourceKvp('mdk_points', json.encode(points))
  TriggerClientEvent('mdk:client:points', -1, points)
  K.say(src, 'رجعت النقطة للإعدادات الأصلية.')
end)

K.action('zone', 'give', function(src, args)   -- كاش للتجربة
  if not devOnly(src) then return end
  K.addCash(src, math.min(tonumber(args[1]) or 100000, 5000000))
  K.say(src, 'تمت إضافة كاش (أدوات التجربة).', 'good')
end)

K.provide('zone', function(src)
  local list = {}
  for _, h in ipairs(Config.Heists) do list[#list + 1] = { id = h.id, label = h.label, type = 'heist' } end
  for _, l in ipairs(Config.Launderers) do list[#list + 1] = { id = l.id, label = l.label, type = 'launderer' } end
  for _, p in ipairs(Config.Places) do list[#list + 1] = { id = p.id, label = p.label, type = p.type } end
  return K.isDev(src) and { spots = list } or {}
end)

if __MDK_TEST then __MT.points = points end
