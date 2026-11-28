-- استئجار المركبات من موقف السيارات (مجاناً). مركبة واحدة لكل لاعب، والقديمة تنحذف.
local Actions, say = MD.Actions, MD.say
local rentals, last = {}, {}

local function remove(src)
  local v = rentals[src]
  if v and DoesEntityExist(v) then DeleteEntity(v) end
  rentals[src] = nil
end

function Actions.rent(src)
  if not Sec.rate(src, 'rent', 1500) then return end
  local p = MD.getP(src)
  if p.jailUntil then return end
  local spot = MD.nearType(src, 'terminal')
  if not spot then return say(src, 'استئجار المركبات من موقف السيارات.') end
  local left = (last[src] or 0) - os.time()
  if left > 0 then return say(src, ('انتظر %d ثانية.'):format(left)) end
  last[src] = os.time() + Config.Vehicles.cooldown
  remove(src)
  local pt = MD.point(spot)
  local model = p.role == 'police' and Config.Vehicles.police or Config.Vehicles.civil
  local veh = CreateVehicleServerSetter(joaat(model), 'automobile', pt[1] + 4.0, pt[2], pt[3], spot.heading or 0.0)
  if not veh or veh == 0 then return say(src, 'تعذّر تجهيز المركبة.', 'alert') end
  rentals[src] = veh
  TriggerClientEvent('medo:client:vehicle', src, NetworkGetNetworkIdFromEntity(veh))
  say(src, 'استأجرت مركبة. ادخل فيها.', 'good')
end

function Actions.return_vehicle(src)
  if not Sec.rate(src, 'rent', 1500) or not rentals[src] then return end
  remove(src)
  say(src, 'أرجعت المركبة.', 'info')
end

AddEventHandler('playerDropped', function() remove(source); last[source] = nil end)
if __MEDO_TEST then __MT.rentals = rentals end
