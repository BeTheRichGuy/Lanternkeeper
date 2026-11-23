using System;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// GDD §14 야영지 (M2 Task 5): opens the first time an act boss falls. Costs are ash shards + lantern oil (no gold
    /// yet). No timers, no failure: a purchase is instant.
    /// </summary>
    public sealed partial class SimWorld
    {
        public const int MaxMasterwork = 12, MaxOilStore = 4, MaxCartographer = 3;

        public bool CampUnlocked;
        /// <summary>등불 기름 저장고: each level adds 2 h to the offline cap (8 h → 16 h).</summary>
        public int OilStoreLevel;
        /// <summary>지도 제작자: 1 unlocks expedition orders, 2 the floor lock switch, 3 "도감 먼저".</summary>
        public int CartographerLevel;

        public double OfflineCapSeconds => (8 + 2 * OilStoreLevel) * 3600.0;

        public static (int ash, int oil) MasterworkCost(int current) => (25 * (current + 1), 1 + current / 2);
        public static (int ash, int oil) OilStoreCost(int level) => (50 * (level + 1), 5 * (level + 1));
        public static (int ash, int oil) CartographerCost(int level) => (40 * (level + 1), 4 * (level + 1));

        bool Pay((int ash, int oil) cost)
        {
            if (!CampUnlocked || Bag.AshShards < cost.ash || Bag.LanternOil < cost.oil) return false;
            Bag.AshShards -= cost.ash;
            Bag.LanternOil -= cost.oil;
            return true;
        }

        /// <summary>대장장이 명품화: +8% to every stat of the item in <paramref name="spot"/>, up to +12.</summary>
        public bool Masterwork(EquipSpot spot)
        {
            var item = Hero.Gear[spot];
            if (item == null || item.Masterwork >= MaxMasterwork || !Pay(MasterworkCost(item.Masterwork))) return false;
            item.Masterwork++;
            HeroStats.Recompute(Hero);
            Events.Add(new SimEvent { Type = SimEventType.Equip, TargetId = item.Uid, Pos = Hero.Pos, Item = item });
            return true;
        }

        // ---------- GDD §9 재굴림 (crafting at the camp's anvil) ----------

        public const int RerollStability = 4;
        public static (int ash, int oil) RerollCost(int rerolls) => (10 * (rerolls + 1), 0);

        /// <summary>True when line <paramref name="line"/> of this item may be rerolled now (budget and line rule).</summary>
        public static bool CanReroll(Item item, int line) =>
            item != null && line >= 0 && line < item.Affixes.Count && item.StabilityLeft >= RerollStability
            && (item.RerollLine < 0 || item.RerollLine == line);

        /// <summary>
        /// 재굴림: line <paramref name="line"/> of the item in <paramref name="spot"/> rolls a new affix. Costs 4 stability
        /// and ash shards that climb with every reroll; once a line is picked, only that line can be rerolled.
        /// </summary>
        public bool Reroll(EquipSpot spot, int line)
        {
            var item = Hero.Gear[spot];
            if (!CanReroll(item, line) || !Pay(RerollCost(item.Rerolls))) return false;
            item.Stability = item.StabilityLeft - RerollStability;
            item.Rerolls++;
            item.RerollLine = line;
            item.Affixes[line] = ItemGen.RerollAffix(_lootRng, item, line);
            if (item.Rarity == Rarity.Magic && line == 0) // magic names come from the first line's adjective
                item.Name = ItemData.Affix(item.Affixes[0].Stat).Adjective + " " + ItemData.Base(item.BaseId).Name;
            HeroStats.Recompute(Hero);
            Events.Add(new SimEvent { Type = SimEventType.Equip, TargetId = item.Uid, Pos = Hero.Pos, Item = item });
            return true;
        }

        // ---------- GDD §9 재련 (Temper) ----------

        public const int TemperStability = 6;
        public static int MaxAffixes(Rarity r) => r == Rarity.Magic ? 2 : r == Rarity.Rare || r == Rarity.Legendary ? 4 : 0;
        public static (int ash, int oil) TemperCost(Item item) => (30 * (item.Affixes.Count + 1), 1);

        public static bool CanTemper(Item item) =>
            item != null && item.Affixes.Count < MaxAffixes(item.Rarity) && item.StabilityLeft >= TemperStability
            && ItemGen.FreeAffixes(item) > 0;

        /// <summary>재련: one more affix line on the item (from the slot's pool, no duplicate stat), for 6 stability.</summary>
        public bool Temper(EquipSpot spot)
        {
            var item = Hero.Gear[spot];
            if (!CanTemper(item) || !Pay(TemperCost(item))) return false;
            item.Stability = item.StabilityLeft - TemperStability;
            item.Affixes.Add(default);
            item.Affixes[item.Affixes.Count - 1] = ItemGen.RerollAffix(_lootRng, item, item.Affixes.Count - 1);
            HeroStats.Recompute(Hero);
            Events.Add(new SimEvent { Type = SimEventType.Equip, TargetId = item.Uid, Pos = Hero.Pos, Item = item });
            return true;
        }

        // ---------- GDD §9 변환 제단: 룬 승급 3 → 1 ----------

        public const int TransmuteAsh = 20;

        /// <summary>The rune the altar would take three of (the most plentiful with three or more, not the last rune), or None.</summary>
        public Rune TransmuteFrom
        {
            get
            {
                Rune best = Rune.None;
                int bestN = 2;
                for (int i = 1; i < Runes.Count - 1; i++)
                    if (Bag.RuneCounts[i] > bestN) { bestN = Bag.RuneCounts[i]; best = (Rune)i; }
                return best;
            }
        }

        /// <summary>룬 승급: three of the most plentiful rune become one of the next rune.</summary>
        public bool TransmuteRune()
        {
            var from = TransmuteFrom;
            if (from == Rune.None || !Pay((TransmuteAsh, 0))) return false;
            Bag.RuneCounts[(int)from] -= 3;
            Bag.RuneCounts[(int)from + 1]++;
            Events.Add(new SimEvent { Type = SimEventType.RuneDrop, Pos = Hero.Pos, Value = (int)from + 1 });
            SocketRunes(); // a runeword may now be complete
            return true;
        }

        // ---------- GDD §9 도박 (수상한 상인) ----------

        /// <summary>Ash shards for one gamble: grows with the area level so it stays a real choice.</summary>
        public int GambleCost => 30 + 3 * AreaLevel;

        /// <summary>
        /// 도박: pick the slot, pay ash shards, get an unidentified item of that slot at the area's item level — at least
        /// magic, with +150% magic find. It goes through the usual auto-equip and filter.
        /// </summary>
        public Item Gamble(Slot slot)
        {
            if (!Pay((GambleCost, 0))) return null;
            return DropItem(Hero.Pos, Rarity.Magic, slot, 150f);
        }

        public bool UpgradeOilStore()
        {
            if (OilStoreLevel >= MaxOilStore || !Pay(OilStoreCost(OilStoreLevel))) return false;
            OilStoreLevel++;
            return true;
        }

        public bool UpgradeCartographer()
        {
            if (CartographerLevel >= MaxCartographer || !Pay(CartographerCost(CartographerLevel))) return false;
            CartographerLevel++;
            return true;
        }

        void UnlockCampIfActBoss(Enemy e)
        {
            if (CampUnlocked || !e.Boss || !IsActBossFloor) return;
            CampUnlocked = true;
            Events.Add(new SimEvent { Type = SimEventType.CampUnlocked, Pos = e.Pos });
        }
    }
}
