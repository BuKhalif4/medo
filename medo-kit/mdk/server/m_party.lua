-- سكربت المجموعة: دعوة وقبول ومغادرة، حتى Config.PartyMax لاعبين، وعلامات الأعضاء على الخريطة.
local parties, partyOf, invites, seq = {}, {}, {}, 0

local function members(src)
  local out = {}
  local id = partyOf[src]
  if id and parties[id] then for _, m in ipairs(parties[id]) do out[#out + 1] = { id = m, name = Bridge.name(m) } end end
  return out
end
local function broadcast(id) for _, m in ipairs(parties[id] or {}) do TriggerClientEvent('mdk:client:party', m, members(m)) end end
local function leave(src)
  local id = partyOf[src]
  if not id or not parties[id] then return end
  for i, m in ipairs(parties[id]) do if m == src then table.remove(parties[id], i) break end end
  partyOf[src] = nil
  TriggerClientEvent('mdk:client:party', src, {})
  if #parties[id] <= 1 then
    for _, m in ipairs(parties[id]) do partyOf[m] = nil; TriggerClientEvent('mdk:client:party', m, {}) end
    parties[id] = nil
  else broadcast(id) end
end

K.action('party', 'players', function(src)
  if not Sec.rate(src, 'players', 2000) then return end
  local list = {}
  for _, idStr in ipairs(GetPlayers()) do
    local s = tonumber(idStr)
    if s and s ~= src and #list < 100 then list[#list + 1] = { id = s, name = Bridge.name(s), inParty = partyOf[s] ~= nil } end
  end
  TriggerClientEvent('mdk:client:players', src, list)
end)

K.action('party', 'party_invite', function(src, args)
  if not Sec.rate(src, 'party', 800) then return end
  local target = tonumber(args[1])
  if not target or target == src or not GetPlayerName(target) then return end
  if partyOf[target] then return K.say(src, 'هذا اللاعب في مجموعة أصلاً.') end
  local id = partyOf[src]
  if id and #parties[id] >= Config.PartyMax then return K.say(src, ('المجموعة ممتلئة (%d).'):format(Config.PartyMax)) end
  invites[target] = { from = src, exp = os.time() + 60 }
  K.say(src, ('أرسلت دعوة إلى %s.'):format(Bridge.name(target)))
  K.say(target, ('%s يدعوك لمجموعته. افتح صفحة المجموعة لتقبل.'):format(Bridge.name(src)), 'alert')
end)

K.action('party', 'party_accept', function(src)
  if not Sec.rate(src, 'party', 800) then return end
  local inv = invites[src]
  if not inv or inv.exp < os.time() then return K.say(src, 'ما عندك دعوة صالحة.') end
  local from = inv.from
  if not GetPlayerName(from) then invites[src] = nil; return end
  if partyOf[src] then return K.say(src, 'أنت في مجموعة أصلاً.') end
  local id = partyOf[from]
  if not id then seq = seq + 1; id = seq; parties[id] = { from }; partyOf[from] = id end
  if #parties[id] >= Config.PartyMax then invites[src] = nil; return K.say(src, 'المجموعة ممتلئة.') end
  parties[id][#parties[id] + 1] = src
  partyOf[src], invites[src] = id, nil
  broadcast(id)
  K.say(src, 'انضممت للمجموعة.', 'good')
end)

K.action('party', 'party_leave', function(src)
  if not Sec.rate(src, 'party', 800) then return end
  leave(src)
  K.say(src, 'غادرت المجموعة.')
end)

K.onPlayerDrop(function(src) invites[src] = nil; leave(src) end)

K.provide('party', function(src)
  local inv = invites[src]
  return { members = members(src), max = Config.PartyMax, invites = (inv and inv.exp > os.time()) and { { from = inv.from, name = Bridge.name(inv.from) } } or {} }
end)
K.statsOf.party = function() local n = 0; for _ in pairs(parties) do n = n + 1 end; return { parties = n } end
if __MDK_TEST then __MT.partyOf = partyOf; __MT.parties = parties end
