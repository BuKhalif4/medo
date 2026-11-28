-- عميل المجموعة: علامات أعضاء المجموعة على الخريطة.
local partyBlips = {}
RegisterNetEvent('mdk:client:party', function(members)
  for _, b in ipairs(partyBlips) do RemoveBlip(b) end
  partyBlips = {}
  for _, m in ipairs(members or {}) do
    if m.id ~= GetPlayerServerId(PlayerId()) then
      local pl = GetPlayerFromServerId(m.id)
      if pl and pl ~= -1 then
        local b = AddBlipForEntity(GetPlayerPed(pl))
        SetBlipColour(b, 38); SetBlipScale(b, 0.8)
        BeginTextCommandSetBlipName('STRING'); AddTextComponentSubstringPlayerName(m.name or 'Party'); EndTextCommandSetBlipName(b)
        partyBlips[#partyBlips + 1] = b
      end
    end
  end
end)
KC.cleanup[#KC.cleanup + 1] = function() for _, b in ipairs(partyBlips) do RemoveBlip(b) end end
