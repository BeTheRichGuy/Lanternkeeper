using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// Item tables (GDD §8.3–8.6): class bases with one implicit, the affixes, legendary powers by class, and 14 uniques
    /// with powers of their own. All balance numbers for items live here.
    /// </summary>
    public static class ItemData
    {
        static readonly Slot[] Offense = { Slot.Weapon, Slot.Gloves, Slot.Ring, Slot.Amulet };
        static readonly Slot[] Defense = { Slot.Helm, Slot.Armor, Slot.Belt, Slot.Boots };

        /// <summary>
        /// 희승 11-22: "활 계열 아이템 새로 만들어서 고치자. 마법사나 성기사도 전용 아이템들, 전용 방어구들 싹 다" — weapon, helm,
        /// armour, gloves and boots belong to one class (<see cref="BaseDef.ClassId"/>) and drop only for it; belts, rings
        /// and amulets are shared. <see cref="BaseDef.Icon"/> is the slot's shared picture; the view shows the base's own
        /// `icon/item_&lt;id&gt;` once it is drawn.
        /// </summary>
        public static readonly BaseDef[] Bases =
        {
            new BaseDef { Id = "rusted_greatsword", Name = "녹슨 대검", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.FlatDamage, ImplicitValue = 3f, ClassId = "ashknight" },
            new BaseDef { Id = "ember_longsword", Name = "잿불 장검", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.EmberPct, ImplicitValue = 6f, ClassId = "ashknight" },
            new BaseDef { Id = "gravedigger_axe", Name = "묘지기 도끼", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.CritDamagePct, ImplicitValue = 10f, ClassId = "ashknight" },
            new BaseDef { Id = "executioner_axe", Name = "처형인 대도끼", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.CritDamagePct, ImplicitValue = 12f, ClassId = "ashknight" },
            new BaseDef { Id = "iron_helm", Name = "철 투구", Slot = Slot.Helm, Icon = "icon/item_helm", ImplicitStat = Stat.MaxHp, ImplicitValue = 10f, ClassId = "ashknight" },
            new BaseDef { Id = "horned_helm", Name = "뿔 투구", Slot = Slot.Helm, Icon = "icon/item_helm", ImplicitStat = Stat.CritChancePct, ImplicitValue = 1.5f, ClassId = "ashknight" },
            new BaseDef { Id = "chain_mail", Name = "사슬 갑옷", Slot = Slot.Armor, Icon = "icon/item_armor", ImplicitStat = Stat.DamageReductionPct, ImplicitValue = 3f, ClassId = "ashknight" },
            new BaseDef { Id = "scorched_plate", Name = "그을린 판금", Slot = Slot.Armor, Icon = "icon/item_armor", ImplicitStat = Stat.MaxHp, ImplicitValue = 20f, ClassId = "ashknight" },
            new BaseDef { Id = "iron_gauntlets", Name = "쇠 건틀릿", Slot = Slot.Gloves, Icon = "icon/item_gloves", ImplicitStat = Stat.FlatDamage, ImplicitValue = 2f, ClassId = "ashknight" },
            new BaseDef { Id = "spiked_gauntlets", Name = "가시 건틀릿", Slot = Slot.Gloves, Icon = "icon/item_gloves", ImplicitStat = Stat.CritChancePct, ImplicitValue = 1.5f, ClassId = "ashknight" },
            new BaseDef { Id = "ironshod_boots", Name = "철갑 장화", Slot = Slot.Boots, Icon = "icon/item_boots", ImplicitStat = Stat.DamageReductionPct, ImplicitValue = 2f, ClassId = "ashknight" },
            new BaseDef { Id = "war_greaves", Name = "전투 경갑", Slot = Slot.Boots, Icon = "icon/item_boots", ImplicitStat = Stat.MoveSpeedPct, ImplicitValue = 5f, ClassId = "ashknight" },
            new BaseDef { Id = "pilgrim_mace", Name = "순례자 철퇴", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.FlatDamage, ImplicitValue = 3f, ClassId = "oathknight" },
            new BaseDef { Id = "judgment_hammer", Name = "심판의 망치", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.CritDamagePct, ImplicitValue = 10f, ClassId = "oathknight" },
            new BaseDef { Id = "oath_scepter", Name = "서약의 홀", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.ResonancePct, ImplicitValue = 8f, ClassId = "oathknight" },
            new BaseDef { Id = "winged_helm", Name = "날개 투구", Slot = Slot.Helm, Icon = "icon/item_helm", ImplicitStat = Stat.MaxHp, ImplicitValue = 10f, ClassId = "oathknight" },
            new BaseDef { Id = "oath_great_helm", Name = "서약 대투구", Slot = Slot.Helm, Icon = "icon/item_helm", ImplicitStat = Stat.DamageReductionPct, ImplicitValue = 2f, ClassId = "oathknight" },
            new BaseDef { Id = "sanctified_plate", Name = "성유 판금", Slot = Slot.Armor, Icon = "icon/item_armor", ImplicitStat = Stat.DamageReductionPct, ImplicitValue = 3f, ClassId = "oathknight" },
            new BaseDef { Id = "pilgrim_chainmail", Name = "순례자 사슬 갑옷", Slot = Slot.Armor, Icon = "icon/item_armor", ImplicitStat = Stat.MaxHp, ImplicitValue = 18f, ClassId = "oathknight" },
            new BaseDef { Id = "vow_gauntlets", Name = "서약 건틀릿", Slot = Slot.Gloves, Icon = "icon/item_gloves", ImplicitStat = Stat.FlatDamage, ImplicitValue = 2f, ClassId = "oathknight" },
            new BaseDef { Id = "pilgrim_greaves", Name = "순례자 경갑", Slot = Slot.Boots, Icon = "icon/item_boots", ImplicitStat = Stat.MoveSpeedPct, ImplicitValue = 5f, ClassId = "oathknight" },
            new BaseDef { Id = "hunter_shortbow", Name = "사냥꾼 단궁", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.AttackSpeedPct, ImplicitValue = 4f, ClassId = "windhunter" },
            new BaseDef { Id = "bone_longbow", Name = "뼈 장궁", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.CritDamagePct, ImplicitValue = 10f, ClassId = "windhunter" },
            new BaseDef { Id = "storm_recurve", Name = "폭풍 복합궁", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.ShockPct, ImplicitValue = 6f, ClassId = "windhunter" },
            new BaseDef { Id = "plague_crossbow", Name = "역병 석궁", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.FlatDamage, ImplicitValue = 3f, ClassId = "windhunter" },
            new BaseDef { Id = "mourning_hood", Name = "상복 두건", Slot = Slot.Helm, Icon = "icon/item_helm", ImplicitStat = Stat.XpPct, ImplicitValue = 4f, ClassId = "windhunter" },
            new BaseDef { Id = "hunter_hood", Name = "사냥꾼 두건", Slot = Slot.Helm, Icon = "icon/item_helm", ImplicitStat = Stat.CritChancePct, ImplicitValue = 1.5f, ClassId = "windhunter" },
            new BaseDef { Id = "hide_armor", Name = "짐승 가죽 갑옷", Slot = Slot.Armor, Icon = "icon/item_armor", ImplicitStat = Stat.DamageReductionPct, ImplicitValue = 3f, ClassId = "windhunter" },
            new BaseDef { Id = "wind_cloak_armor", Name = "바람 외투", Slot = Slot.Armor, Icon = "icon/item_armor", ImplicitStat = Stat.MaxHp, ImplicitValue = 16f, ClassId = "windhunter" },
            new BaseDef { Id = "leather_gloves", Name = "가죽 장갑", Slot = Slot.Gloves, Icon = "icon/item_gloves", ImplicitStat = Stat.AttackSpeedPct, ImplicitValue = 3f, ClassId = "windhunter" },
            new BaseDef { Id = "archer_bracers", Name = "궁수 팔찌", Slot = Slot.Gloves, Icon = "icon/item_gloves", ImplicitStat = Stat.RangePct, ImplicitValue = 6f, ClassId = "windhunter" },
            new BaseDef { Id = "grave_boots", Name = "무덤 장화", Slot = Slot.Boots, Icon = "icon/item_boots", ImplicitStat = Stat.MoveSpeedPct, ImplicitValue = 6f, ClassId = "windhunter" },
            new BaseDef { Id = "wind_striders", Name = "바람 장화", Slot = Slot.Boots, Icon = "icon/item_boots", ImplicitStat = Stat.MoveSpeedPct, ImplicitValue = 8f, ClassId = "windhunter" },
            new BaseDef { Id = "star_staff", Name = "별 지팡이", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.AreaPct, ImplicitValue = 6f, ClassId = "starseer" },
            new BaseDef { Id = "void_orb", Name = "공허의 구슬", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.ResonancePct, ImplicitValue = 8f, ClassId = "starseer" },
            new BaseDef { Id = "comet_wand", Name = "혜성 마법봉", Slot = Slot.Weapon, Icon = "icon/item_sword", ImplicitStat = Stat.EmberPct, ImplicitValue = 6f, ClassId = "starseer" },
            new BaseDef { Id = "star_circlet", Name = "별 머리관", Slot = Slot.Helm, Icon = "icon/item_helm", ImplicitStat = Stat.CooldownPct, ImplicitValue = 3f, ClassId = "starseer" },
            new BaseDef { Id = "seer_cowl", Name = "점술사 고깔", Slot = Slot.Helm, Icon = "icon/item_helm", ImplicitStat = Stat.XpPct, ImplicitValue = 4f, ClassId = "starseer" },
            new BaseDef { Id = "star_robe", Name = "별 로브", Slot = Slot.Armor, Icon = "icon/item_armor", ImplicitStat = Stat.MaxHp, ImplicitValue = 16f, ClassId = "starseer" },
            new BaseDef { Id = "void_vestment", Name = "공허 예복", Slot = Slot.Armor, Icon = "icon/item_armor", ImplicitStat = Stat.DamageReductionPct, ImplicitValue = 3f, ClassId = "starseer" },
            new BaseDef { Id = "silk_gloves", Name = "비단 장갑", Slot = Slot.Gloves, Icon = "icon/item_gloves", ImplicitStat = Stat.CritChancePct, ImplicitValue = 1.5f, ClassId = "starseer" },
            new BaseDef { Id = "star_sandals", Name = "별 샌들", Slot = Slot.Boots, Icon = "icon/item_boots", ImplicitStat = Stat.MoveSpeedPct, ImplicitValue = 6f, ClassId = "starseer" },
            new BaseDef { Id = "leather_belt", Name = "가죽 허리띠", Slot = Slot.Belt, Icon = "icon/item_belt", ImplicitStat = Stat.MaxHp, ImplicitValue = 12f, ClassId = null },
            new BaseDef { Id = "ash_sash", Name = "재 허리띠", Slot = Slot.Belt, Icon = "icon/item_belt", ImplicitStat = Stat.RegenPct, ImplicitValue = 0.3f, ClassId = null },
            new BaseDef { Id = "bone_ring", Name = "뼈 반지", Slot = Slot.Ring, Icon = "icon/item_ring", ImplicitStat = Stat.CritChancePct, ImplicitValue = 2f, ClassId = null },
            new BaseDef { Id = "brass_ring", Name = "놋 반지", Slot = Slot.Ring, Icon = "icon/item_ring", ImplicitStat = Stat.DamagePct, ImplicitValue = 4f, ClassId = null },
            new BaseDef { Id = "lantern_pendant", Name = "등불 목걸이", Slot = Slot.Amulet, Icon = "icon/item_amulet", ImplicitStat = Stat.ResonancePct, ImplicitValue = 8f, ClassId = null },
            new BaseDef { Id = "rosary", Name = "묵주", Slot = Slot.Amulet, Icon = "icon/item_amulet", ImplicitStat = Stat.MagicFindPct, ImplicitValue = 6f, ClassId = null },
        };

        public static readonly AffixDef[] Affixes =
        {
            new AffixDef { Stat = Stat.FlatDamage,         Min = 2f,   Max = 4f,   Slots = Offense, Adjective = "날카로운", Label = "공격력 +{0}" },
            new AffixDef { Stat = Stat.DamagePct,          Min = 4f,   Max = 8f,   Slots = Offense, Adjective = "잔혹한",   Label = "피해 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.AttackSpeedPct,     Min = 3f,   Max = 5f,   Slots = new[] { Slot.Weapon, Slot.Gloves, Slot.Ring }, Adjective = "재빠른", Label = "공격 속도 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.MaxHp,              Min = 8f,   Max = 15f,  Slots = new[] { Slot.Helm, Slot.Armor, Slot.Belt, Slot.Boots, Slot.Ring, Slot.Amulet }, Adjective = "강인한", Label = "최대 생명력 +{0}" },
            new AffixDef { Stat = Stat.DamageReductionPct, Min = 2f,   Max = 4f,   Slots = new[] { Slot.Helm, Slot.Armor, Slot.Belt }, Adjective = "단단한", Label = "받는 피해 −{0}%", Percent = true },
            new AffixDef { Stat = Stat.CritChancePct,      Min = 1.5f, Max = 3f,   Slots = new[] { Slot.Weapon, Slot.Gloves, Slot.Ring, Slot.Amulet, Slot.Helm }, Adjective = "예리한", Label = "치명타 확률 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.CritDamagePct,      Min = 8f,   Max = 15f,  Slots = new[] { Slot.Weapon, Slot.Ring, Slot.Amulet }, Adjective = "치명적인", Label = "치명타 피해 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.MoveSpeedPct,       Min = 5f,   Max = 10f,  Slots = new[] { Slot.Boots, Slot.Amulet }, Adjective = "날랜", Label = "이동 속도 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.AreaPct,            Min = 4f,   Max = 8f,   Slots = new[] { Slot.Weapon, Slot.Helm, Slot.Amulet }, Adjective = "퍼지는", Label = "스킬 범위 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.RangePct,           Min = 5f,   Max = 10f,  Slots = new[] { Slot.Weapon, Slot.Gloves, Slot.Helm }, Adjective = "멀리 닿는", Label = "사거리 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.ProjectileCount,    Min = 0.6f, Max = 1f,   Slots = new[] { Slot.Weapon, Slot.Amulet }, Adjective = "갈라지는", Label = "투사체 및 분신 +{0}", Percent = false },
            new AffixDef { Stat = Stat.CooldownPct,        Min = 3f,   Max = 6f,   Slots = new[] { Slot.Helm, Slot.Amulet, Slot.Ring }, Adjective = "서두르는", Label = "재사용 대기시간 −{0}%", Percent = true },
            new AffixDef { Stat = Stat.EmberPct,           Min = 6f,   Max = 10f,  Slots = new[] { Slot.Weapon, Slot.Amulet, Slot.Gloves, Slot.Helm }, Adjective = "타오르는", Label = "잿불 피해 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.FrostPct,           Min = 6f,   Max = 10f,  Slots = new[] { Slot.Weapon, Slot.Amulet, Slot.Gloves, Slot.Helm }, Adjective = "서리 낀", Label = "서리 피해 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.ShockPct,           Min = 6f,   Max = 10f,  Slots = new[] { Slot.Weapon, Slot.Amulet, Slot.Gloves, Slot.Helm }, Adjective = "번개 서린", Label = "전격 피해 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.RotPct,             Min = 6f,   Max = 10f,  Slots = new[] { Slot.Weapon, Slot.Amulet, Slot.Gloves, Slot.Helm }, Adjective = "썩어 가는", Label = "부패 피해 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.VoidPct,            Min = 6f,   Max = 10f,  Slots = new[] { Slot.Weapon, Slot.Amulet, Slot.Gloves, Slot.Helm }, Adjective = "공허에 물든", Label = "공허 피해 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.ResonancePct,       Min = 8f,   Max = 14f,  Slots = new[] { Slot.Amulet, Slot.Ring, Slot.Weapon }, Adjective = "공명하는", Label = "공명 피해 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.ChainRadiusPct,     Min = 5f,   Max = 10f,  Slots = new[] { Slot.Armor, Slot.Amulet, Slot.Belt }, Adjective = "번지는", Label = "연쇄 범위 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.RegenPct,           Min = 0.3f, Max = 0.6f, Slots = new[] { Slot.Armor, Slot.Belt, Slot.Ring }, Adjective = "되살아나는", Label = "초당 생명력 회복 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.XpPct,              Min = 3f,   Max = 6f,   Slots = new[] { Slot.Helm, Slot.Amulet }, Adjective = "깨우친", Label = "경험치 획득량 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.MagicFindPct,       Min = 5f,   Max = 10f,  Slots = new[] { Slot.Boots, Slot.Ring, Slot.Amulet, Slot.Helm }, Adjective = "운 좋은", Label = "마법 아이템 발견 +{0}%", Percent = true },
        };

        static readonly string[] Ash = { "ashknight" }, Oath = { "oathknight" }, Wind = { "windhunter" }, Star = { "starseer" };

        /// <summary>
        /// Legendary powers (GDD §8.5) and, last, each unique's own power. 희승 11-24 ("디아블로에서 먹었을 때는 기분 좋았는데,
        /// 이건 양산형 RPG 아이템 같아"): a power changes how one of the class's skills plays and shows on screen; a legendary
        /// rolls only powers of the hero's class (<see cref="EssenceDef.Classes"/>), a unique's power is its own.
        /// The first five ids are also the evolution conditions' essences (SkillData) — keep them.
        /// </summary>
        public static readonly EssenceDef[] Essences =
        {
            new EssenceDef { Id = "ember_echo",    Adjective = "메아리치는",    Text = "잿불 베기를 쓰면 같은 적을 한 번 더 벱니다", Classes = Ash },
            new EssenceDef { Id = "shatter_chain", Adjective = "산산조각 내는", Text = "파쇄가 터진 자리에서 잿불 폭발이 한 번 더 일어납니다", Classes = new[] { "ashknight", "starseer" } },
            new EssenceDef { Id = "long_chain",    Adjective = "끝없는",        Text = "각인된 적이 죽을 때 번지는 폭발이 최대 2번 더 이어집니다" },
            new EssenceDef { Id = "ash_feast",     Adjective = "재를 먹는",     Text = "각인된 적을 처치할 때마다 최대 생명력의 2%를 회복합니다" },
            new EssenceDef { Id = "wide_whirl",    Adjective = "휘몰아치는",    Text = "휠윈드가 도는 범위가 40% 넓어집니다", Classes = Ash },
            // 직업 전설 효과 (11-24)
            new EssenceDef { Id = "whirl_finale",  Adjective = "폭발하는",      Text = "휠윈드가 끝날 때 잿불이 터져 3칸 안의 적에게 공격력의 200% 피해를 줍니다", Classes = Ash },
            new EssenceDef { Id = "brand_burst",   Adjective = "서리 튀는",     Text = "서리 낙인이 적중하면 서리 파편이 튀어 1.8칸 안의 적에게 공격력의 50% 서리 피해를 줍니다", Classes = Ash },
            new EssenceDef { Id = "zeal_arc",      Adjective = "광신의",        Text = "열성의 연타가 끝나면 번개가 주변 적 넷에게 튑니다 (각 공격력의 80%)", Classes = Oath },
            new EssenceDef { Id = "bash_ricochet", Adjective = "튕겨 나는",     Text = "방패 강타의 방패가 근처 적 셋에게 더 튕깁니다 (각 공격력의 80%)", Classes = Oath },
            new EssenceDef { Id = "volley_pierce", Adjective = "꿰뚫는",        Text = "속사의 화살이 맞은 적을 꿰뚫고 뒤의 적 둘도 똑같이 맞힙니다", Classes = Wind },
            new EssenceDef { Id = "rain_rot",      Adjective = "썩어 내리는",   Text = "화살비가 떨어진 자리에 2초간 독 웅덩이가 남아 초당 공격력의 50% 부패 피해를 줍니다", Classes = Wind },
            new EssenceDef { Id = "meteor_scorch", Adjective = "운석 같은",     Text = "운석이 떨어진 자리가 3초간 불타 초당 공격력의 60% 잿불 피해를 줍니다", Classes = Star },
            new EssenceDef { Id = "ice_splinter",  Adjective = "쪼개지는",      Text = "얼음 화살이 적을 맞히면 얼음 조각 셋이 근처 적에게 튑니다 (각 공격력의 60% 서리)", Classes = Star },
            // 고유 효과 (unique-only)
            new EssenceDef { Id = "wick_flame",      UniqueOnly = true, Text = "죽음의 연쇄가 터진 자리마다 2초간 등불 불길이 남아 초당 공격력의 100% 피해를 줍니다" },
            new EssenceDef { Id = "kings_echo",      UniqueOnly = true, Text = "대공명이 1초 뒤 한 번 더 울리고(같은 위력), 대공명 게이지가 25% 빨리 찹니다" },
            new EssenceDef { Id = "grave_burst",     UniqueOnly = true, Classes = Ash, Text = "잿불 베기와 용광 일격이 적중할 때마다 무덤 불꽃이 터져 2칸 안의 적에게 공격력의 100% 잿불 피해를 줍니다 (적을 쓰러뜨리면 200%)" },
            new EssenceDef { Id = "furnace_trail",   UniqueOnly = true, Classes = Ash, Text = "휠윈드가 지나간 자리에 3초간 불길이 남아 초당 공격력의 40% 잿불 피해를 줍니다" },
            new EssenceDef { Id = "cinder_crown",    UniqueOnly = true, Classes = Ash, Text = "작열한 스킬을 쓸 때마다 주변 적 셋 위로 잿불 운석이 떨어집니다 (각 공격력의 180%)" },
            new EssenceDef { Id = "judges_verdict",  UniqueOnly = true, Classes = Oath, Text = "심판이 적을 칠 때마다 주변 적 넷에게 번개가 내리치고(각 공격력의 160%, 반경 1.5칸), 심판이 1초 빨리 돌아옵니다" },
            new EssenceDef { Id = "pilgrim_hammers", UniqueOnly = true, Classes = Oath, Text = "축복의 망치가 두 번 더 휘몰아칩니다 (각 공격력의 50%)" },
            new EssenceDef { Id = "silent_bastion",  UniqueOnly = true, Classes = Oath, Text = "피해를 10번 받을 때마다 침묵을 깨고 3칸 안의 적을 공격력의 250% 전격으로 내려치며 생명력 5%를 회복합니다" },
            new EssenceDef { Id = "storm_forks",     UniqueOnly = true, Classes = Wind, Text = "갈래 화살이 3갈래가 아니라 5갈래로 갈라집니다" },
            new EssenceDef { Id = "gale_ward",       UniqueOnly = true, Classes = Wind, Text = "1.5초마다 2칸 안의 적을 회오리로 1칸 밀쳐 내며 공격력의 120% 전격 피해를 줍니다" },
            new EssenceDef { Id = "hawk_mark",       UniqueOnly = true, Classes = Wind, Text = "4초마다 가장 단단한 적에게 매의 표식을 새깁니다: 받는 피해 +25%, 죽으면 주변 적에게 화살 5발 (각 공격력의 60%)" },
            new EssenceDef { Id = "star_fall",       UniqueOnly = true, Classes = Star, Text = "고리 스킬을 3번 쓸 때마다 대상 위로 별이 떨어져 2.2칸 안의 적에게 공격력의 300% 잿불 피해를 줍니다" },
            new EssenceDef { Id = "orbit_stars",     UniqueOnly = true, Classes = Star, Text = "별 셋이 곁을 돌며 1초마다 7칸 안의 적 셋에게 별빛을 쏩니다 (맞은 자리 1.2칸에 공격력의 100%)" },
            new EssenceDef { Id = "time_eater",      UniqueOnly = true, Classes = Star, Text = "적을 쓰러뜨리면(8초에 한 번) 4칸 안의 시간이 2초간 멈추고, 멈춘 동안 받은 피해가 1.5배로 한꺼번에 들어갑니다" },
        };

        /// <summary>
        /// GDD §8.6: uniques appear from Lv15. Two shared jewels and three pieces per class (11-24: two per class — one
        /// shared amulet and a weapon — made every unique one of two items). Icons: `icon/item_unique_&lt;id&gt;`.
        /// </summary>
        public static readonly UniqueDef[] Uniques =
        {
            new UniqueDef { Id = "unquenched_wick", Name = "꺼지지 않는 심지", BaseId = "lantern_pendant", EssenceId = "wick_flame",
                Flavor = "첫 등불지기가 끝까지 놓지 않은 불씨.",
                Fixed = new[] { (Stat.MaxHp, 40f), (Stat.RegenPct, 1f), (Stat.ResonancePct, 25f), (Stat.ChainRadiusPct, 20f) } },
            new UniqueDef { Id = "dead_kings_seal", Name = "죽은 왕의 인장", BaseId = "bone_ring", EssenceId = "kings_echo",
                Flavor = "왕은 모래에 묻혔으나 명령은 아직 살아 있다.",
                Fixed = new[] { (Stat.CritDamagePct, 30f), (Stat.ResonancePct, 20f), (Stat.CooldownPct, 6f), (Stat.MagicFindPct, 15f) } },
            // 잿불기사
            new UniqueDef { Id = "gravekeepers_oath", Name = "묘지기의 맹세", BaseId = "gravedigger_axe", EssenceId = "grave_burst",
                Flavor = "묻은 이의 수만큼 날이 무거워진다.",
                Fixed = new[] { (Stat.FlatDamage, 14f), (Stat.CritDamagePct, 50f), (Stat.EmberPct, 20f), (Stat.AttackSpeedPct, 8f) } },
            new UniqueDef { Id = "furnace_heart", Name = "식지 않는 용광로", BaseId = "scorched_plate", EssenceId = "furnace_trail",
                Flavor = "조합장의 심장은 식는 법을 잊었다.",
                Fixed = new[] { (Stat.MaxHp, 60f), (Stat.DamageReductionPct, 6f), (Stat.EmberPct, 25f), (Stat.RegenPct, 0.8f) } },
            new UniqueDef { Id = "crown_of_cinders", Name = "재의 왕관", BaseId = "horned_helm", EssenceId = "cinder_crown",
                Flavor = "불타는 왕국의 왕은 재를 쓰고 웃었다.",
                Fixed = new[] { (Stat.EmberPct, 20f), (Stat.CritChancePct, 4f), (Stat.CooldownPct, 8f), (Stat.MaxHp, 30f) } },
            // 서약기사 (11-22: every class needs a weapon unique of its own — the 20 h sim lost 4–8 h without one)
            new UniqueDef { Id = "judges_vow", Name = "심판자의 맹세", BaseId = "judgment_hammer", EssenceId = "judges_verdict",
                Flavor = "판결은 한 번, 번개는 세 번.",
                Fixed = new[] { (Stat.FlatDamage, 13f), (Stat.ShockPct, 25f), (Stat.CritChancePct, 6f), (Stat.AreaPct, 10f) } },
            new UniqueDef { Id = "pilgrims_grasp", Name = "순례자의 손아귀", BaseId = "vow_gauntlets", EssenceId = "pilgrim_hammers",
                Flavor = "길 위에서 닳은 손은 망치를 놓지 않는다.",
                Fixed = new[] { (Stat.AttackSpeedPct, 10f), (Stat.ShockPct, 18f), (Stat.FlatDamage, 6f), (Stat.MaxHp, 30f) } },
            new UniqueDef { Id = "silent_vow", Name = "침묵의 서약", BaseId = "oath_great_helm", EssenceId = "silent_bastion",
                Flavor = "맹세한 날부터 그는 한마디도 하지 않았다.",
                Fixed = new[] { (Stat.MaxHp, 50f), (Stat.DamageReductionPct, 6f), (Stat.FrostPct, 18f), (Stat.RegenPct, 0.6f) } },
            // 바람사냥꾼
            new UniqueDef { Id = "stormstring", Name = "폭풍의 시위", BaseId = "storm_recurve", EssenceId = "storm_forks",
                Flavor = "시위를 당기면 하늘이 먼저 운다.",
                Fixed = new[] { (Stat.FlatDamage, 11f), (Stat.ShockPct, 22f), (Stat.AttackSpeedPct, 8f), (Stat.ProjectileCount, 1f) } },
            new UniqueDef { Id = "galewalkers", Name = "바람을 걷는 자", BaseId = "wind_striders", EssenceId = "gale_ward",
                Flavor = "발자국 대신 바람만 남는다.",
                Fixed = new[] { (Stat.MoveSpeedPct, 15f), (Stat.MaxHp, 35f), (Stat.DamageReductionPct, 4f), (Stat.AttackSpeedPct, 6f) } },
            new UniqueDef { Id = "hawkeye_hood", Name = "매의 눈", BaseId = "hunter_hood", EssenceId = "hawk_mark",
                Flavor = "한번 눈에 든 사냥감은 놓치지 않는다.",
                Fixed = new[] { (Stat.CritChancePct, 6f), (Stat.CritDamagePct, 30f), (Stat.RangePct, 15f), (Stat.XpPct, 6f) } },
            // 별점술사
            new UniqueDef { Id = "fallen_star_staff", Name = "떨어진 별의 지팡이", BaseId = "star_staff", EssenceId = "star_fall",
                Flavor = "하늘에서 떨어진 것은 다시 떨어지고 싶어 한다.",
                Fixed = new[] { (Stat.FlatDamage, 12f), (Stat.EmberPct, 18f), (Stat.AreaPct, 15f), (Stat.CritDamagePct, 25f) } },
            new UniqueDef { Id = "constellation_robe", Name = "별자리 법의", BaseId = "star_robe", EssenceId = "orbit_stars",
                Flavor = "꿰맨 별들이 아직 제자리를 기억한다.",
                Fixed = new[] { (Stat.MaxHp, 45f), (Stat.DamageReductionPct, 5f), (Stat.FrostPct, 20f), (Stat.ResonancePct, 15f) } },
            new UniqueDef { Id = "timeeater_sash", Name = "시간을 삼킨 허리띠", BaseId = "ash_sash", EssenceId = "time_eater", ClassId = "starseer",
                Flavor = "조여 맬수록 세상이 느려진다.",
                Fixed = new[] { (Stat.CooldownPct, 10f), (Stat.MaxHp, 35f), (Stat.RegenPct, 0.8f), (Stat.XpPct, 8f) } },
        };

        public static UniqueDef Unique(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var u in Uniques) if (u.Id == id) return u;
            return null;
        }

        static readonly string[] RareFirst = { "잿빛", "핏빛", "무덤", "등불", "부서진", "잠든", "울부짖는", "식은", "꺼진", "타다 남은" };
        static readonly Dictionary<Slot, string[]> RareSecond = new Dictionary<Slot, string[]>
        {
            { Slot.Weapon, new[] { "송곳니", "심판", "흉터", "애가" } },
            { Slot.Helm,   new[] { "왕관", "가면", "기억", "눈" } },
            { Slot.Armor,  new[] { "외피", "껍데기", "맹세", "벽" } },
            { Slot.Gloves, new[] { "손아귀", "손길", "집게", "주먹" } },
            { Slot.Boots,  new[] { "발자국", "행진", "방랑", "걸음" } },
            { Slot.Belt,   new[] { "사슬", "매듭", "고리", "띠" } },
            { Slot.Ring,   new[] { "약속", "고리", "인장", "눈물" } },
            { Slot.Amulet, new[] { "기도", "부적", "심장", "불씨" } },
        };

        /// <summary>T1..T8 multipliers on the base affix range (GDD §8.2).</summary>
        public static readonly float[] TierScale = { 1f, 1.6f, 2.3f, 3.1f, 4f, 5f, 6.5f, 8f };
        static readonly int[] TierIlvl = { 1, 10, 20, 30, 40, 50, 65, 80 };

        public static int MaxTier(int ilvl)
        {
            int t = 1;
            for (int i = 0; i < TierIlvl.Length; i++) if (ilvl >= TierIlvl[i]) t = i + 1;
            return t;
        }

        static Dictionary<string, BaseDef> _bases;
        public static BaseDef Base(string id)
        {
            if (_bases == null)
            {
                _bases = new Dictionary<string, BaseDef>();
                foreach (var b in Bases) _bases[b.Id] = b;
            }
            return _bases[id];
        }

        public static bool HasBase(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            Base(Bases[0].Id); // build the lookup
            return _bases.ContainsKey(id);
        }

        public static AffixDef Affix(Stat s)
        {
            foreach (var a in Affixes) if (a.Stat == s) return a;
            return null;
        }

        public static EssenceDef Essence(string id)
        {
            foreach (var e in Essences) if (e.Id == id) return e;
            return null;
        }

        public static string RareName(SimRng rng, Slot slot) =>
            RareFirst[rng.Range(0, RareFirst.Length)] + " " + RareSecond[slot][rng.Range(0, RareSecond[slot].Length)];
    }
}
