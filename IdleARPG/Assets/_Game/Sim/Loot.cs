using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>GDD §10 presets. Legendary and above are never auto-salvaged; socketed bases outlive stricter filters.</summary>
    public enum FilterPreset { Beginner, Normal, Strict, Endgame, ClassFit, StarHunt, Custom }

    public enum LootAction { Keep, Salvage }

    /// <summary>What a loot rule tests (재기획 §8 규칙형 필터; the first matching rule decides).</summary>
    public enum RuleCond { Always, RarityAtLeast, RarityAtMost, StarsAtLeast, SocketsAtLeast, ClassFitAtLeast }

    public struct LootRule
    {
        public RuleCond Cond;
        public int Value;
        public LootAction Action;

        public LootRule(RuleCond cond, int value, LootAction action) { Cond = cond; Value = value; Action = action; }

        public bool Matches(Item item, string classId)
        {
            switch (Cond)
            {
                case RuleCond.RarityAtLeast: return (int)item.Rarity >= Value;
                case RuleCond.RarityAtMost: return (int)item.Rarity <= Value;
                case RuleCond.StarsAtLeast: return item.GreaterCount >= Value;
                case RuleCond.SocketsAtLeast: return item.Sockets >= Value;
                case RuleCond.ClassFitAtLeast:
                    int fit = 0;
                    foreach (var a in item.Affixes) if (LootFilter.FitsClass(a.Stat, classId)) fit++;
                    return fit >= Value;
                default: return true;
            }
        }
    }

    public static class LootFilter
    {
        public static LootAction Decide(FilterPreset preset, Item item) => Decide(preset, item, null);

        /// <summary>Stats that feed a class's growth axis: speed for melee, area/reach/copies for ranged (GDD v0.8).</summary>
        public static bool FitsClass(Stat s, string classId)
        {
            bool melee = HeroStats.IsMeleeClass(classId);
            switch (s)
            {
                case Stat.AttackSpeedPct: case Stat.MoveSpeedPct: return melee;
                case Stat.AreaPct: case Stat.RangePct: case Stat.ProjectileCount: return !melee || s == Stat.AreaPct;
                case Stat.DamagePct: case Stat.CritChancePct: case Stat.CritDamagePct: case Stat.ResonancePct: case Stat.CooldownPct: return true;
                default: return false;
            }
        }

        /// <summary>The first rule that matches decides (every list ends with an Always rule).</summary>
        public static LootAction Decide(FilterPreset preset, Item item, string classId) => Decide(preset, item, classId, null);

        /// <summary><paramref name="custom"/>: the player's own rules for 직접 (the protecting rules still come first).</summary>
        public static LootAction Decide(FilterPreset preset, Item item, string classId, IList<LootRule> custom)
        {
            foreach (var r in Rules(preset, custom)) if (r.Matches(item, classId)) return r.Action;
            return LootAction.Keep; // 직접 with no rule that matches keeps the piece
        }

        /// <summary>Rules a new 직접 list starts from: 보통's.</summary>
        public static List<LootRule> StarterRules() => new List<LootRule>(Presets[(int)FilterPreset.Normal]);

        public const int MaxCustomRules = 10;

        static readonly LootRule[] Protect =
        {
            // Legendary and better, and ★ 대옵션 pieces (the chase), are never salvaged by a preset.
            new LootRule(RuleCond.RarityAtLeast, (int)Rarity.Legendary, LootAction.Keep),
            new LootRule(RuleCond.StarsAtLeast, 1, LootAction.Keep),
        };

        static readonly LootRule[][] Presets =
        {
            // 초보: everything.
            new[] { new LootRule(RuleCond.Always, 0, LootAction.Keep) },
            // 보통: commons go.
            new[] { new LootRule(RuleCond.RarityAtMost, (int)Rarity.Common, LootAction.Salvage), new LootRule(RuleCond.Always, 0, LootAction.Keep) },
            // 엄격: runeword bases (2+ sockets, GDD §8.7) stay; magic and below go.
            new[] { new LootRule(RuleCond.SocketsAtLeast, 2, LootAction.Keep), new LootRule(RuleCond.RarityAtMost, (int)Rarity.Magic, LootAction.Salvage), new LootRule(RuleCond.Always, 0, LootAction.Keep) },
            // 엔드게임: only three-socket bases beyond the protected ones.
            new[] { new LootRule(RuleCond.SocketsAtLeast, 3, LootAction.Keep), new LootRule(RuleCond.Always, 0, LootAction.Salvage) },
            // 직업 맞춤: rare and better with two lines on this class's growth axis, or a runeword base.
            new[] { new LootRule(RuleCond.SocketsAtLeast, 3, LootAction.Keep), new LootRule(RuleCond.RarityAtMost, (int)Rarity.Magic, LootAction.Salvage),
                    new LootRule(RuleCond.ClassFitAtLeast, 2, LootAction.Keep), new LootRule(RuleCond.Always, 0, LootAction.Salvage) },
            // ★ 사냥: late hunting — ★ pieces, legendaries and three-socket bases.
            new[] { new LootRule(RuleCond.SocketsAtLeast, 3, LootAction.Keep), new LootRule(RuleCond.Always, 0, LootAction.Salvage) },
        };

        /// <summary>A preset as the rule list it is (재기획 §8: a preset is a list you can open and read).</summary>
        public static IEnumerable<LootRule> Rules(FilterPreset preset) => Rules(preset, null);

        public static IEnumerable<LootRule> Rules(FilterPreset preset, IList<LootRule> custom)
        {
            foreach (var r in Protect) yield return r;
            if (preset == FilterPreset.Custom)
            {
                if (custom != null) foreach (var r in custom) yield return r;
                yield break;
            }
            foreach (var r in Presets[Math.Max(0, Math.Min(Presets.Length - 1, (int)preset))]) yield return r;
        }

        /// <summary>Ash shards from salvage (GDD §9 분해): grows with rarity and item level.</summary>
        public static int SalvageValue(Item item) => (1 + (int)item.Rarity * 3) * (1 + item.Ilvl / 10);
    }

    /// <summary>
    /// The bag and materials. 희승 11-22: forty places, as in Diablo II (10 × 4), and never more — when it is full the
    /// weakest unlocked piece goes to ash (the new one, if it is the weakest). Locked pieces are never taken.
    /// </summary>
    public sealed class Bag
    {
        public const int Capacity = 40, Cols = 10;
        public readonly List<Item> Items = new List<Item>();
        public int AshShards;
        /// <summary>등불 기름 (GDD §6.2 기름 샘, §14 야영지 비용).</summary>
        public int LanternOil;
        /// <summary>Loose runes by <see cref="Rune"/> value, waiting for a socket.</summary>
        public readonly int[] RuneCounts = new int[Runes.Count];

        /// <summary>Adds the item; if that overfills the bag, the weakest unlocked piece is salvaged (possibly the new one).
        /// Returns what was salvaged, or null.</summary>
        public Item Add(Item item)
        {
            Items.Add(item);
            return Items.Count > Capacity ? SalvageWeakest(item) : null;
        }

        /// <summary>Back under the cap (a save from when the bag held more): the weakest unlocked pieces go to ash.</summary>
        public int Trim()
        {
            int n = 0;
            while (Items.Count > Capacity && SalvageWeakest(null) != null) n++;
            return n;
        }

        Item SalvageWeakest(Item fallback)
        {
            Item worst = null;
            foreach (var it in Items)
            {
                if (it.Locked) continue; // 잠금 always wins
                if (worst == null || Weight(it) < Weight(worst)) worst = it;
            }
            worst ??= fallback; // all locked: the new piece goes
            if (worst == null) return null;
            Items.Remove(worst);
            AshShards += LootFilter.SalvageValue(worst);
            return worst;
        }

        /// <summary>Uniques outweigh everything, then rarity, ★ lines, runeword sockets and item level.</summary>
        static float Weight(Item it) => (it.Rarity == Rarity.Unique ? 10000 : 0) + (int)it.Rarity * 1000 + it.GreaterCount * 600 + (it.Sockets >= 2 ? 300 : 0) + it.Ilvl;
    }

    /// <summary>GDD §11.4 drop counts.</summary>
    public static class DropTable
    {
        public const float NormalChance = 0.08f;
        public const int BossDrops = 4, ActBossDrops = 8, EliteDrops = 2, ChestDrops = 3, LegendaryGuaranteeLevel = 3;
    }
}
