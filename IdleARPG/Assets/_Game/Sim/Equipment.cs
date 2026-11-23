using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>Nine equip spots (GDD §8.3, M1 subset). Two ring spots share <see cref="Slot.Ring"/>.</summary>
    public enum EquipSpot { Weapon, Helm, Armor, Gloves, Boots, Belt, Ring1, Ring2, Amulet }

    /// <summary>
    /// The one answer to "is this piece better?" (UX plan §3.0 다), checked in this order: too high to wear, a ◆ trade,
    /// ▲▲ (+10% 전투력), ▲ (+1%), ▼ (−1%), else ═.
    /// </summary>
    public enum GearArrow { Locked, Trade, BigUp, Up, Down, Same }

    /// <summary>A piece against what is worn in <see cref="Spot"/>. The three ratios are after ÷ now (1 = no change).</summary>
    public struct GearCompare
    {
        public EquipSpot Spot;
        public float Power, Offence, Survival;
        /// <summary>Legendary powers gained (or lost) by the swap.</summary>
        public int Essences;
        public GearArrow Arrow;
        /// <summary>Auto-equip takes ▲ and ▲▲ only: a ◆ trade is the player's call and stays in the bag.</summary>
        public bool AutoEquips => Arrow == GearArrow.Up || Arrow == GearArrow.BigUp;
    }

    /// <summary>Everything gear and level add up to. Recomputed whenever gear or level changes.</summary>
    public sealed class Derived
    {
        public float DamagePct, EmberPct, FrostPct, ShockPct, ResonancePct;
        public float CritChance = 0.05f, CritMult = 1.5f;
        public float DamageReduction, RegenPctPerSec, XpPct, MagicFindPct, ChainRadiusPct;
        /// <summary>Skill area (+%), extra copies of every projectile and targeted skill, and reach (+%).</summary>
        public float AreaPct, RangePct;
        /// <summary>Ring skill cooldowns shorter by this share (0..MaxCooldownCut).</summary>
        public float CooldownCut;
        public int Projectiles;
        public readonly HashSet<string> Essences = new HashSet<string>();
        /// <summary>Runewords formed in equipped items (GDD §8.7).</summary>
        public readonly HashSet<string> Runewords = new HashSet<string>();

        public float ElementPct(Element e)
        {
            switch (e)
            {
                case Element.Ember: return EmberPct;
                case Element.Frost: return FrostPct;
                case Element.Shock: return ShockPct;
                default: return 0f;
            }
        }
    }

    public sealed class Equipment
    {
        public readonly Item[] Spots = new Item[9];

        public Item this[EquipSpot s] => Spots[(int)s];

        /// <summary>The item slot a spot takes (both ring spots take rings).</summary>
        public static Slot SlotOf(EquipSpot spot) =>
            spot == EquipSpot.Ring1 || spot == EquipSpot.Ring2 ? Slot.Ring : spot == EquipSpot.Amulet ? Slot.Amulet : (Slot)(int)spot;

        public static EquipSpot[] SpotsFor(Slot slot)
        {
            switch (slot)
            {
                case Slot.Ring: return new[] { EquipSpot.Ring1, EquipSpot.Ring2 };
                case Slot.Weapon: return new[] { EquipSpot.Weapon };
                case Slot.Helm: return new[] { EquipSpot.Helm };
                case Slot.Armor: return new[] { EquipSpot.Armor };
                case Slot.Gloves: return new[] { EquipSpot.Gloves };
                case Slot.Boots: return new[] { EquipSpot.Boots };
                case Slot.Belt: return new[] { EquipSpot.Belt };
                default: return new[] { EquipSpot.Amulet };
            }
        }

        /// <summary>Puts the item on and returns whatever was there (or null).</summary>
        public Item Equip(Item item, EquipSpot spot)
        {
            var old = Spots[(int)spot];
            Spots[(int)spot] = item;
            return old;
        }

        public float Sum(Stat s)
        {
            float v = 0f;
            foreach (var it in Spots) if (it != null) v += it.Sum(s);
            return v;
        }
    }

    /// <summary>Level + gear → hero stats (GDD §5.4 buckets: flat, additive %, multiplicative).</summary>
    public static class HeroStats
    {
        public const float BaseHp = 200f, HpPerLevel = 10f, BaseDamage = 12f, DamagePerLevel = 1f;
        public const float BaseAttackSpeed = 1.5f, BaseMoveSpeed = 5.0f, MaxDamageReduction = 60f; // 희승: 속도감 — 3.6 → 5.0

        /// <summary>Applies level + gear to the hero, keeping the current HP fraction.</summary>
        public static void Recompute(Hero h)
        {
            float hpFrac = h.Stats.MaxHp > 0f ? h.Stats.Hp / h.Stats.MaxHp : 1f;
            Calc(h.Gear, h.IntegratedLevel, -1, null, h.Stats, h.Derived, h.ClassId, h.WickLevel);
            Talents.Apply(h);
            Runes.ApplyWords(h);
            Augments.Apply(h); // 환생 증강 카드
            // The melee guard multiplies on its own, after every capped source (gear, talents, runewords), so those stay
            // worth having up to their 60% cap.
            if (IsMeleeClass(h.ClassId)) h.Derived.DamageReduction = 1f - (1f - h.Derived.DamageReduction) * (1f - MeleeGuardPct / 100f);
            h.Stats.Hp = Math.Min(h.Stats.MaxHp, Math.Max(1f, hpFrac * h.Stats.MaxHp));
        }

        /// <summary>Pure stat calculation; <paramref name="overrideSpot"/> (or -1) is treated as holding <paramref name="overrideItem"/>.</summary>
        /// <summary>Melee classes grow faster (attack and move speed); ranged classes grow wider and longer (area and reach).</summary>
        public static bool IsMeleeClass(string classId) => classId != "windhunter" && classId != "starseer";

        /// <summary>
        /// Melee classes stand inside the pack (Diablo III gives them 30% less damage taken for the same reason). Without it
        /// the Ashknight died 4–7 times an hour from act II on and the retreat rule kept it pacing act III for 20 h+.
        /// </summary>
        public static float MeleeGuardPct = 30f;

        /// <summary>
        /// Ranged classes' power per level (a multiplier on base damage). After the auto-equip fix the melee classes, with
        /// their guard and life on hit, ran about 2.5–3× the ranged ones' damage at 60 h (희승: 원거리를 올리기).
        /// </summary>
        public static float RangedPowerPerLevel = 0.02f;

        public const int MaxProjectiles = 8;
        public const float MaxCooldownCut = 0.4f;

        static void Calc(Equipment eq, int level, int overrideSpot, Item overrideItem, Stats s, Derived d, string classId, int wick)
        {
            float Sum(Stat st)
            {
                float v = 0f;
                for (int i = 0; i < eq.Spots.Length; i++)
                {
                    var it = i == overrideSpot ? overrideItem : eq.Spots[i];
                    // GDD §11.5: gear above the integrated level (after a rebirth) stays on but does nothing.
                    if (it != null && it.ReqLevel <= level) v += it.Sum(st);
                }
                return v;
            }

            s.MaxHp = BaseHp + HpPerLevel * (level - 1) + Sum(Stat.MaxHp);
            s.Damage = BaseDamage + DamagePerLevel * (level - 1) + Sum(Stat.FlatDamage);
            // 희승 (Diablo): warriors sweep through on speed, casters and archers clear with ever-wider areas.
            bool melee = IsMeleeClass(classId);
            int lv = Math.Max(0, level - 1);
            s.AttackSpeed = BaseAttackSpeed * (1f + Sum(Stat.AttackSpeedPct) / 100f + (melee ? 0.006f * lv : 0f) + 0.002f * wick);
            // Ranged heroes' spells and shots hit harder with each level, as melee swings faster (a separate multiplier).
            if (!melee) s.Damage *= 1f + RangedPowerPerLevel * lv;
            s.MoveSpeed = BaseMoveSpeed * (1f + Sum(Stat.MoveSpeedPct) / 100f + (melee ? 0.004f * lv : 0.0015f * lv) + 0.001f * wick);
            d.AreaPct = Sum(Stat.AreaPct) / 100f + (melee ? 0.003f * lv : 0.008f * lv) + 0.005f * wick;
            d.RangePct = Sum(Stat.RangePct) / 100f + (melee ? 0f : 0.005f * lv);
            d.Projectiles = Math.Min(MaxProjectiles, (int)Math.Floor(Sum(Stat.ProjectileCount) + 1e-3f));
            d.CooldownCut = Math.Min(MaxCooldownCut, Sum(Stat.CooldownPct) / 100f);

            d.DamagePct = Sum(Stat.DamagePct) / 100f + 0.01f * wick;
            d.EmberPct = Sum(Stat.EmberPct) / 100f;
            d.FrostPct = Sum(Stat.FrostPct) / 100f;
            d.ShockPct = Sum(Stat.ShockPct) / 100f;
            d.ResonancePct = Sum(Stat.ResonancePct) / 100f;
            d.CritChance = Math.Min(0.75f, 0.05f + Sum(Stat.CritChancePct) / 100f);
            d.CritMult = 1.5f + Sum(Stat.CritDamagePct) / 100f;
            d.DamageReduction = Math.Min(MaxDamageReduction, Sum(Stat.DamageReductionPct)) / 100f;
            d.RegenPctPerSec = Sum(Stat.RegenPct) / 100f;
            d.XpPct = Sum(Stat.XpPct) / 100f;
            d.MagicFindPct = Sum(Stat.MagicFindPct);
            d.ChainRadiusPct = Sum(Stat.ChainRadiusPct) / 100f;
            d.Essences.Clear();
            d.Runewords.Clear();
            for (int i = 0; i < eq.Spots.Length; i++)
            {
                var it = i == overrideSpot ? overrideItem : eq.Spots[i];
                if (it != null && it.ReqLevel > level) continue;
                if (it != null && !string.IsNullOrEmpty(it.EssenceId)) d.Essences.Add(it.EssenceId);
                var rw = it?.Runeword;
                if (rw != null) d.Runewords.Add(rw.Id);
            }
        }

        /// <summary>Expected damage per second against an unmarked target, used to compare gear.</summary>
        public static float Dps(Hero h) => Dps(h.Stats, h.Derived);

        static float Dps(Stats s, Derived d)
        {
            float elem = (d.EmberPct + d.FrostPct + d.ShockPct) / 3f;
            float crit = 1f + d.CritChance * (d.CritMult - 1f);
            // Area and extra copies clear packs: worth a share of a damage bonus when comparing gear.
            float clear = 1f + d.AreaPct * 0.5f + d.Projectiles * 0.25f + d.RangePct * 0.15f + d.CooldownCut * 0.5f;
            return s.Damage * (1f + d.DamagePct + elem + d.ResonancePct * 0.25f) * crit * s.AttackSpeed * clear;
        }

        /// <summary>Effective HP: more health, less damage taken, some regen.</summary>
        public static float Ehp(Hero h) => Ehp(h.Stats, h.Derived);

        static float Ehp(Stats s, Derived d) => s.MaxHp / (1f - d.DamageReduction) * (1f + d.RegenPctPerSec * 10f);

        // Scratch objects for what-if comparisons (Sim is single-threaded).
        static readonly Stats ScratchStats = new Stats();
        static readonly Derived ScratchDerived = new Derived();
        static readonly Stats BaseStats = new Stats();
        static readonly Derived BaseDerived = new Derived();

        // ---------- 전투력 and the arrows (UX plan §3.0 다): one number, one function, for the bag, the tooltip and auto-equip ----------

        /// <summary>Each legendary power worn is worth this much 전투력 (the flat +15% the compare always gave one).</summary>
        public const float EssencePower = 1.15f;
        /// <summary>Arrow thresholds on the 전투력 ratio, and the share a ◆ trade's smaller side must reach.</summary>
        public const float UpAt = 1.01f, BigUpAt = 1.10f, DownAt = 0.99f, TradeAt = 0.05f;

        static float Power(Stats s, Derived d) =>
            Dps(s, d) * (float)Math.Sqrt(Ehp(s, d)) * (float)Math.Pow(EssencePower, d.Essences.Count);

        /// <summary>
        /// 전투력: offence (expected DPS) × √survival (effective HP) × legendary powers, from gear and level alone — the
        /// hero's live numbers also carry talents, runewords and the melee guard, which a what-if cannot, so comparing
        /// against them asked late upgrades to beat the talent bonus too.
        /// </summary>
        public static float Power(Hero h)
        {
            Calc(h.Gear, h.IntegratedLevel, -1, null, BaseStats, BaseDerived, h.ClassId, h.WickLevel);
            return Power(BaseStats, BaseDerived);
        }

        /// <summary>
        /// What wearing <paramref name="item"/> in <paramref name="spot"/> would do: Power(after) ÷ Power(now), the
        /// offence and survival ratios on their own, and the arrow they earn. Read-only: the hero is not touched.
        /// </summary>
        public static GearCompare Compare(Hero h, Item item, EquipSpot spot)
        {
            Calc(h.Gear, h.IntegratedLevel, -1, null, BaseStats, BaseDerived, h.ClassId, h.WickLevel);
            Calc(h.Gear, h.IntegratedLevel, (int)spot, item, ScratchStats, ScratchDerived, h.ClassId, h.WickLevel);
            var c = new GearCompare
            {
                Spot = spot,
                Power = Power(ScratchStats, ScratchDerived) / Power(BaseStats, BaseDerived),
                Offence = Dps(ScratchStats, ScratchDerived) / Dps(BaseStats, BaseDerived),
                Survival = Ehp(ScratchStats, ScratchDerived) / Ehp(BaseStats, BaseDerived),
                Essences = ScratchDerived.Essences.Count - BaseDerived.Essences.Count,
            };
            c.Arrow = item.ReqLevel > h.IntegratedLevel || !FitsClass(item, h.ClassId) ? GearArrow.Locked : ArrowOf(c.Power, c.Offence, c.Survival);
            return c;
        }

        /// <summary>
        /// Whether this class may wear <paramref name="item"/>: a unique by its own class (11-24 UniqueDef), anything else by
        /// its base. Drops are rolled for the hero's class, but a save from before then holds other classes' uniques
        /// (11-24: a 별점술사 had auto-equipped the 잿불기사's 묘지기의 맹세).
        /// </summary>
        public static bool FitsClass(Item item, string classId)
        {
            if (item == null) return false;
            if (item.Rarity == Rarity.Unique && !string.IsNullOrEmpty(item.UniqueId))
            {
                var u = ItemData.Unique(item.UniqueId);
                if (u != null) return u.Fits(classId);
            }
            var b = ItemData.Base(item.BaseId);
            return b == null || b.Fits(classId);
        }

        /// <summary>Against the spot a click would use: an empty one of its kind first, else (two rings) the one it improves most.</summary>
        public static GearCompare Compare(Hero h, Item item)
        {
            var spots = Equipment.SpotsFor(item.Slot);
            foreach (var s in spots) if (h.Gear.Spots[(int)s] == null) return Compare(h, item, s);
            var best = Compare(h, item, spots[0]);
            for (int i = 1; i < spots.Length; i++)
            {
                var c = Compare(h, item, spots[i]);
                if (c.Power > best.Power) best = c;
            }
            return best;
        }

        /// <summary>The order of the plan's rules: a ◆ trade first (offence and survival pull apart, each by 5% or more), then the 전투력 steps.</summary>
        static GearArrow ArrowOf(float power, float offence, float survival)
        {
            float o = offence - 1f, s = survival - 1f;
            if (o * s < 0f && Math.Min(Math.Abs(o), Math.Abs(s)) >= TradeAt) return GearArrow.Trade;
            if (power >= BigUpAt) return GearArrow.BigUp;
            if (power >= UpAt) return GearArrow.Up;
            if (power <= DownAt) return GearArrow.Down;
            return GearArrow.Same;
        }

        /// <summary>The 전투력 ratio alone (1 = same, &gt;1 better) and the offence change; see <see cref="Compare(Hero, Item, EquipSpot)"/>.</summary>
        public static float Upgrade(Hero h, Item item, EquipSpot spot, out float dpsChange)
        {
            var c = Compare(h, item, spot);
            dpsChange = c.Offence - 1f;
            return c.Power;
        }
    }
}
