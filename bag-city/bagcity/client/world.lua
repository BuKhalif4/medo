-- إزالة البوتات: مشاة وسيارات وشرطة اللعبة. الشرطة الحقيقية هم اللاعبون فقط.
-- يشتغل مع إعداد السيرفر: set onesync_population false (السيرفر ما يصنعها أصلاً)

local W = Config.World

CreateThread(function()
  if W.noPolice then
    SetMaxWantedLevel(0)
    SetCreateRandomCops(false)
    SetCreateRandomCopsNotOnScenarios(false)
    SetCreateRandomCopsOnScenarios(false)
    for i = 1, 15 do EnableDispatchService(i, false) end
  end
  SetGarbageTrucks(false)
  SetRandomBoats(false)
  SetAmbientPedsDropMoney(false)

  while true do
    if W.noPeds then
      SetPedDensityMultiplierThisFrame(0.0)
      SetScenarioPedDensityMultiplierThisFrame(0.0, 0.0)
    end
    if W.noTraffic then
      SetVehicleDensityMultiplierThisFrame(0.0)
      SetRandomVehicleDensityMultiplierThisFrame(0.0)
      SetParkedVehicleDensityMultiplierThisFrame(0.0)
    end
    if W.noPolice then
      local id = PlayerId()
      if GetPlayerWantedLevel(id) > 0 then
        SetPlayerWantedLevel(id, 0, false)
        SetPlayerWantedLevelNow(id, false)
      end
    end
    Wait(0)
  end
end)
