-- مال اللاعب (كاش) محفوظ بالـ KVP باسم هويته. لعبة مستقلة بدون فريمورك.
Bridge = {}

function Bridge.id(src)
  local id = GetPlayerIdentifierByType and GetPlayerIdentifierByType(src, 'license')
  return id or ('src:' .. tostring(src))
end

function Bridge.name(src) return GetPlayerName(src) or ('#' .. tostring(src)) end

local function bal(src) return tonumber(GetResourceKvpString('cash_' .. Bridge.id(src))) or 0 end
local function setBal(src, v) SetResourceKvp('cash_' .. Bridge.id(src), tostring(math.floor(v))) end

function Bridge.getCash(src) return bal(src) end

function Bridge.addCash(src, amount)
  amount = math.floor(amount)
  if amount <= 0 then return false end
  setBal(src, bal(src) + amount)
  return true
end

function Bridge.removeCash(src, amount)
  amount = math.floor(amount)
  if amount < 0 or bal(src) < amount then return false end
  if amount > 0 then setBal(src, bal(src) - amount) end
  return true
end
