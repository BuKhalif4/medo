-- جسر الفريمورك: يشتغل مع Qbox / QBCore / ESX، وإلا يستخدم رصيد مستقل للتجربة.
Bridge = {}

local fw = 'standalone'
if GetResourceState('qbx_core') == 'started' then fw = 'qbx'
elseif GetResourceState('qb-core') == 'started' then fw = 'qb'
elseif GetResourceState('es_extended') == 'started' then fw = 'esx' end
Bridge.fw = fw
print(('^3[mdk]^7 framework: %s'):format(fw))

local QB, ESX
if fw == 'qb' then QB = exports['qb-core']:GetCoreObject() end
if fw == 'esx' then ESX = exports['es_extended']:getSharedObject() end

function Bridge.id(src)
  local id = GetPlayerIdentifierByType and GetPlayerIdentifierByType(src, 'license')
  return id or ('src:' .. tostring(src))
end

function Bridge.name(src) return GetPlayerName(src) or ('#' .. tostring(src)) end

local function qbPlayer(src)
  if fw == 'qbx' then return exports.qbx_core:GetPlayer(src) end
  if fw == 'qb' then return QB.Functions.GetPlayer(src) end
end

local function kvpBalance(src)
  return tonumber(GetResourceKvpString('bal_' .. Bridge.id(src))) or 0
end

function Bridge.addCash(src, amount)
  amount = math.floor(amount)
  if amount <= 0 then return false end
  if fw == 'qbx' or fw == 'qb' then
    local pl = qbPlayer(src)
    return pl ~= nil and pl.Functions.AddMoney('cash', amount, 'bagcity') or false
  elseif fw == 'esx' then
    local xp = ESX.GetPlayerFromId(src)
    if not xp then return false end
    xp.addMoney(amount)
    return true
  end
  SetResourceKvp('bal_' .. Bridge.id(src), tostring(kvpBalance(src) + amount))
  return true
end

function Bridge.getBank(src)
  if fw == 'qbx' or fw == 'qb' then
    local pl = qbPlayer(src)
    return pl and pl.PlayerData.money.bank or 0
  elseif fw == 'esx' then
    local xp = ESX.GetPlayerFromId(src)
    return xp and xp.getAccount('bank').money or 0
  end
  return kvpBalance(src)
end

function Bridge.removeBank(src, amount)
  amount = math.floor(amount)
  if amount <= 0 or Bridge.getBank(src) < amount then return false end
  if fw == 'qbx' or fw == 'qb' then
    return qbPlayer(src).Functions.RemoveMoney('bank', amount, 'bagcity-bribe')
  elseif fw == 'esx' then
    ESX.GetPlayerFromId(src).removeAccountMoney('bank', amount)
    return true
  end
  SetResourceKvp('bal_' .. Bridge.id(src), tostring(kvpBalance(src) - amount))
  return true
end

function Bridge.getCash(src)
  if fw == 'qbx' or fw == 'qb' then
    local pl = qbPlayer(src)
    return pl and pl.PlayerData.money.cash or 0
  elseif fw == 'esx' then
    local xp = ESX.GetPlayerFromId(src)
    return xp and xp.getMoney() or 0
  end
  return kvpBalance(src)
end

function Bridge.removeCash(src, amount)
  amount = math.floor(amount)
  if amount < 0 or Bridge.getCash(src) < amount then return false end
  if amount == 0 then return true end
  if fw == 'qbx' or fw == 'qb' then
    return qbPlayer(src).Functions.RemoveMoney('cash', amount, 'bagcity-gear')
  elseif fw == 'esx' then
    ESX.GetPlayerFromId(src).removeMoney(amount)
    return true
  end
  SetResourceKvp('bal_' .. Bridge.id(src), tostring(kvpBalance(src) - amount))
  return true
end

function Bridge.isCop(src)
  if fw == 'qbx' or fw == 'qb' then
    local pl = qbPlayer(src)
    local job = pl and pl.PlayerData.job
    return job ~= nil and job.type == 'leo' and job.onduty == true
  elseif fw == 'esx' then
    local xp = ESX.GetPlayerFromId(src)
    return xp ~= nil and xp.job ~= nil and xp.job.name == 'police'
  end
  return false
end
