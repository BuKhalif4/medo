Config = {}

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
-- type: police | gang | house. الإحداثيات تقريبية: انتقل بالقائمة (أدوات) وعدّلها
Config.Places = {
  { id = 'hq',     type = 'police', label = 'مركز الشرطة',         tag = 'Police HQ',    coords = {441.2, -981.9, 30.7},    radius = 4.0 },
  { id = 'gang1',  type = 'gang',   label = 'مقر عصابة الجروف',    tag = 'Gang hideout', coords = {-14.7, -1441.8, 31.1},   radius = 3.5, cool = 2 },
  { id = 'gang2',  type = 'gang',   label = 'مقر عصابة الفورم',    tag = 'Gang hideout', coords = {86.0, -1927.0, 20.8},    radius = 3.5, cool = 2 },
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
  'bagcity:giveMoney', 'bagcity:addBag', 'bagcity:setHeat', 'bagcity:server:giveBag',
  'bagcity:server:addMoney', 'bagcity:server:clearCooldown', 'bagcity:admin:setCop',
}
