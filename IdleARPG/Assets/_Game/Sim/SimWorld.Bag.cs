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
        public EquipSpot SpotFor(Item item) => HeroStats.Compare(Hero, item).Spot;

        /// <summary>Wears a bag item (GDD §8.2: 통합 레벨 must reach it). The replaced piece takes its place in the bag.</summary>
        public bool EquipFromBag(int uid)
        {
            int idx = Bag.Items.FindIndex(i => i.Uid == uid);
            if (idx < 0) return false;
            var item = Bag.Items[idx];
            if (item.ReqLevel > Hero.IntegratedLevel || !HeroStats.FitsClass(item, Hero.ClassId)) return false;
            var old = Hero.Gear.Equip(item, SpotFor(item));
            AutoEquipDeclined.Remove(uid); // worn by hand: no longer set aside
            Bag.Items.RemoveAt(idx);
            if (old != null) Bag.Items.Insert(idx, old);
            HeroStats.Recompute(Hero);
            SocketRunes();
            return true;
        }

        /// <summary>
        /// True while the last auto-equip can be taken back (UX plan A6): its piece is still worn where it went, and the old
        /// one is in the bag — or was salvaged on the spot and the ash it gave is still there to buy it back.
        /// </summary>
        public bool CanUndoAutoEquip
        {
            get
            {
                var s = LastAutoSwap;
                if (s == null || Hero.Gear.Spots[(int)s.Spot] != s.New) return false;
                if (s.Old != null && Bag.Items.Contains(s.Old)) return true; // the two trade places
                if (Bag.Items.Count >= Bag.Capacity) return false;           // the new piece needs a place
                return s.Old == null || (s.AshPaid > 0 && Bag.AshShards >= s.AshPaid);
            }
        }

        /// <summary>
        /// 직전 교체 되돌리기: the old piece goes back on, the new one into the bag, and auto-equip leaves that piece alone
        /// from then on (it would otherwise come straight back at the next level).
        /// </summary>
        public bool UndoAutoEquip()
        {
            if (!CanUndoAutoEquip) return false;
            var s = LastAutoSwap;
            int idx = s.Old != null ? Bag.Items.IndexOf(s.Old) : -1;
            if (idx >= 0) Bag.Items[idx] = s.New;
            else
            {
                if (s.Old != null) Bag.AshShards -= s.AshPaid;
                Bag.Items.Add(s.New);
            }
            Hero.Gear.Equip(s.Old, s.Spot);
            AutoEquipDeclined.Add(s.New.Uid);
            LastAutoSwap = null;
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
            int idx = Bag.Items.FindIndex(i => i.Uid == uid);
            if (idx < 0 || Bag.Items[idx].Locked) return false;
            var it = Bag.Items[idx];
            Bag.Items.RemoveAt(idx);
            int ash = LootFilter.SalvageValue(it);
            Bag.AshShards += ash;
            LastSalvaged = it; // kept for Z (UX plan A7): the piece, its place and what it paid
            LastSalvagedAsh = ash;
            _lastSalvagedIndex = idx;
            return true;
        }

        /// <summary>The piece the last hand salvage broke (null once undone or after a bulk salvage). Not saved, not hashed.</summary>
        public Item LastSalvaged { get; private set; }
        public int LastSalvagedAsh { get; private set; }
        int _lastSalvagedIndex;

        /// <summary>Z can put it back only while the ash it gave is still there and the bag has a place.</summary>
        public bool CanUndoSalvage =>
            LastSalvaged != null && Bag.Items.Count < Bag.Capacity && Bag.AshShards >= LastSalvagedAsh;

        /// <summary>분해 되돌리기: the piece returns to its old place and the ash it paid is taken back.</summary>
        public bool UndoSalvage()
        {
            if (!CanUndoSalvage) return false;
            Bag.AshShards -= LastSalvagedAsh;
            Bag.Items.Insert(System.Math.Min(_lastSalvagedIndex, Bag.Items.Count), LastSalvaged);
            LastSalvaged = null;
            return true;
        }

        /// <summary>
        /// What 일괄 분해 takes: not locked, and below legendary with no ★ line — or any piece this class can never wear
        /// (11-24: an older save's bag held a dozen of another class's uniques).
        /// </summary>
        public bool BulkSalvageable(Item it) => !it.Locked && ((it.Rarity < Rarity.Legendary && it.GreaterCount == 0) || !HeroStats.FitsClass(it, Hero.ClassId));

        /// <summary>How many of each rarity (index = <see cref="Rarity"/>) 일괄 분해 would take; read-only, for its confirm box.</summary>
        public int[] BulkSalvageCounts()
        {
            var n = new int[(int)Rarity.Unique + 1];
            foreach (var it in Bag.Items) if (BulkSalvageable(it)) n[(int)it.Rarity]++;
            return n;
        }

        /// <summary>일괄 분해: everything below legendary without a ★ line and not locked. Returns how many went.</summary>
        public int SalvageAll()
        {
            int n = 0;
            for (int i = Bag.Items.Count - 1; i >= 0; i--)
            {
                var it = Bag.Items[i];
                if (!BulkSalvageable(it)) continue;
                Bag.Items.RemoveAt(i);
                Bag.AshShards += LootFilter.SalvageValue(it);
                n++;
            }
            return n;
        }
    }
}
