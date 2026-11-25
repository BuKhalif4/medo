-- ميدو (MEDO): لعبة جريمة وشرطة على FiveM. كل الإعدادات هنا.
Config = {}

-- ===== عام =====
Config.Name         = 'MEDO'
Config.Webhook      = ''
Config.HoneypotKick = true
Config.MaxStrikes   = 5

-- ===== الخريطة =====
-- المدينة (يبقى الوسم الجنائي ما دمت فيها)، وخارجها صحراء. المنطقة الكبرى = حدود اللعب كلها.
Config.City = { center = {300.0, -900.0, 30.0}, radius = 1500.0 }
Config.Zone = { center = {300.0, -900.0, 30.0}, radius = 3200.0, warnSeconds = 12, safe = {441.2, -975.0, 30.7} }

-- ===== الأماكن =====
-- type: dealer | police | sell | store. الإحداثيات تقريبية: عدّلها من قائمة التجربة (تحديد نقطة) وقوفاً في المكان.
Config.Spots = {
  { id = 'dealer',  type = 'dealer', label = 'تاجر السوق السوداء', tag = 'Black Market', coords = {131.5, -1052.0, 29.2}, heading = 160.0, ped = 's_m_y_dealer_01', radius = 2.2 },
  { id = 'pd',      type = 'police', label = 'مكتب الشرطة',        tag = 'Police Desk',  coords = {441.2, -978.0, 30.7},  heading = 180.0, ped = 's_m_y_cop_01',    radius = 2.2 },
  { id = 'sell1',   type = 'sell',   label = 'نقطة بيع الغنائم 1', tag = 'Sell Point',   coords = {-44.0, -1747.0, 29.4}, heading = 50.0,  ped = 'a_m_y_business_01', radius = 2.4 },
  { id = 'sell2',   type = 'sell',   label = 'نقطة بيع الغنائم 2', tag = 'Sell Point',   coords = {718.0, -976.0, 24.1},  heading = 270.0, ped = 'a_m_y_business_01', radius = 2.4 },
  { id = 'terminal1', type = 'terminal', label = 'موقف المركبات (استئجار)', tag = 'Parking', coords = {408.0, -1010.0, 29.3}, heading = 90.0, radius = 2.6 },
  { id = 'store1',  type = 'store',  label = 'بقالة ستروبري',      tag = 'Store',        coords = {25.7, -1347.3, 29.5},  clerk = {24.4, -1347.3, 29.5, 270.0}, radius = 3.0, cooldown = 600,  rolls = 3, tier = 1 },
  { id = 'store2',  type = 'store',  label = 'بقالة فاينوود',      tag = 'Store',        coords = {373.8, 325.9, 103.6},  clerk = {372.6, 326.9, 103.6, 255.0}, radius = 3.0, cooldown = 600,  rolls = 3, tier = 1 },
  { id = 'store3',  type = 'store',  label = 'بقالة دافيس',        tag = 'Store',        coords = {-47.0, -1757.5, 29.4}, clerk = {-47.3, -1758.7, 29.4, 50.0}, radius = 3.0, cooldown = 600,  rolls = 3, tier = 1 },
  { id = 'jewel',   type = 'store',  label = 'محل مجوهرات',        tag = 'Jewelry',      coords = {-622.5, -230.9, 38.1}, clerk = {-621.2, -228.6, 38.1, 215.0}, radius = 4.0, cooldown = 1500, rolls = 5, tier = 2 },
}

-- ===== الغنائم (شبكة الحقيبة): size = {عرض, ارتفاع} =====
Config.Loot = {
  { id = 'phone',    label = 'جوال',         size = {1, 1}, value = 450 },
  { id = 'cash',     label = 'رزمة نقود',    size = {1, 1}, value = 600 },
  { id = 'watch',    label = 'ساعة',        size = {1, 1}, value = 800 },
  { id = 'necklace', label = 'عقد ذهب',      size = {1, 1}, value = 1800 },
  { id = 'laptop',   label = 'لابتوب',       size = {2, 1}, value = 2400 },
  { id = 'jewel',    label = 'طقم مجوهرات',  size = {2, 1}, value = 6500 },
  { id = 'goldbar',  label = 'سبيكة ذهب',    size = {1, 1}, value = 9000 },
  { id = 'painting', label = 'لوحة فنية',    size = {2, 2}, value = 16000 },
}
-- جدول الغنائم لكل درجة محل: id + وزن الحظ
Config.LootTables = {
  [1] = { { 'cash', 50 }, { 'phone', 25 }, { 'watch', 15 }, { 'necklace', 8 }, { 'laptop', 2 } },
  [2] = { { 'watch', 25 }, { 'necklace', 30 }, { 'jewel', 25 }, { 'goldbar', 15 }, { 'painting', 5 } },
}
Config.GridWidth    = 6      -- عرض شبكة الحقيبة
Config.BaseSquares  = 18     -- خانات بدون حقيبة مجهّزة
Config.SellBonus    = 0.05   -- زيادة سعر البيع لكل مستوى سمعة
Config.MaxNotoriety = 5

-- ===== السطو =====
Config.Heist = {
  duration = 12,        -- ثواني النهب
  alarmDelay = 25,      -- ثواني قبل بلاغ الشرطة لو هدّدت الموظف بالسلاح
  aimSeconds = 3,       -- ثواني التصويب على الموظف حتى يرفع يديه
  alert = true,
}

-- ===== الأدوار =====
Config.PoliceCut = 0.10      -- نسبة الشرطي من قيمة غنائم مصادرة
Config.Jail = { seconds = 45, cell = {459.8, -994.0, 24.9}, release = {441.2, -975.0, 30.7} }
Config.SurrenderRange = 25.0 -- أقصى بعد للشرطي حتى تقدر تستسلم
Config.ArrestRange = 3.5

-- ===== العالم =====
Config.World = { noPeds = true, noTraffic = true, noPolice = true }
Config.MaskDrawable = 51
Config.MaskTexture = 0

-- ===== السوق السوداء =====
Config.RefillRate = 0.30
Config.Weapons = {
  { id = 'pistol',  slot = 'secondary', tier = 1, label = 'مسدس',       weapon = 'WEAPON_PISTOL',       price = 2500,   ammo = 48,
    mods = { { id = 'supp', label = 'كاتم صوت', comp = 'COMPONENT_AT_PI_SUPP_02', price = 3500 }, { id = 'clip', label = 'مخزن موسّع', comp = 'COMPONENT_PISTOL_CLIP_02', price = 2500 } } },
  { id = 'smg',     slot = 'primary',   tier = 2, node = 'w_smg',     label = 'رشاش صغير', weapon = 'WEAPON_MICROSMG', price = 18000, ammo = 90,
    mods = { { id = 'clip', label = 'مخزن موسّع', comp = 'COMPONENT_MICROSMG_CLIP_02', price = 4000 }, { id = 'scope', label = 'منظار', comp = 'COMPONENT_AT_SCOPE_MACRO', price = 3000 } } },
  { id = 'shotgun', slot = 'primary',   tier = 2, node = 'w_shotgun', label = 'بامب أكشن', weapon = 'WEAPON_PUMPSHOTGUN', price = 22000, ammo = 24,
    mods = { { id = 'supp', label = 'كاتم صوت', comp = 'COMPONENT_AT_SR_SUPP', price = 5000 } } },
  { id = 'rifle',   slot = 'primary',   tier = 3, node = 'w_rifle',   label = 'بندقية',    weapon = 'WEAPON_CARBINERIFLE', price = 45000, ammo = 120,
    mods = { { id = 'clip', label = 'مخزن موسّع', comp = 'COMPONENT_CARBINERIFLE_CLIP_02', price = 6000 }, { id = 'grip', label = 'مقبض', comp = 'COMPONENT_AT_AR_AFGRIP', price = 3500 }, { id = 'scope', label = 'منظار', comp = 'COMPONENT_AT_SCOPE_MEDIUM', price = 5000 } } },
  { id = 'rpg',     slot = 'primary',   tier = 3, node = 'w_rpg',     label = 'قاذف قنابل', weapon = 'WEAPON_RPG', price = 180000, ammo = 2, limitSeconds = 1800, mods = {} },
}
Config.Gear = {
  { id = 'vest_light',   cat = 'armor', label = 'سترة خفيفة',   price = 4000,   tier = 1, armor = 40 },
  { id = 'vest_plate',   cat = 'armor', label = 'حامل لوحات',   price = 15000,  tier = 1, armor = 70 },
  { id = 'vest_tac',     cat = 'armor', label = 'سترة تكتيكية', price = 70000,  tier = 2, armor = 100, node = 'e_vest' },
  { id = 'vest_heavy',   cat = 'armor', label = 'سترة ثقيلة',   price = 140000, tier = 3, armor = 100, node = 'e_heavy' },
  { id = 'bag_pouch',    cat = 'bag',   label = 'كيس صغير',     price = 5000,   tier = 1, squares = 6 },
  { id = 'bag_satchel',  cat = 'bag',   label = 'حقيبة كتف',    price = 10000,  tier = 1, squares = 12 },
  { id = 'bag_duffle',   cat = 'bag',   label = 'حقيبة رياضية', price = 15000,  tier = 2, squares = 24, node = 'e_duffle' },
  { id = 'bag_backpack', cat = 'bag',   label = 'حقيبة ظهر',    price = 40000,  tier = 3, squares = 36, node = 'e_pack' },
}
Config.Tree = {
  { id = 'w_root',    branch = 'weapons',   label = 'المسدسات',         col = 0, row = 0, root = true },
  { id = 'w_smg',     branch = 'weapons',   label = 'رشاشات صغيرة',     col = 1, row = 0, cost = 20000,  requires = { 'w_root' } },
  { id = 'w_shotgun', branch = 'weapons',   label = 'بنادق بامب',       col = 1, row = 1, cost = 30000,  requires = { 'w_root' } },
  { id = 'w_rifle',   branch = 'weapons',   label = 'بنادق هجومية',     col = 2, row = 0, cost = 70000,  requires = { 'w_smg' } },
  { id = 'w_rpg',     branch = 'weapons',   label = 'قاذف القنابل',     col = 3, row = 0, cost = 250000, requires = { 'w_rifle', 'w_shotgun' } },
  { id = 'e_root',    branch = 'equipment', label = 'المعدات الأساسية',  col = 0, row = 3, root = true },
  { id = 'e_vest',    branch = 'equipment', label = 'سترات تكتيكية',    col = 1, row = 3, cost = 40000,  requires = { 'e_root' } },
  { id = 'e_duffle',  branch = 'equipment', label = 'حقائب رياضية',     col = 1, row = 4, cost = 30000,  requires = { 'e_root' } },
  { id = 'e_heavy',   branch = 'equipment', label = 'سترات ثقيلة',      col = 2, row = 3, cost = 100000, requires = { 'e_vest' } },
  { id = 'e_pack',    branch = 'equipment', label = 'حقائب ظهر',        col = 2, row = 4, cost = 80000,  requires = { 'e_duffle' } },
}

-- ===== رتب الشرطة (تتقدم بنقاط) =====
Config.PoliceRanks = {
  { id = 'patrol', label = 'ضابط دورية', xp = 0,   armor = 50,  kit = { { weapon = 'WEAPON_COMBATPISTOL', ammo = 120, label = 'مسدس قتالي' }, { weapon = 'WEAPON_STUNGUN', ammo = 1, label = 'صاعق' }, { weapon = 'WEAPON_NIGHTSTICK', ammo = 1, label = 'هراوة' } } },
  { id = 'sgt1',   label = 'رقيب أول',   xp = 10,  armor = 75,  kit = { { weapon = 'WEAPON_COMBATPISTOL', ammo = 120, label = 'مسدس قتالي' }, { weapon = 'WEAPON_PUMPSHOTGUN', ammo = 40, label = 'بامب أكشن' }, { weapon = 'WEAPON_STUNGUN', ammo = 1, label = 'صاعق' } } },
  { id = 'sgt2',   label = 'رقيب ثان',   xp = 25,  armor = 100, kit = { { weapon = 'WEAPON_COMBATPISTOL', ammo = 120, label = 'مسدس قتالي' }, { weapon = 'WEAPON_CARBINERIFLE', ammo = 120, label = 'بندقية' }, { weapon = 'WEAPON_STUNGUN', ammo = 1, label = 'صاعق' } } },
  { id = 'lt',     label = 'ملازم',      xp = 50,  armor = 100, kit = { { weapon = 'WEAPON_PISTOL50', ammo = 100, label = 'مسدس ثقيل' }, { weapon = 'WEAPON_SMG', ammo = 150, label = 'رشاش' }, { weapon = 'WEAPON_STUNGUN', ammo = 1, label = 'صاعق' } } },
  { id = 'cpt',    label = 'نقيب',       xp = 100, armor = 100, kit = { { weapon = 'WEAPON_PISTOL50', ammo = 100, label = 'مسدس ثقيل' }, { weapon = 'WEAPON_CARBINERIFLE_MK2', ammo = 180, label = 'بندقية متطورة' }, { weapon = 'WEAPON_STUNGUN', ammo = 1, label = 'صاعق' } } },
}
Config.RankKitCooldown = 60
Config.XP = { arrest = 3, seize = 2 }
Config.PoliceModel = 's_m_y_cop_01'

-- ===== المسح الأسبوعي (الغارة): يصفّر الكاش والمعدات والقواعد، وتتحول الثروة المتبقية إلى سلطة ووقت سجن =====
Config.Wipe = { days = 7, valuePerAuthority = 5000, jailSecondsPer = 20000 }

-- ===== المركبات (استئجار مجاني من الموقف) =====
Config.Vehicles = { police = 'police', civil = 'sultan', cooldown = 60 }

-- ===== المجموعة =====
Config.PartyMax = 3

-- ===== أحداث فخ (أي لاعب يستدعيها = غشاش) =====
Config.Honeypots = { 'medo:giveMoney', 'medo:addItem', 'medo:setRole', 'medo:server:giveLoot', 'medo:server:addCash', 'medo:admin:setRank' }
