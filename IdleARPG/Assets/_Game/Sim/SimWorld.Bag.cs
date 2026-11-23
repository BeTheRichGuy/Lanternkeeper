namespace Lanternkeeper.Sim
{
    /// <summary>
    /// 2026-11-21 재기획 §5.4: the gear panel works the bag by hand — equip, lock, salvage one, salvage all.
    /// Called between ticks from the GUI; nothing here emits events (the panel redraws from state).
    /// </summary>
    public sealed partial class SimWorld
    {
        public Item FindInBag(int uid) => Bag.Items.Find(i => i.Uid == uid);

        /// <summary>The item wherever it is: bag or worn (null if neither).</summary>
        public Item FindItem(int uid)
        {
            var it = FindInBag(uid);
            if (it != null) return it;
            foreach (var worn in Hero.Gear.Spots) if (worn != null && worn.Uid == uid) return worn;
            return null;
        }

        /// <summary>Where a bag item would go: an empty spot of its kind first, else the one it improves most.</summary>
        public EquipSpot SpotFor(Item item)
        {
            var spots = Equipment.SpotsFor(item.Slot);
            foreach (var s in spots) if (Hero.Gear.Spots[(int)s] == null) return s;
            var best = spots[0];
            float bestScore = float.MinValue;
            foreach (var s in spots)
            {
                float score = HeroStats.Upgrade(Hero, item, s, out _);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        /// <summary>Wears a bag item (GDD §8.2: 통합 레벨 must reach it). The replaced piece takes its place in the bag.</summary>
        public bool EquipFromBag(int uid)
        {
            int idx = Bag.Items.FindIndex(i => i.Uid == uid);
            if (idx < 0) return false;
            var item = Bag.Items[idx];
            if (item.ReqLevel > Hero.IntegratedLevel) return false;
            var old = Hero.Gear.Equip(item, SpotFor(item));
            Bag.Items.RemoveAt(idx);
            if (old != null) Bag.Items.Insert(idx, old);
            HeroStats.Recompute(Hero);
            SocketRunes();
            return true;
        }

        /// <summary>잠금 on or off, for a bag item or a worn one.</summary>
        public void ToggleLock(int uid)
        {
            var it = FindItem(uid);
            if (it != null) it.Locked = !it.Locked;
        }

        /// <summary>Breaks one bag item into ash shards. A locked item refuses.</summary>
        public bool SalvageFromBag(int uid)
        {
            var it = FindInBag(uid);
            if (it == null || it.Locked) return false;
            Bag.Items.Remove(it);
            Bag.AshShards += LootFilter.SalvageValue(it);
            return true;
        }

        /// <summary>일괄 분해: everything below legendary without a ★ line and not locked. Returns how many went.</summary>
        public int SalvageAll()
        {
            int n = 0;
            for (int i = Bag.Items.Count - 1; i >= 0; i--)
            {
                var it = Bag.Items[i];
                if (it.Locked || it.Rarity >= Rarity.Legendary || it.GreaterCount > 0) continue;
                Bag.Items.RemoveAt(i);
                Bag.AshShards += LootFilter.SalvageValue(it);
                n++;
            }
            return n;
        }
    }
}
