using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>Rolls rarity and items (GDD §8.1–8.2, §11.4). Deterministic given the SimRng.</summary>
    public static class ItemGen
    {
        /// <summary>GDD §11.4 weights before magic find. Unique only from Lv15.</summary>
        public static Rarity RollRarity(SimRng rng, int heroLevel, float magicFindPct)
        {
            float mf = 1f + Math.Max(0f, magicFindPct) / 100f;
            // GDD §8.1 first appearance: rare and legendary from Lv3 (so the Lv3 legendary guarantee is the first one), unique from Lv15.
            float common = 60f, magic = 28f;
            float rare = heroLevel >= 3 ? 10f * mf : 0f;
            // 재기획 §8 드롭 경제 (11-21): legendaries were about 15 an hour (bags full of orange, one drop meant nothing);
            // a quarter of the old weight gives about 3–4, act bosses still guarantee one.
            float legendary = heroLevel >= 3 ? LegendaryWeight * mf : 0f;
            float unique = heroLevel >= 15 ? UniqueWeight * mf : 0f;
            float roll = rng.NextFloat() * (common + magic + rare + legendary + unique);
            if ((roll -= unique) < 0f) return Rarity.Unique;
            if ((roll -= legendary) < 0f) return Rarity.Legendary;
            if ((roll -= rare) < 0f) return Rarity.Rare;
            if ((roll -= magic) < 0f) return Rarity.Magic;
            return Rarity.Common;
        }

        public static int AffixCount(SimRng rng, Rarity r)
        {
            switch (r)
            {
                case Rarity.Magic: return rng.Range(1, 3);
                case Rarity.Rare: return rng.Range(3, 5);
                case Rarity.Legendary: return 3;
                default: return 0;
            }
        }

        public static Item Roll(SimRng rng, int uid, int ilvl, Rarity rarity) => Roll(rng, uid, ilvl, rarity, null, 0f);

        /// <summary>
        /// Chance of a ★ line on rare and better: none before area level 20, then 0.5% a line rising 0.1% a level, up to 5%.
        /// (2→12% made about seven ★ pieces an hour in a 20 h sim — too common to be the chase.)
        /// </summary>
        public static float GreaterChance(int areaLevel) => areaLevel < GreaterFrom ? 0f : Math.Min(0.05f, 0.005f + (areaLevel - GreaterFrom) * 0.001f);

        /// <summary>★ from act II (area Lv10, was 20 ≈ 9 hours in): the early hours get a chase beyond rarity colours.</summary>
        public const int GreaterFrom = 10;
        public const float LegendaryWeight = 0.4f, UniqueWeight = 0.2f;

        /// <summary><paramref name="slot"/>: only bases of that slot (도박, GDD §9); a unique then becomes a legendary.</summary>
        public static Item Roll(SimRng rng, int uid, int ilvl, Rarity rarity, Slot? slot, float greaterChance, string classId = null)
        {
            ilvl = Math.Max(1, Math.Min(100, ilvl));
            if (rarity == Rarity.Unique && slot == null && HasUniqueFor(classId)) return Greaten(rng, RollUnique(rng, uid, ilvl, classId), greaterChance);
            if (rarity == Rarity.Unique) rarity = Rarity.Legendary;

            BaseDef b;
            if (slot == null)
            {
                var mine = Array.FindAll(ItemData.Bases, x => x.Fits(classId)); // 희승 11-22: 내 직업 것만
                b = mine[rng.Range(0, mine.Length)];
            }
            else
            {
                var of = Array.FindAll(ItemData.Bases, x => x.Slot == slot.Value && x.Fits(classId));
                b = of[rng.Range(0, of.Length)];
            }
            var item = new Item
            {
                Uid = uid,
                BaseId = b.Id,
                Rarity = rarity,
                Ilvl = ilvl,
                ReqLevel = Math.Max(1, ilvl - 5),
                Implicit = new AffixRoll { Stat = b.ImplicitStat, Value = Round(b.ImplicitValue * (1f + (ilvl - 1) / 20f)), Tier = 0 },
            };

            var pool = new List<AffixDef>();
            foreach (var a in ItemData.Affixes) if (Array.IndexOf(a.Slots, b.Slot) >= 0) pool.Add(a);
            int count = Math.Min(AffixCount(rng, rarity), pool.Count);
            int maxT = ItemData.MaxTier(ilvl);
            for (int i = 0; i < count; i++)
            {
                var def = pool[rng.Range(0, pool.Count)];
                pool.Remove(def);
                int tier = rng.Range(Math.Max(1, maxT - 2), maxT + 1);
                float v = rng.Range(def.Min, def.Max) * ItemData.TierScale[tier - 1];
                item.Affixes.Add(new AffixRoll { Stat = def.Stat, Value = WholeIfCount(def.Stat, Round(v), tier), Tier = tier });
            }

            // GDD §8.7: weapons and body armour roll up to three sockets, helms up to two.
            if (b.Slot == Slot.Weapon || b.Slot == Slot.Armor || b.Slot == Slot.Helm)
            {
                float roll = rng.NextFloat();
                item.Sockets = roll < 0.55f ? 0 : roll < 0.85f || b.Slot == Slot.Helm ? 2 : 3;
            }

            switch (rarity)
            {
                case Rarity.Legendary:
                    var ess = ItemData.Essences[rng.Range(0, ItemData.Essences.Length)];
                    item.EssenceId = ess.Id;
                    item.Name = ess.Adjective + " " + b.Name;
                    break;
                case Rarity.Rare:
                    item.Name = ItemData.RareName(rng, b.Slot);
                    break;
                case Rarity.Magic:
                    item.Name = ItemData.Affix(item.Affixes[0].Stat).Adjective + " " + b.Name;
                    break;
                default:
                    item.Name = b.Name;
                    break;
            }
            return rarity >= Rarity.Rare ? Greaten(rng, item, greaterChance) : item;
        }

        /// <summary>★ 대옵션: each line of a rare-or-better item may roll greater (×1.5).</summary>
        static Item Greaten(SimRng rng, Item item, float chance)
        {
            if (chance <= 0f) return item;
            for (int i = 0; i < item.Affixes.Count; i++)
            {
                if (rng.NextFloat() >= chance) continue;
                var a = item.Affixes[i];
                a.Greater = true;
                a.Value = Round(a.Value * (a.Stat == Stat.ProjectileCount ? 1f : 1.5f) + (a.Stat == Stat.ProjectileCount ? 1f : 0f));
                item.Affixes[i] = a;
            }
            return item;
        }

        /// <summary>GDD §9 재굴림: a new affix for one line, from the slot's pool minus the stats the other lines have.</summary>
        /// <summary>Affix stats the item's slot offers that it does not have yet (재련 needs at least one).</summary>
        public static int FreeAffixes(Item item)
        {
            var slot = ItemData.Base(item.BaseId).Slot;
            int n = 0;
            foreach (var a in ItemData.Affixes)
            {
                if (Array.IndexOf(a.Slots, slot) < 0) continue;
                bool taken = false;
                foreach (var r in item.Affixes) if (r.Stat == a.Stat) taken = true;
                if (!taken) n++;
            }
            return n;
        }

        public static AffixRoll RerollAffix(SimRng rng, Item item, int line)
        {
            var slot = ItemData.Base(item.BaseId).Slot;
            var pool = new List<AffixDef>();
            foreach (var a in ItemData.Affixes)
            {
                if (Array.IndexOf(a.Slots, slot) < 0) continue;
                bool taken = false;
                for (int i = 0; i < item.Affixes.Count; i++) if (i != line && item.Affixes[i].Stat == a.Stat) taken = true;
                if (!taken) pool.Add(a);
            }
            var def = pool[rng.Range(0, pool.Count)];
            int maxT = ItemData.MaxTier(item.Ilvl);
            int tier = rng.Range(Math.Max(1, maxT - 2), maxT + 1);
            return new AffixRoll { Stat = def.Stat, Value = WholeIfCount(def.Stat, Round(rng.Range(def.Min, def.Max) * ItemData.TierScale[tier - 1]), tier), Tier = tier };
        }

        static bool HasUniqueFor(string classId) => Array.Exists(ItemData.Uniques, u => ItemData.Base(u.BaseId).Fits(classId));

        static Item RollUnique(SimRng rng, int uid, int ilvl, string classId)
        {
            var mine = Array.FindAll(ItemData.Uniques, x => ItemData.Base(x.BaseId).Fits(classId));
            var u = mine[rng.Range(0, mine.Length)];
            var b = ItemData.Base(u.BaseId);
            int tier = ItemData.MaxTier(ilvl);
            var item = new Item
            {
                Uid = uid,
                BaseId = b.Id,
                Rarity = Rarity.Unique,
                Ilvl = ilvl,
                ReqLevel = Math.Max(1, ilvl - 5),
                Name = u.Name,
                EssenceId = u.EssenceId,
                Implicit = new AffixRoll { Stat = b.ImplicitStat, Value = Round(b.ImplicitValue * (1f + (ilvl - 1) / 20f)), Tier = 0 },
            };
            foreach (var (stat, value) in u.Fixed)
                item.Affixes.Add(new AffixRoll { Stat = stat, Value = Round(value * (1f + (ilvl - 15) / 40f)), Tier = tier });
            return item;
        }

        static float Round(float v) => (float)Math.Round(v, 1);

        /// <summary>투사체·분신 is a count: one copy per line (two from tier 7), never a fraction the floor would eat.</summary>
        static float WholeIfCount(Stat s, float v, int tier) => s == Stat.ProjectileCount ? (tier >= 7 ? 2f : 1f) : v;
    }
}
