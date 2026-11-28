-- النواة: سجل الوحدات، بوابة الإجراءات الوحيدة، الإعدادات المحفوظة، الحالة للواجهة، ولوحة التحكم.
K = { actions = {}, owner = {}, providers = {}, hooks = {}, P = {}, saves = {} }
local enabled, modCfg, settingDef = {}, {}, {}
local stores

-- ===== الوحدات: مفعّلة/متوقفة (تنحفظ) =====
do
  local saved = GetResourceKvpString('mdk_modules')
  local t = saved and json.decode(saved) or {}
  for _, m in ipairs(Config.Modules) do
    modCfg[m.id] = m
    if t[m.id] == nil then enabled[m.id] = m.default else enabled[m.id] = t[m.id] == true end
  end
end
function K.enabled(id) return enabled[id] == true end

-- ===== الإعدادات المحفوظة (تتطبق على Config مباشرة) =====
local function pathGet(path)
  local t = Config
  for i = 1, #path - 1 do t = t[path[i]] end
  return t[path[#path]]
end
local function pathSet(path, v)
  local t = Config
  for i = 1, #path - 1 do t = t[path[i]] end
  t[path[#path]] = v
end
local overrides = {}
do
  local saved = GetResourceKvpString('mdk_settings')
  overrides = saved and json.decode(saved) or {}
  for _, d in ipairs(Config.Settings) do
    settingDef[d.id] = d
    local v = overrides[d.id]
    if v ~= nil and ((d.type == 'bool' and type(v) == 'boolean') or (d.type == 'number' and type(v) == 'number' and v >= d.min and v <= d.max)) then pathSet(d.path, v) end
  end
end
local function settingsList()
  local list = {}
  for _, d in ipairs(Config.Settings) do
    list[#list + 1] = { id = d.id, module = d.module, label = d.label, type = d.type, min = d.min, max = d.max, step = d.step, value = pathGet(d.path) }
  end
  return list
end
K.overrides = overrides

-- ===== أدوات عامة =====
function K.getP(src)
  local p = K.P[src]
  if not p then p = { masked = false }; K.P[src] = p end
  return p
end
function K.clamp(v, a, b) return math.max(a, math.min(b, v)) end
function K.isAdmin(src) return IsPlayerAceAllowed(src, Config.AdminAce) end
function K.isDev(src) return IsPlayerAceAllowed(src, Config.DevAce) end
function K.isCop(src) return Bridge.isCop(src) end
function K.getCash(src) return Bridge.getCash(src) end
function K.addCash(src, n) return Bridge.addCash(src, n) end
function K.removeCash(src, n) return Bridge.removeCash(src, n) end
function K.thread(ms, fn) CreateThread(function() while true do Wait(ms); fn() end end) end
function K.onSave(fn) K.saves[#K.saves + 1] = fn end

-- ===== الإجراءات والبوابة =====
function K.action(module, name, fn) K.actions[name] = fn; K.owner[name] = module end
function K.provide(module, fn) K.providers[module] = fn end
function K.on(name, fn) K.hooks[name] = fn end
function K.incident(kind, coords, extra) if enabled.press and K.hooks.incident then return K.hooks.incident(kind, coords, extra) end end
function K.addXP(src, n) if enabled.police and K.hooks.xp then K.hooks.xp(src, n) end end

function K.pushState(src)
  local mods, list = {}, {}
  for _, m in ipairs(Config.Modules) do list[#list + 1] = { id = m.id, icon = m.icon, label = m.label, desc = m.desc, enabled = enabled[m.id] } end
  for id, fn in pairs(K.providers) do if enabled[id] then mods[id] = fn(src) end end
  TriggerClientEvent('mdk:client:state', src, {
    id = src, name = Bridge.name(src), fw = Bridge.fw, cash = Bridge.getCash(src), admin = K.isAdmin(src), dev = K.isDev(src),
    modules = list, mods = mods,
  })
end
function K.say(src, msg, kind)
  TriggerClientEvent('mdk:client:notify', src, msg, kind or 'info')
  K.pushState(src)
end
function K.doc(src, title, lines) TriggerClientEvent('mdk:client:doc', src, title, lines) end
function K.list(src, title, items) TriggerClientEvent('mdk:client:list', src, title, items) end
function K.banner(title, text, kind) TriggerClientEvent('mdk:client:banner', -1, title, text, kind or 'news') end

RegisterNetEvent('mdk:server:action', function(name, args)
  local src = source
  if type(name) ~= 'string' or not K.actions[name] then return Sec.strike(src, 'bad action') end
  if not Sec.rate(src, 'act', 250) then return end
  local owner = K.owner[name]
  if owner ~= 'core' and not enabled[owner] then return K.say(src, 'هذا السكربت متوقف من لوحة التحكم.', 'alert') end
  local clean = {}
  if type(args) == 'table' then
    for i = 1, math.min(#args, 12) do
      local v = args[i]
      if type(v) == 'string' or type(v) == 'number' then clean[#clean + 1] = tostring(v):sub(1, 120) end
    end
  end
  K.actions[name](src, clean)
end)

RegisterNetEvent('mdk:server:state', function()
  local src = source
  if Sec.rate(src, 'state', 500) then
    K.pushState(src)
    TriggerClientEvent('mdk:client:settings', src, settingsList())
    if K.syncClient then K.syncClient(src) end
  end
end)

-- ===== لوحة التحكم (للأدمن فقط) =====
local function adminOnly(src)
  if not K.isAdmin(src) then Sec.strike(src, 'admin action without permission'); return false end
  return true
end

K.action('core', 'admin_module', function(src, args)
  if not adminOnly(src) or not Sec.rate(src, 'admin', 400) then return end
  local id, on = args[1], args[2] == 'true' or args[2] == '1'
  if not modCfg[id] then return end
  enabled[id] = on
  local t = {}
  for k, v in pairs(enabled) do t[k] = v end
  SetResourceKvp('mdk_modules', json.encode(t))
  TriggerClientEvent('mdk:client:modules', -1, id, on)
  for _, idStr in ipairs(GetPlayers()) do K.pushState(tonumber(idStr)) end
  K.say(src, ('%s: %s'):format(modCfg[id].label, on and 'تم التفعيل' or 'تم الإيقاف'), on and 'good' or 'info')
end)

K.action('core', 'admin_set', function(src, args)
  if not adminOnly(src) or not Sec.rate(src, 'admin', 400) then return end
  local d = settingDef[args[1] or '']
  if not d then return Sec.strike(src, 'bad setting id') end
  local v
  if d.type == 'bool' then v = (args[2] == 'true' or args[2] == '1')
  else
    v = tonumber(args[2])
    if not v or v < d.min or v > d.max then return K.say(src, ('القيمة لازم تكون بين %s و %s'):format(d.min, d.max), 'alert') end
  end
  pathSet(d.path, v)
  overrides[d.id] = v
  SetResourceKvp('mdk_settings', json.encode(overrides))
  TriggerClientEvent('mdk:client:settings', -1, settingsList())
  K.say(src, ('تم حفظ: %s'):format(d.label), 'good')
end)

K.action('core', 'admin_reset', function(src, args)
  if not adminOnly(src) then return end
  overrides = {}
  K.overrides = overrides
  SetResourceKvp('mdk_settings', json.encode(overrides))
  K.say(src, 'الإعدادات المحفوظة انمسحت. أعد تشغيل المورد لتطبيق الافتراضي.', 'info')
end)

K.action('core', 'admin_stats', function(src)
  if not adminOnly(src) then return end
  local stats = { players = #GetPlayers() }
  for id, fn in pairs(K.providers) do
    if enabled[id] and K.statsOf and K.statsOf[id] then stats[id] = K.statsOf[id]() end
  end
  TriggerClientEvent('mdk:client:stats', src, stats)
end)
K.statsOf = {}

-- ===== تخزين دائم لكل لاعب ولكل وحدة (KVP باسم هويته) =====
stores = {}
function K.store(module, src)
  local k = src .. ':' .. module
  if stores[k] then return stores[k] end
  local raw = GetResourceKvpString(('mdk_%s_%s'):format(module, Bridge.id(src)))
  local t = raw and json.decode(raw) or nil
  if type(t) ~= 'table' then t = {} end
  stores[k] = t
  return t
end
function K.saveStore(module, src)
  local t = stores[src .. ':' .. module]
  if t then SetResourceKvp(('mdk_%s_%s'):format(module, Bridge.id(src)), json.encode(t)) end
end
K.onSave(function() for k in pairs(stores) do local src, m = k:match('^(%d+):(.+)$'); K.saveStore(m, tonumber(src)) end end)

-- ===== الحفظ =====
K.thread(300000, function() for _, fn in ipairs(K.saves) do fn() end end)
AddEventHandler('onResourceStop', function(res)
  if res == GetCurrentResourceName() then for _, fn in ipairs(K.saves) do fn() end end
end)
K.dropHandlers = {}
function K.onPlayerDrop(fn) K.dropHandlers[#K.dropHandlers + 1] = fn end
AddEventHandler('playerDropped', function()
  local src = source
  for _, fn in ipairs(K.dropHandlers) do fn(src) end
  for k in pairs(stores) do if k:find('^' .. src .. ':') then K.saveStore(k:match(':(.+)$'), src); stores[k] = nil end end
  K.P[src] = nil
  Sec.clear(src)
end)

if __MDK_TEST then __MT = { enabled = enabled, overrides = overrides, stores = stores } end
