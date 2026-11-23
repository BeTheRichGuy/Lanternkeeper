using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>GDD §8.1 (M1 subset: relic and mythic come later).</summary>
    public enum Rarity { Common, Magic, Rare, Legendary, Unique }

    /// <summary>GDD §8.3 (M1: 9 equip spots, rings share one slot type).</summary>
    public enum Slot { Weapon, Helm, Armor, Gloves, Boots, Belt, Ring, Amulet }

    public enum Stat
    {
        FlatDamage, DamagePct, AttackSpeedPct, MaxHp, DamageReductionPct, CritChancePct, CritDamagePct, MoveSpeedPct,
        EmberPct, FrostPct, ShockPct, ResonancePct, ChainRadiusPct, RegenPct, XpPct, MagicFindPct,
        // 희승: effects should multiply and spread as the hero grows — area, extra projectiles, reach.
        AreaPct, ProjectileCount, RangePct,
        CooldownPct,
    }

    [Serializable]
    public struct AffixRoll
    {
        public Stat Stat;
        public float Value;
        public int Tier; // 1..8 (GDD §8.2 T1~T8)
        /// <summary>★ 대옵션 (D4 Greater Affix): rolled at 1.5× the value; only on rare and better, more likely deeper.</summary>
        public bool Greater;
    }

    /// <summary>
    /// One item instance. Public fields only so the save layer can serialise it with JsonUtility.
    /// </summary>
    [Serializable]
    public sealed class Item
    {
        public int Uid;
        public string BaseId;
        public Rarity Rarity;
        public int Ilvl;
        public int ReqLevel;
        public string Name;
        public AffixRoll Implicit;
        public List<AffixRoll> Affixes = new List<AffixRoll>();
        /// <summary>Legendary power (GDD §8.5) or unique effect id; empty if none.</summary>
        public string EssenceId = "";
        /// <summary>야영지 대장장이 명품화 0..12: every stat of the item +8% per step (GDD §14).</summary>
        public int Masterwork;
        /// <summary>GDD §8.7 sockets (0–3) and the runes in them, in order (Rune values).</summary>
        public int Sockets;
        public List<int> RuneSlots = new List<int>();
        public RunewordDef Runeword => Runes.Match(RuneSlots);
        /// <summary>
        /// GDD §9 안정도: a visible crafting budget (magic 10, rare 20, legendary 30). Rerolls spend it; at 0 crafting
        /// stops, the item is never destroyed. −1 = not set yet (items from before crafting): the rarity's full budget.
        /// </summary>
        public int Stability = -1;
        /// <summary>The affix line this item rerolls (GDD §9: once picked, always the same line). −1 = none yet.</summary>
        public int RerollLine = -1;
        public int Rerolls;
        /// <summary>잠금 (gear panel): never salvaged, by hand, by 일괄 분해 or by a full bag. Old saves: unlocked.</summary>
        public bool Locked;

        public static int StabilityBudget(Rarity r) => r == Rarity.Magic ? 10 : r == Rarity.Rare ? 20 : r == Rarity.Legendary ? 30 : 0;
        public int StabilityLeft => Stability < 0 ? StabilityBudget(Rarity) : Stability;

        public Slot Slot => ItemData.Base(BaseId).Slot;

        /// <summary>How many ★ lines the item has (drop signal, compare).</summary>
        public int GreaterCount { get { int n = 0; foreach (var a in Affixes) if (a.Greater) n++; return n; } }

        public float Sum(Stat s)
        {
            float v = Implicit.Stat == s ? Implicit.Value : 0f;
            foreach (var a in Affixes) if (a.Stat == s) v += a.Value;
            v *= 1f + 0.08f * Masterwork;
            if (RuneSlots != null)
                foreach (int r in RuneSlots)
                {
                    var (stat, value) = Runes.StatOf((Rune)r);
                    if (stat == s) v += value;
                }
            return v;
        }
    }

    public sealed class BaseDef
    {
        public string Id, Name, Icon;
        public Slot Slot;
        public Stat ImplicitStat;
        public float ImplicitValue;
        /// <summary>The class it drops for (bows for 바람사냥꾼, staves for 별점술사, …); null = every class.</summary>
        public string ClassId;

        public bool Fits(string classId) => ClassId == null || classId == null || ClassId == classId;
    }

    public sealed class AffixDef
    {
        public Stat Stat;
        public float Min, Max;
        public Slot[] Slots;
        /// <summary>Adjective used to name magic items, e.g. "날카로운 녹슨 대검".</summary>
        public string Adjective;
        public string Label;
        public bool Percent;
    }

    public sealed class EssenceDef
    {
        public string Id, Adjective, Text;
    }

    public sealed class UniqueDef
    {
        public string Id, Name, BaseId, EssenceId;
        public (Stat stat, float value)[] Fixed;
    }
}
