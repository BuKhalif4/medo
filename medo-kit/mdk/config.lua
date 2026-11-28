-- طقم ميدو (MDK): ست سكربتات (وحدات) تتفعل وتتعطل من لوحة التحكم. كل الإعدادات الافتراضية هنا،
-- وما تغيّره من لوحة التحكم ينحفظ فوقها (ما يلمس هذا الملف).
Config = {}

Config.Name = 'MDK'
Config.Framework = 'auto'          -- auto | standalone | qbx | qb | esx
Config.AdminAce  = 'mdk.admin'     -- صلاحية لوحة التحكم
Config.DevAce    = 'mdk.dev'       -- أدوات التجربة (انتقال، كاش)

-- ===== الوحدات (السكربتات): true = مفعّل افتراضياً =====
Config.Modules = {
  { id = 'zone',   icon = '🗺️', label = 'الحدود والأماكن',   desc = 'حدود المدينة، وتحديد نقاط الأماكن من اللعبة',        default = true },
  { id = 'bag',    icon = '🎒', label = 'الحقيبة وسجلها',    desc = 'السطو، والحقيبة الحية، والتبييض، والتخبئة',          default = true },
  { id = 'police', icon = '🛡️', label = 'الشرطة',            desc = 'الخدمة والرتب والمعدات، والقبض والرشوة والأدلة',     default = true },
  { id = 'press',  icon = '📰', label = 'الصحافة',           desc = 'تصوير الأحداث الحقيقية ونشرها (تسخّن الحقيبة)',      default = true },
  { id = 'market', icon = '🛒', label = 'السوق والشحنات',    desc = 'السوق السوداء، وشجرة الشحنات، والتجهيز والذخيرة',    default = true },
  { id = 'party',  icon = '👥', label = 'المجموعة',          desc = 'دعوة لاعبين (حتى 3) وعلامات الأعضاء على الخريطة',    default = true },
}
Config.PartyMax = 3

-- ===== عام =====
Config.Webhook        = ''      -- رابط Discord webhook لسجل الأمان (اتركه فاضي لو ما تبي)
Config.HoneypotKick   = true    -- طرد أي لاعب يستدعي حدث فخ
Config.MaxStrikes     = 5       -- عدد المخالفات قبل الطرد التلقائي
Config.News           = true    -- إعلان "عاجل" لكل اللاعبين عند سرقة كبيرة
Config.NewsMinHeat    = 30      -- أقل حرارة للسرقة حتى تنذاع في الأخبار

-- ===== العالم: إزالة البوتات (مشاة وسيارات وشرطة اللعبة) =====
Config.World = {
  noPeds    = true,   -- لا مشاة بوتات
  noTraffic = true,   -- لا سيارات بوتات (متحركة أو واقفة)
  noPolice  = true,   -- لا شرطة بوتات ولا مستوى مطلوبية
}

-- ===== القناع =====
Config.MaskDrawable   = 51      -- رقم القناع (للشخصية الذكرية freemode). غيّره حسب ذوقك
Config.MaskTexture    = 0

-- ===== الحقيبة =====
Config.MaxBagValue    = 250000  -- أقصى قيمة تحملها حقيبة واحدة
Config.MinSpeed       = 0.70    -- أبطأ سرعة جري عند حقيبة ممتلئة (1.0 = عادي)
Config.HandRange      = 3.5     -- أقصى مسافة لتمرير الحقيبة أو القبض
Config.PassHeat       = 5       -- كم ترتفع حرارة الحقيبة مع كل تمرير
Config.DropTTL        = 600     -- ثواني قبل ما تختفي حقيبة مرمية بالشارع
Config.ArchiveMax     = 100     -- عدد سجلات الحقائب المحفوظة كأدلة

-- ===== الحرارة =====
Config.HeatDecay      = 2       -- كم تبرد الأحياء كل دقيقة
Config.AlertFuzz      = 60.0    -- تشويش موقع البلاغ (متر) حتى تبحث الشرطة ولا تروح للنقطة بالضبط

-- ===== التبييض =====
Config.LaunderTime    = 10      -- ثواني
Config.LaunderMinCut  = 0.25    -- أقل نسبة يأخذها التاجر
Config.LaunderMaxCut  = 0.70    -- أعلى نسبة (للحقيبة الساخنة)
Config.TraceWindow    = 1800    -- ثواني: المدة اللي تقدر الشرطة فيها تفحص فلوس مبيَّضة

-- ===== القبض والرشوة =====
Config.ArrestWindow   = 15      -- ثواني ينتظر فيها الاعتقال قرار المطلوب
Config.EscapeDist     = 12.0    -- لو ابتعد أكثر من هالمسافة يهرب
Config.CopCut         = 0.10    -- نسبة الشرطي من قيمة الحقيبة المصادرة
Config.BribeMin       = 5000
Config.BribeMax       = 200000

-- ===== الصحافة =====
Config.SnapRange      = 30.0    -- أقصى مسافة للتصوير (متر)
Config.IncidentTTL    = 900     -- ثواني يبقى فيها الحدث صالحاً للتصوير والنشر
Config.IncidentKeep   = 100     -- عدد الأحداث المحفوظة في الذاكرة
Config.HeadlineMax    = 80      -- أقصى طول للعنوان
Config.PressHeat      = 10      -- النشر يرفع حرارة الحقيبة (الإعلام ينفخ القصة)
Config.PressRewardMax = 5000    -- سقف مكافأة الخبر الواحد

-- ===== حدود المدينة (بدل الخريطة الكاملة) =====
-- اللاعب خارج الدائرة يتحذّر، وبعد warnSeconds ثانية يرجع تلقائياً لنقطة safe
Config.Zone = {
  center = {-50.0, -800.0, 30.0},
  radius = 1450.0,
  warnSeconds = 12,
  safe = {441.2, -975.0, 30.7},   -- مركز الشرطة
}

-- ===== المخابئ (بيوت ومقرات العصابات) =====
Config.Stash = {
  coolPerMinute = 1,    -- كم تبرد حرارة الحقيبة المخبأة كل دقيقة (تضرب في cool المكان)
  raidTime      = 20,   -- ثواني مداهمة الشرطة
  raidCooldown  = 600,  -- ثواني قبل ما ينداهم نفس المكان مرة ثانية
}
Config.DutyAtHQ = true  -- دخول الخدمة (وضع التجربة) يحتاج تكون عند مركز الشرطة

-- ===== الأماكن (أقرب شيء لنظام ROB RUN: مركز شرطة + مقرات + بيوت) =====
-- type: police | gang | house.
-- coords = الباب (الاقتحام). inside = نقطة التخبئة داخل الغرفة. bench = طاولة الأدوات (مقرات العصابات فقط).
-- لو ما حددت inside/bench تُستخدم نقطة الباب. الأسهل: قف مكانها بالقائمة (أدوات التجربة ← تحديد نقطة) وتنحفظ.
Config.Places = {
  { id = 'hq',     type = 'police', label = 'مركز الشرطة',         tag = 'Police HQ',    coords = {441.2, -981.9, 30.7},    radius = 4.0 },
  { id = 'gang1',  type = 'gang',   label = 'مقر عصابة الجروف',    tag = 'Gang hideout', coords = {-14.7, -1441.8, 31.1},   radius = 3.5, cool = 2, bench = nil },
  { id = 'gang2',  type = 'gang',   label = 'مقر عصابة الفورم',    tag = 'Gang hideout', coords = {86.0, -1927.0, 20.8},    radius = 3.5, cool = 2, bench = nil },
  { id = 'house1', type = 'house',  label = 'بيت آمن - الشاطئ',    tag = 'Safehouse',    coords = {-1150.0, -1521.0, 10.6}, radius = 3.0, cool = 1 },
  { id = 'house2', type = 'house',  label = 'بيت آمن - وسط المدينة', tag = 'Safehouse',  coords = {-47.3, -584.0, 38.2},    radius = 3.0, cool = 1 },
}

-- ===== المحلات (غيّر الإحداثيات من قائمة الأدوات) =====
-- loot = {أقل, أعلى}, heat = كم ترتفع حرارة الحي والحقيبة, duration/cooldown بالثواني
Config.Heists = {
  { id = 'jewel',  label = 'محل مجوهرات',   tag = 'Jewelry heist',  coords = {-622.5, -230.9, 38.1},  radius = 6.0, duration = 25, cooldown = 1800, loot = {15000, 40000}, heat = 35, district = 'vinewood' },
  { id = 'store1', label = 'بقالة ستروبري', tag = 'Store robbery',  coords = {25.7, -1347.3, 29.5},   radius = 4.0, duration = 12, cooldown = 600,  loot = {1500, 5000},   heat = 12, district = 'strawberry' },
  { id = 'store2', label = 'بقالة فاينوود', tag = 'Store robbery',  coords = {373.8, 325.9, 103.6},   radius = 4.0, duration = 12, cooldown = 600,  loot = {1500, 5000},   heat = 12, district = 'vinewood' },
  { id = 'store3', label = 'بقالة دافيس',   tag = 'Store robbery',  coords = {-47.0, -1757.5, 29.4},  radius = 4.0, duration = 12, cooldown = 600,  loot = {1500, 5000},   heat = 12, district = 'davis' },
}

-- ===== تجار التبييض =====
Config.Launderers = {
  { id = 'l1', label = 'تاجر الميناء', coords = {1135.8, -982.3, 46.4}, radius = 3.0 },
  { id = 'l2', label = 'تاجر الجبل',   coords = {-1172.0, -1572.0, 4.6}, radius = 3.0 },
}

-- ===== أحداث فخ (أي لاعب يستدعيها = غشاش) =====
Config.Honeypots = {
  'mdk:giveMoney', 'mdk:addBag', 'mdk:setHeat', 'mdk:server:giveBag',
  'mdk:server:addMoney', 'mdk:server:clearCooldown', 'mdk:admin:setCop', 'mdk:server:setModule',
}

-- ===== الأسلحة والمعدات (طاولة الأدوات في مقرات العصابات، ومركز الشرطة للشرطة) =====
-- price = السعر (كاش). ammo = الذخيرة اللي تجي معه. refill = تعبئة الذخيرة تكلف 30% من السعر
-- limitSeconds = أقل مدة بين شراءين (للقاذف). mods = تعديلات (price)
-- أسماء التعديلات (COMPONENT_*) ممكن يكون فيها خطأ بسيط، لو ما اشتغل تعديل قل لي.
Config.Weapons = {
  { id = 'pistol', slot = 'secondary', tier = 1, node = nil,  label = 'مسدس',        weapon = 'WEAPON_PISTOL',        price = 2500,   ammo = 48,
    mods = { { id = 'supp', label = 'كاتم صوت', comp = 'COMPONENT_AT_PI_SUPP_02', price = 3500 }, { id = 'clip', label = 'مخزن موسّع', comp = 'COMPONENT_PISTOL_CLIP_02', price = 2500 } } },
  { id = 'smg', slot = 'primary', tier = 2, node = 'w_smg',     label = 'رشاش صغير',   weapon = 'WEAPON_MICROSMG',      price = 18000,  ammo = 90,
    mods = { { id = 'clip', label = 'مخزن موسّع', comp = 'COMPONENT_MICROSMG_CLIP_02', price = 4000 }, { id = 'scope', label = 'منظار', comp = 'COMPONENT_AT_SCOPE_MACRO', price = 3000 } } },
  { id = 'shotgun', slot = 'primary', tier = 2, node = 'w_shotgun', label = 'بامب أكشن',    weapon = 'WEAPON_PUMPSHOTGUN',   price = 22000,  ammo = 24,
    mods = { { id = 'supp', label = 'كاتم صوت', comp = 'COMPONENT_AT_SR_SUPP', price = 5000 } } },
  { id = 'rifle', slot = 'primary', tier = 3, node = 'w_rifle',   label = 'بندقية',       weapon = 'WEAPON_CARBINERIFLE',  price = 45000,  ammo = 120,
    mods = { { id = 'clip', label = 'مخزن موسّع', comp = 'COMPONENT_CARBINERIFLE_CLIP_02', price = 6000 }, { id = 'grip', label = 'مقبض', comp = 'COMPONENT_AT_AR_AFGRIP', price = 3500 }, { id = 'scope', label = 'منظار', comp = 'COMPONENT_AT_SCOPE_MEDIUM', price = 5000 } } },
  { id = 'rpg', slot = 'primary', tier = 3, node = 'w_rpg',     label = 'قاذف قنابل',   weapon = 'WEAPON_RPG',           price = 180000, ammo = 2, limitSeconds = 1800, mods = {} },
}
Config.ArmorPrice   = 3000
Config.RefillRate   = 0.30

-- ===== السوق السوداء: المعدات (الدروع والحقائب) =====
-- tier = درجة البطاقة (1 أخضر، 2 بنفسجي، 3 ذهبي). node = عقدة الشحنات اللي لازم تفتحها أول (nil = متاح).
-- capacity = أقصى قيمة غنائم تحملها الحقيبة المسروقة. squares = خانات شبكة الحقيبة.
Config.BaseCapacity = 60000     -- السعة بدون حقيبة مجهّزة
Config.BaseSquares  = 18
Config.Gear = {
  { id = 'vest_light',    cat = 'armor', label = 'سترة خفيفة',    price = 4000,   tier = 1, armor = 40 },
  { id = 'vest_plate',    cat = 'armor', label = 'حامل لوحات',    price = 15000,  tier = 1, armor = 70 },
  { id = 'vest_tac',      cat = 'armor', label = 'سترة تكتيكية',  price = 70000,  tier = 2, armor = 100, node = 'e_vest' },
  { id = 'vest_heavy',    cat = 'armor', label = 'سترة ثقيلة',    price = 140000, tier = 3, armor = 100, node = 'e_heavy' },
  { id = 'bag_pouch',     cat = 'bag',   label = 'كيس صغير',      price = 5000,   tier = 1, capacity = 100000, squares = 6 },
  { id = 'bag_satchel',   cat = 'bag',   label = 'حقيبة كتف',     price = 10000,  tier = 1, capacity = 150000, squares = 12 },
  { id = 'bag_duffle',    cat = 'bag',   label = 'حقيبة رياضية',  price = 15000,  tier = 2, capacity = 250000, squares = 24, node = 'e_duffle' },
  { id = 'bag_backpack',  cat = 'bag',   label = 'حقيبة ظهر',     price = 40000,  tier = 3, capacity = 400000, squares = 36, node = 'e_pack' },
}

-- ===== الشحنات: شجرة التقدم (تفتح الدرجات الأعلى). root = مفتوحة من البداية =====
-- col/row = مكان العقدة في الشجرة. cost = كاش. requires = لازم تكون مفتوحة قبلها
Config.Tree = {
  { id = 'w_root',    branch = 'weapons',   label = 'المسدسات',        col = 0, row = 0, root = true },
  { id = 'w_smg',     branch = 'weapons',   label = 'رشاشات صغيرة',    col = 1, row = 0, cost = 20000,  requires = { 'w_root' } },
  { id = 'w_shotgun', branch = 'weapons',   label = 'بنادق بامب',      col = 1, row = 1, cost = 30000,  requires = { 'w_root' } },
  { id = 'w_rifle',   branch = 'weapons',   label = 'بنادق هجومية',    col = 2, row = 0, cost = 70000,  requires = { 'w_smg' } },
  { id = 'w_rpg',     branch = 'weapons',   label = 'قاذف القنابل',    col = 3, row = 0, cost = 250000, requires = { 'w_rifle', 'w_shotgun' } },
  { id = 'e_root',    branch = 'equipment', label = 'المعدات الأساسية', col = 0, row = 3, root = true },
  { id = 'e_vest',    branch = 'equipment', label = 'سترات تكتيكية',   col = 1, row = 3, cost = 40000,  requires = { 'e_root' } },
  { id = 'e_duffle',  branch = 'equipment', label = 'حقائب رياضية',    col = 1, row = 4, cost = 30000,  requires = { 'e_root' } },
  { id = 'e_heavy',   branch = 'equipment', label = 'سترات ثقيلة',     col = 2, row = 3, cost = 100000, requires = { 'e_vest' } },
  { id = 'e_pack',    branch = 'equipment', label = 'حقائب ظهر',       col = 2, row = 4, cost = 80000,  requires = { 'e_duffle' } },
}

-- ===== رتب الشرطة (تتقدم بنقاط: قبض، مداهمة، مصادرة) =====
Config.PoliceRanks = {
  { id = 'patrol', label = 'ضابط دورية', xp = 0,   armor = 50,  kit = { { weapon = 'WEAPON_COMBATPISTOL', ammo = 120, label = 'مسدس قتالي' }, { weapon = 'WEAPON_STUNGUN', ammo = 1, label = 'صاعق' }, { weapon = 'WEAPON_NIGHTSTICK', ammo = 1, label = 'هراوة' } } },
  { id = 'sgt1',   label = 'رقيب أول',   xp = 10,  armor = 75,  kit = { { weapon = 'WEAPON_COMBATPISTOL', ammo = 120, label = 'مسدس قتالي' }, { weapon = 'WEAPON_PUMPSHOTGUN', ammo = 40, label = 'بامب أكشن' }, { weapon = 'WEAPON_STUNGUN', ammo = 1, label = 'صاعق' } } },
  { id = 'sgt2',   label = 'رقيب ثان',   xp = 25,  armor = 100, kit = { { weapon = 'WEAPON_COMBATPISTOL', ammo = 120, label = 'مسدس قتالي' }, { weapon = 'WEAPON_CARBINERIFLE', ammo = 120, label = 'بندقية' }, { weapon = 'WEAPON_STUNGUN', ammo = 1, label = 'صاعق' } } },
  { id = 'lt',     label = 'ملازم',      xp = 50,  armor = 100, kit = { { weapon = 'WEAPON_PISTOL50', ammo = 100, label = 'مسدس ثقيل' }, { weapon = 'WEAPON_SMG', ammo = 150, label = 'رشاش' }, { weapon = 'WEAPON_STUNGUN', ammo = 1, label = 'صاعق' } } },
  { id = 'cpt',    label = 'نقيب',       xp = 100, armor = 100, kit = { { weapon = 'WEAPON_PISTOL50', ammo = 100, label = 'مسدس ثقيل' }, { weapon = 'WEAPON_CARBINERIFLE_MK2', ammo = 180, label = 'بندقية متطورة' }, { weapon = 'WEAPON_STUNGUN', ammo = 1, label = 'صاعق' } } },
}
Config.RankKitCooldown = 60   -- ثواني بين استلام معدات وأخرى
Config.XP = { arrest = 1, seize = 2, raid = 3 }   -- نقاط الشرطي لكل إجراء ناجح

-- ===== إعدادات تُعدَّل من لوحة التحكم (حقول مسموحة فقط) =====
-- path = مكانها داخل Config. type = number | bool. بعد الحفظ تتطبق فوراً وتنحفظ للأبد.
Config.Settings = {
  { module = 'zone',   id = 'zone_radius',   label = 'نصف قطر حدود المدينة (م)',      path = { 'Zone', 'radius' },        type = 'number', min = 300,  max = 6000, step = 50 },
  { module = 'zone',   id = 'zone_warn',     label = 'ثواني التحذير قبل الإرجاع',      path = { 'Zone', 'warnSeconds' },   type = 'number', min = 3,    max = 60,   step = 1 },
  { module = 'bag',    id = 'bag_speed',     label = 'أبطأ سرعة جري بحقيبة ممتلئة',   path = { 'MinSpeed' },              type = 'number', min = 0.4,  max = 1.0,  step = 0.05 },
  { module = 'bag',    id = 'bag_heatdecay', label = 'تبريد الأحياء (كل دقيقة)',      path = { 'HeatDecay' },             type = 'number', min = 0,    max = 20,   step = 1 },
  { module = 'bag',    id = 'bag_launder',   label = 'أقل نسبة للتاجر عند التبييض',    path = { 'LaunderMinCut' },         type = 'number', min = 0,    max = 0.9,  step = 0.05 },
  { module = 'bag',    id = 'bag_launder_max', label = 'أعلى نسبة للتاجر (حقيبة ساخنة)', path = { 'LaunderMaxCut' },      type = 'number', min = 0.1,  max = 0.95, step = 0.05 },
  { module = 'bag',    id = 'bag_news',      label = 'إعلان «عاجل» للسرقات الكبيرة',   path = { 'News' },                  type = 'bool' },
  { module = 'bag',    id = 'bag_capacity',  label = 'سعة الحقيبة بدون تجهيز',         path = { 'BaseCapacity' },          type = 'number', min = 10000, max = 1000000, step = 10000 },
  { module = 'police', id = 'pol_cut',       label = 'نسبة الشرطي من المصادرة',        path = { 'CopCut' },                type = 'number', min = 0,    max = 0.5,  step = 0.01 },
  { module = 'police', id = 'pol_bribe_min', label = 'أقل رشوة',                       path = { 'BribeMin' },              type = 'number', min = 0,    max = 100000, step = 1000 },
  { module = 'police', id = 'pol_bribe_max', label = 'أعلى رشوة',                      path = { 'BribeMax' },              type = 'number', min = 1000, max = 2000000, step = 5000 },
  { module = 'police', id = 'pol_window',    label = 'مهلة قرار المطلوب (ثانية)',       path = { 'ArrestWindow' },          type = 'number', min = 5,    max = 60,   step = 1 },
  { module = 'police', id = 'pol_raid',      label = 'مدة المداهمة (ثانية)',            path = { 'Stash', 'raidTime' },     type = 'number', min = 5,    max = 90,   step = 1 },
  { module = 'press',  id = 'press_ttl',     label = 'مدة صلاحية الحدث للنشر (ثانية)',  path = { 'IncidentTTL' },           type = 'number', min = 60,   max = 7200, step = 60 },
  { module = 'press',  id = 'press_reward',  label = 'سقف مكافأة الخبر',               path = { 'PressRewardMax' },        type = 'number', min = 0,    max = 100000, step = 500 },
  { module = 'press',  id = 'press_heat',    label = 'تسخين الحقيبة عند النشر',         path = { 'PressHeat' },             type = 'number', min = 0,    max = 50,   step = 1 },
  { module = 'market', id = 'mk_refill',     label = 'سعر تعبئة الذخيرة (نسبة من سعر السلاح)', path = { 'RefillRate' },   type = 'number', min = 0.05, max = 1,    step = 0.05 },
  { module = 'party',  id = 'party_max',     label = 'أقصى عدد في المجموعة',            path = { 'PartyMax' },              type = 'number', min = 2,    max = 8,    step = 1 },
}
