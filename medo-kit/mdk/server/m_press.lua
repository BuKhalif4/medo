-- سكربت الصحافة: الصحفي يصوّر أحداثاً حقيقية سجلها السيرفر وينشرها، والنشر يسخّن الحقيبة.
local incidents, incSeq = {}, 0
local pressDuty = {}
local KIND = { heist = 'سطو مسلح', drop = 'حقيبة مرمية', arrest = 'عملية قبض', bribe = 'رشوة', raid = 'مداهمة' }

K.on('incident', function(kind, coords, extra)
  incSeq = incSeq + 1
  extra = extra or {}
  extra.id, extra.kind, extra.c, extra.t = incSeq, kind, coords, os.time()
  incidents[incSeq] = extra
  incidents[incSeq - Config.IncidentKeep] = nil
  return incSeq
end)
K.on('incidentBag', function(id, bag) if incidents[id] then incidents[id].bag, incidents[id].serial = bag, bag.serial end end)

K.action('press', 'press', function(src)
  if Bridge.fw ~= 'standalone' then return end
  if not IsPlayerAceAllowed(src, 'mdk.press') then return K.say(src, 'ما عندك صلاحية.') end
  pressDuty[src] = not pressDuty[src] or nil
  K.say(src, pressDuty[src] and 'دخلت كصحفي. قرّب من حدث وصوّره، ثم انشر الخبر.' or 'خرجت من الصحافة.')
end)

K.action('press', 'snap', function(src)
  if not pressDuty[src] or not Sec.rate(src, 'snap', 3000) then return end
  local me, now, best, bestD = Sec.coords(src), os.time(), nil, Config.SnapRange
  if not me then return end
  for _, inc in pairs(incidents) do
    if not inc.published and now - inc.t <= Config.IncidentTTL then
      local d = Sec.dist(me, inc.c)
      if d <= bestD then best, bestD = inc, d end
    end
  end
  if not best then return K.say(src, 'ما فيه حدث قريب يستاهل التصوير.') end
  local p = K.getP(src)
  p.shots = p.shots or {}
  p.shots[best.id] = true
  K.say(src, ('صوّرت: %s. اكتب العنوان وانشر قبل ما يبرد الخبر.'):format(KIND[best.kind] or best.kind), 'good')
end)

K.action('press', 'publish', function(src, args)
  if not pressDuty[src] or not Sec.rate(src, 'publish', 5000) then return end
  local p = K.getP(src)
  local pick
  for id in pairs(p.shots or {}) do
    local inc = incidents[id]
    if inc and not inc.published and os.time() - inc.t <= Config.IncidentTTL then
      if not pick or inc.t > pick.t then pick = inc end
    end
  end
  if not pick then return K.say(src, 'ما عندك صور صالحة للنشر. صوّر حدثاً أولاً.') end
  local headline = table.concat(args, ' '):gsub('%^%d', ''):sub(1, Config.HeadlineMax)
  if #headline < 3 then return K.say(src, 'اكتب عنواناً.') end
  pick.published = true
  p.shots[pick.id] = nil
  local tail = pick.kind == 'bribe' and pick.cop and (' | الشرطي: ' .. pick.cop) or ''
  K.banner('الصحافة - ' .. Bridge.name(src), headline .. tail, 'news')
  if pick.bag then
    pick.bag.heat = math.min(100, pick.bag.heat + Config.PressHeat)
    K.addHist(pick.bag, 'صحفي ' .. Bridge.name(src), 'نشر خبراً: ' .. headline, '')
  end
  local reward = math.min(Config.PressRewardMax, 250 + (pick.heat or 0) * 60)
  K.addCash(src, reward)
  K.say(src, ('تم النشر! مكافأتك %d.'):format(reward), 'good')
end)

K.onPlayerDrop(function(src) pressDuty[src] = nil end)
K.provide('press', function(src)
  return { on = pressDuty[src] == true, allowed = Bridge.fw == 'standalone' and IsPlayerAceAllowed(src, 'mdk.press') or Bridge.fw ~= 'standalone' }
end)
K.statsOf.press = function()
  local fresh, journalists = 0, 0
  for _, inc in pairs(incidents) do if not inc.published and os.time() - inc.t <= Config.IncidentTTL then fresh = fresh + 1 end end
  for _ in pairs(pressDuty) do journalists = journalists + 1 end
  return { freshIncidents = fresh, journalists = journalists }
end
if __MDK_TEST then __MT.press = { incidents = incidents, duty = pressDuty } end
