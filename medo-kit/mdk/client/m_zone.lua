-- عميل الحدود والأماكن: نقاط الأماكن (الإعدادات + ما حدده المطوّر) وعلامات الخريطة.
local blips, zoneBlip = {}, nil
KC.spots = {}
for _, h in ipairs(Config.Heists) do h.type = 'heist'; KC.spots[h.id] = h end
for _, l in ipairs(Config.Launderers) do l.type = 'launderer'; KC.spots[l.id] = l end
for _, p in ipairs(Config.Places) do KC.spots[p.id] = p end

function KC.pt(spot, kind)
  kind = kind or 'main'
  local o = KC.overrides[spot.id] and KC.overrides[spot.id][kind]
  if o then return vec3(o[1], o[2], o[3]) end
  local c
  if kind == 'main' then c = spot.coords
  elseif kind == 'inside' then c = spot.inside or spot.coords
  elseif kind == 'bench' then
    if spot.bench then c = spot.bench elseif spot.type == 'gang' then return KC.pt(spot, 'inside') end
  end
  return c and vec3(c[1], c[2], c[3]) or nil
end

local function addBlip(c, sprite, color, name, scale)
  local b = AddBlipForCoord(c.x, c.y, c.z)
  SetBlipSprite(b, sprite); SetBlipColour(b, color); SetBlipScale(b, scale or 0.85); SetBlipAsShortRange(b, true)
  BeginTextCommandSetBlipName('STRING'); AddTextComponentSubstringPlayerName(name); EndTextCommandSetBlipName(b)
  blips[#blips + 1] = b
end

local function build()
  for _, b in ipairs(blips) do RemoveBlip(b) end
  blips = {}
  if zoneBlip then RemoveBlip(zoneBlip); zoneBlip = nil end
  if KC.enabled('zone') then
    local z = Config.Zone
    zoneBlip = AddBlipForRadius(z.center[1], z.center[2], z.center[3], z.radius)
    SetBlipColour(zoneBlip, 1); SetBlipAlpha(zoneBlip, 70)
  end
  for _, h in ipairs(Config.Heists) do addBlip(KC.pt(h, 'main'), 52, 5, h.tag, 0.7) end
  for _, l in ipairs(Config.Launderers) do addBlip(KC.pt(l, 'main'), 108, 2, 'Launderer', 0.8) end
  for _, p in ipairs(Config.Places) do
    local sprite, color = 40, 2
    if p.type == 'police' then sprite, color = 60, 3 elseif p.type == 'gang' then sprite, color = 84, 1 end
    addBlip(KC.pt(p, 'main'), sprite, color, p.tag, 0.9)
  end
end

RegisterNetEvent('mdk:client:points', function(o)
  for k in pairs(KC.overrides) do KC.overrides[k] = nil end
  for k, v in pairs(o or {}) do KC.overrides[k] = v end
  build()
end)
KC.onState[#KC.onState + 1] = function() if not KC._built then KC._built = true; build() end end
KC.cleanup[#KC.cleanup + 1] = function() for _, b in ipairs(blips) do RemoveBlip(b) end end
