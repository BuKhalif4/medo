-- المجموعة: حتى 3 لاعبين (دعوة، قبول، مغادرة). أعضاء المجموعة يظهرون لبعض على الخريطة.
local Actions, say = MD.Actions, MD.say
local parties, partyOf, invites, seq = {}, {}, {}, 0   -- partyOf[src] = id ; invites[target] = { from, exp }

function MD.partyOf(src)
  local id = partyOf[src]
  local out = { members = {}, invites = {} }
  if id and parties[id] then
    for _, m in ipairs(parties[id]) do out.members[#out.members + 1] = { id = m, name = Bridge.name(m) } end
  end
  local inv = invites[src]
  if inv and inv.exp > os.time() then out.invites[1] = { from = inv.from, name = Bridge.name(inv.from) } end
  return out
end

local function broadcast(id)
  for _, m in ipairs(parties[id] or {}) do TriggerClientEvent('medo:client:party', m, MD.partyOf(m).members) end
end

local function leave(src)
  local id = partyOf[src]
  if not id or not parties[id] then return end
  for i, m in ipairs(parties[id]) do if m == src then table.remove(parties[id], i) break end end
  partyOf[src] = nil
  TriggerClientEvent('medo:client:party', src, {})
  if #parties[id] <= 1 then
    for _, m in ipairs(parties[id]) do partyOf[m] = nil; TriggerClientEvent('medo:client:party', m, {}) end
    parties[id] = nil
  else broadcast(id) end
end

function Actions.players(src)
  if not Sec.rate(src, 'players', 2000) then return end
  local list = {}
  for _, idStr in ipairs(GetPlayers()) do
    local s = tonumber(idStr)
    if s and s ~= src and #list < 100 then list[#list + 1] = { id = s, name = Bridge.name(s), inParty = partyOf[s] ~= nil } end
  end
  TriggerClientEvent('medo:client:players', src, list)
end

function Actions.party_invite(src, args)
  if not Sec.rate(src, 'party', 800) then return end
  local target = tonumber(args[1])
  if not target or target == src or not GetPlayerName(target) then return end
  if partyOf[target] then return say(src, 'هذا اللاعب في مجموعة أصلاً.') end
  local id = partyOf[src]
  if id and #parties[id] >= Config.PartyMax then return say(src, ('المجموعة ممتلئة (%d).'):format(Config.PartyMax)) end
  invites[target] = { from = src, exp = os.time() + 60 }
  say(src, ('أرسلت دعوة إلى %s.'):format(Bridge.name(target)))
  say(target, ('%s يدعوك لمجموعته. افتح قائمة المجموعة لتقبل.'):format(Bridge.name(src)), 'alert')
end

function Actions.party_accept(src, args)
  if not Sec.rate(src, 'party', 800) then return end
  local inv = invites[src]
  if not inv or inv.exp < os.time() then return say(src, 'ما عندك دعوة صالحة.') end
  local from = inv.from
  if not GetPlayerName(from) then invites[src] = nil; return end
  if partyOf[src] then return say(src, 'أنت في مجموعة أصلاً.') end
  local id = partyOf[from]
  if not id then seq = seq + 1; id = seq; parties[id] = { from }; partyOf[from] = id end
  if #parties[id] >= Config.PartyMax then invites[src] = nil; return say(src, 'المجموعة ممتلئة.') end
  parties[id][#parties[id] + 1] = src
  partyOf[src], invites[src] = id, nil
  broadcast(id)
  say(src, 'انضممت للمجموعة.', 'good')
end

function Actions.party_leave(src)
  if not Sec.rate(src, 'party', 800) then return end
  leave(src)
  say(src, 'غادرت المجموعة.')
end

local oldDrop = MD.onDrop
AddEventHandler('playerDropped', function()
  local src = source
  invites[src] = nil
  leave(src)
end)
if __MEDO_TEST then __MT.parties = parties; __MT.partyOf = partyOf end
