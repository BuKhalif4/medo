-- طبقة الحماية: كل شيء يمر من هنا. السيرفر هو الحاكم دائماً.
Sec = {}

local buckets, strikes, strikeAt = {}, {}, {}

function Sec.log(msg)
  print(('^3[mdk]^7 %s'):format(msg))
  if Config.Webhook and Config.Webhook ~= '' then
    PerformHttpRequest(Config.Webhook, function() end, 'POST',
      json.encode({ content = ('[mdk] %s'):format(msg) }), { ['Content-Type'] = 'application/json' })
  end
end

-- حد معدل: يرجع true لو مسموح
function Sec.rate(src, key, ms)
  local now = GetGameTimer()
  local b = buckets[src]
  if not b then b = {}; buckets[src] = b end
  if now - (b[key] or -1e9) < ms then return false end
  b[key] = now
  return true
end

-- مخالفة: تتراكم ثم تطرد
function Sec.strike(src, reason)
  -- مخالفة واحدة تنمسح كل 5 دقائق، حتى ما ينطرد لاعب سليم بسبب لاق متفرق
  local now = GetGameTimer()
  local n = strikes[src] or 0
  if n > 0 then n = math.max(0, n - math.floor((now - (strikeAt[src] or now)) / 300000)) end
  strikes[src], strikeAt[src] = n + 1, now
  Sec.log(('مخالفة %d/%d من %s (%s): %s'):format(strikes[src], Config.MaxStrikes, GetPlayerName(src) or '?', src, reason))
  if strikes[src] >= Config.MaxStrikes then
    DropPlayer(src, 'Kicked by server protection.')
  end
end

function Sec.clear(src) buckets[src] = nil; strikes[src] = nil; strikeAt[src] = nil end

function Sec.dist(a, b)
  local dx, dy, dz = a[1] - b[1], a[2] - b[2], a[3] - b[3]
  return math.sqrt(dx * dx + dy * dy + dz * dz)
end

-- موقع اللاعب كما يعرفه السيرفر (مو اللي يرسله اللاعب)
function Sec.coords(src)
  local ped = GetPlayerPed(src)
  if not ped or ped == 0 then return nil end
  local c = GetEntityCoords(ped)
  return { c.x, c.y, c.z }
end

function Sec.near(src, target, range)
  local c = Sec.coords(src)
  return c ~= nil and Sec.dist(c, target) <= range
end

function Sec.between(a, b, range)
  local ca, cb = Sec.coords(a), Sec.coords(b)
  return ca ~= nil and cb ~= nil and Sec.dist(ca, cb) <= range
end

-- أفخاخ: أسماء مغرية ما يستدعيها أحد إلا هاكر
for _, name in ipairs(Config.Honeypots) do
  RegisterNetEvent(name, function()
    local src = source
    Sec.log(('فخ: %s (%s) استدعى %s'):format(GetPlayerName(src) or '?', src, name))
    if Config.HoneypotKick then DropPlayer(src, 'Kicked by server protection.') end
  end)
end
