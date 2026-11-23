using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// M1 item tables (GDD §8.3–8.6): 2–3 bases per slot with one implicit, 16 affixes, 5 Ashknight essences,
    /// 2 uniques. All balance numbers for items live here.
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
            new AffixDef { Stat = Stat.ResonancePct,       Min = 8f,   Max = 14f,  Slots = new[] { Slot.Amulet, Slot.Ring, Slot.Weapon }, Adjective = "공명하는", Label = "공명 피해 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.ChainRadiusPct,     Min = 5f,   Max = 10f,  Slots = new[] { Slot.Armor, Slot.Amulet, Slot.Belt }, Adjective = "번지는", Label = "연쇄 범위 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.RegenPct,           Min = 0.3f, Max = 0.6f, Slots = new[] { Slot.Armor, Slot.Belt, Slot.Ring }, Adjective = "되살아나는", Label = "초당 생명력 회복 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.XpPct,              Min = 3f,   Max = 6f,   Slots = new[] { Slot.Helm, Slot.Amulet }, Adjective = "깨우친", Label = "경험치 획득량 +{0}%", Percent = true },
            new AffixDef { Stat = Stat.MagicFindPct,       Min = 5f,   Max = 10f,  Slots = new[] { Slot.Boots, Slot.Ring, Slot.Amulet, Slot.Helm }, Adjective = "운 좋은", Label = "마법 아이템 발견 +{0}%", Percent = true },
        };

        /// <summary>Ashknight essences (GDD §8.5). Three are wired into combat in M1 (see SimWorld).</summary>
        public static readonly EssenceDef[] Essences =
        {
            new EssenceDef { Id = "ember_echo",    Adjective = "메아리치는",    Text = "잿불 베기가 한 번 더 발동" },
            new EssenceDef { Id = "shatter_chain", Adjective = "산산조각 내는", Text = "파쇄 지점에서 연쇄 폭발 발생" },
            new EssenceDef { Id = "long_chain",    Adjective = "끝없는",        Text = "각인 사망 연쇄 최대 횟수 +2" },
            new EssenceDef { Id = "ash_feast",     Adjective = "재를 먹는",     Text = "각인된 적 처치 시 최대 생명력 2% 회복" },
            new EssenceDef { Id = "wide_whirl",    Adjective = "휘몰아치는",    Text = "대검 회전 범위 +40%" },
        };

        /// <summary>GDD §8.6: uniques appear from Lv15 (none reachable in act I zone 1; defined for the drop table).</summary>
        public static readonly UniqueDef[] Uniques =
        {
            new UniqueDef { Id = "unquenched_wick", Name = "꺼지지 않는 심지", BaseId = "lantern_pendant", EssenceId = "long_chain",
                Fixed = new[] { (Stat.MaxHp, 40f), (Stat.RegenPct, 1f), (Stat.ResonancePct, 25f), (Stat.ChainRadiusPct, 20f) } },
            new UniqueDef { Id = "gravekeepers_oath", Name = "묘지기의 맹세", BaseId = "gravedigger_axe", EssenceId = "ash_feast",
                Fixed = new[] { (Stat.FlatDamage, 12f), (Stat.CritChancePct, 6f), (Stat.CritDamagePct, 40f), (Stat.EmberPct, 20f) } },
            // 희승 11-22 직업 전용 장비: with drops kept to the hero's class, every class needs a weapon unique of its own (the
            // 20 h sim lost 4–8 h on 서약기사 and 별점술사 when 묘지기의 맹세 became 잿불기사-only).
            new UniqueDef { Id = "judges_vow", Name = "심판자의 맹세", BaseId = "judgment_hammer", EssenceId = "ash_feast",
                Fixed = new[] { (Stat.FlatDamage, 12f), (Stat.CritChancePct, 6f), (Stat.CritDamagePct, 40f), (Stat.ShockPct, 20f) } },
            new UniqueDef { Id = "stormstring", Name = "폭풍의 시위", BaseId = "storm_recurve", EssenceId = "ash_feast",
                Fixed = new[] { (Stat.FlatDamage, 12f), (Stat.CritChancePct, 6f), (Stat.CritDamagePct, 40f), (Stat.ShockPct, 20f) } },
            new UniqueDef { Id = "fallen_star_staff", Name = "떨어진 별의 지팡이", BaseId = "star_staff", EssenceId = "ash_feast",
                Fixed = new[] { (Stat.FlatDamage, 12f), (Stat.CritChancePct, 6f), (Stat.CritDamagePct, 40f), (Stat.EmberPct, 20f) } },
        };

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
