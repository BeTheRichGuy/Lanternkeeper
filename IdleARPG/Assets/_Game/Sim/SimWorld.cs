using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// The whole game state for one delve, advanced in fixed 10 Hz ticks (GDD §19.1).
    /// No UnityEngine here: View reads public state and <see cref="Events"/>, never writes back.
    /// </summary>
    public sealed partial class SimWorld
    {
        public const float Dt = SimClock.TickSeconds;
        public const int FloorsPerZone = 4;
        public const int DescendDelayTicks = 25; // 2.5 s on a cleared floor before going down
        int _descendAtTick = -1;
        /// <summary>GDD §6.1 rooms per floor, deeper floors of a zone get more: 8/10/12/14 (2026-11-19 pace pass: act I boss at ≈45–105 min).</summary>
        public static int RoomsFor(int floorInZone) => RoomsBase + RoomsPerFloor * floorInZone;
        public static int RoomsBase = 8, RoomsPerFloor = 2;
        const float AggroRadius = 7f;  // sight: an enemy that sees the hero this close charges
        const float AlertRadius = 6f;  // ...and calls its pack within this
        const float ChargeMul = 1.5f;  // charging enemies run (희승: 가만히 서 있는 샌드백 같다)
        const float WanderMul = 0.35f;
        const float MeleeRange = 1.1f;

        readonly int _seed;
        SimRng _rng;
        /// <summary>Crits and loot roll on streams that never reset with the floor.</summary>
        readonly SimRng _combatRng, _lootRng;
        int _nextId = 1, _nextCascade = 1;
        int _pathTick;
        /// <summary>1: the class signature skill sets (11-21).</summary>
        public const int SkillSetVersion = 1;
        int _engagedId = -1;
        double _castLock;
        int _waitTicks;
        int[] _flow;
        int _flowCell = -1;
        Floor _flowFloor;
        List<Vec2> _path = new List<Vec2>();
        Vec2 _pathGoal;


        public int TickCount { get; private set; }
        /// <summary>Seconds since start. Double so timers stay exact on overlays left running for days.</summary>
        public double Time => TickCount * (double)Dt;
        public int FloorIndex { get; private set; }
        /// <summary>GDD §6.1: an act is 3 zones × 4 floors, then the act boss floor (index 12 within the act).</summary>
        public const int ActLength = 13, ActBossFloorInAct = 12;
        public int FloorInAct => FloorIndex % ActLength;
        public int ActLoop => FloorIndex / ActLength;
        /// <summary>GDD §6.3: the act being delved (I 묘실, II 수도원, …); past the last act it repeats.</summary>
        public ActDef ActNow => EnemyRoster.ForAct(ActLoop);
        public bool IsActBossFloor => FloorInAct == ActBossFloorInAct;
        /// <summary>0..2 for the three zones, 3 for the act boss floor.</summary>
        public int Zone => IsActBossFloor ? 3 : FloorInAct / FloorsPerZone;
        public int FloorInZone => IsActBossFloor ? 0 : FloorInAct % FloorsPerZone;
        /// <summary>Death checkpoint: first floor of the current zone (the boss floor is its own checkpoint).</summary>
        /// <remarks>Dying to the act boss sends the hero back to the last zone's first floor to grow stronger; the boss
        /// floor as its own checkpoint was a death loop (2026-11-19 sim: 80 deaths an hour on floor 12).</remarks>
        public int ZoneStartFloor => IsActBossFloor ? FloorIndex - FloorsPerZone : FloorIndex - FloorInZone;
        public int Deaths { get; private set; }
        public Floor Floor { get; private set; }
        public readonly Hero Hero;
        readonly List<Enemy> _enemies = new List<Enemy>();
        public IReadOnlyList<Enemy> Enemies => _enemies;
        public readonly List<SimEvent> Events = new List<SimEvent>();

        /// <summary>Test switches: freeze the hero's or enemies' decision making.</summary>
        public bool HeroBrainEnabled = true, EnemyBrainEnabled = true;

        // ---------- loot state (GDD §8, §10) ----------
        public readonly Bag Bag = new Bag();
        public FilterPreset Filter = FilterPreset.Normal;
        /// <summary>
        /// 자동 분해 (희승 11-22, replaces the filter presets in play): a drop of this rarity or lower turns to ash the
        /// moment it falls; −1 keeps everything. Legendary and better, ★ pieces and two-socket runeword bases are always kept.
        /// </summary>
        public int AutoSalvage = (int)Rarity.Common;
        public const int AutoSalvageMax = (int)Rarity.Rare;

        public bool IsJunk(Item it) =>
            !it.Locked && it.GreaterCount == 0 && it.Sockets < 2 && it.Rarity < Rarity.Legendary && (int)it.Rarity <= AutoSalvage;
        /// <summary>The player's own loot rules (재기획 §8), used when <see cref="Filter"/> is 직접.</summary>
        public readonly List<LootRule> CustomRules = LootFilter.StarterRules();
        public bool AutoEquip = true;
        /// <summary>
        /// The last auto-equip, kept so the gear window can put the old piece back (UX plan A6: what the game did on its
        /// own can be undone). <see cref="AshPaid"/> &gt; 0 when the old piece already went to ash (자동 분해, or a full
        /// bag): undo buys it back for what it gave. Not saved.
        /// </summary>
        public sealed class AutoSwap
        {
            public Item New, Old;
            public EquipSpot Spot;
            /// <summary>The 전투력 ratio the swap was made for.</summary>
            public float Power;
            public int AshPaid;
        }
        public AutoSwap LastAutoSwap { get; private set; }
        /// <summary>Pieces the player took back off with the undo: auto-equip leaves them in the bag from then on.</summary>
        public readonly HashSet<int> AutoEquipDeclined = new HashSet<int>();
        public int NextItemUid = 1;
        public bool FirstLegendaryGiven;
        public int Kills { get; private set; }
        /// <summary>The last three kept or equipped drops, newest first (diorama HUD).</summary>
        public readonly List<Item> RecentDrops = new List<Item>();

        // ---------- efficiency (GDD §13: last 30 minutes) ----------
        public long TotalXp { get; private set; }
        const int TicksPerMinute = 600, WindowMinutes = 30;
        readonly Queue<(float kills, float xp, float drops)> _minutes = new Queue<(float, float, float)>();
        int _minuteKills0, _minuteDrops0, _dropsTotal;
        long _minuteXp0;

        public float KillsPerMinute => Average(0);
        public float XpPerMinute => Average(1);
        /// <summary>Items per minute including boss and chest drops (GDD §13 counts drops, not just kills).</summary>
        public float DropsPerMinute => Average(2);

        float Average(int which)
        {
            if (_minutes.Count == 0) return 0f;
            double sum = 0;
            foreach (var m in _minutes) sum += which == 0 ? m.kills : which == 1 ? m.xp : m.drops;
            return (float)(sum / _minutes.Count);
        }

        void TrackEfficiency()
        {
            if (TickCount % TicksPerMinute != 0) return;
            _minutes.Enqueue((Kills - _minuteKills0, TotalXp - _minuteXp0, _dropsTotal - _minuteDrops0));
            if (_minutes.Count > WindowMinutes) _minutes.Dequeue();
            ResetMinuteBaseline();
        }

        void ResetMinuteBaseline()
        {
            _minuteKills0 = Kills;
            _minuteXp0 = TotalXp;
            _minuteDrops0 = _dropsTotal;
        }

        /// <summary>After a load, fill the 30-minute window with the saved averages so a short session can't wipe them.</summary>
        void SeedEfficiency(float kpm, float xpm, float dpm)
        {
            _minutes.Clear();
            if (kpm <= 0f && xpm <= 0f) return;
            for (int i = 0; i < WindowMinutes; i++) _minutes.Enqueue((kpm, xpm, dpm));
        }

        // ---------- save / load ----------

        public SaveData ToSave()
        {
            var s = new SaveData
            {
                Seed = _seed,
                ClassId = Hero.ClassId,
                Level = Hero.Level,
                Xp = Hero.Xp,
                EmberLevel = Hero.EmberLevel,
                OverflowXp = Hero.OverflowXp,
                WickLevel = Hero.WickLevel,
                WickXp = Hero.WickXp,
                Rebirths = Hero.Rebirths,
                DeepestActCleared = Hero.DeepestActCleared,
                HpFraction = Hero.Stats.MaxHp > 0f ? Hero.Stats.Hp / Hero.Stats.MaxHp : 1f,
                FloorIndex = FloorIndex,
                Deaths = Deaths,
                Kills = Kills,
                NextItemUid = NextItemUid,
                AshShards = Bag.AshShards,
                LanternOil = Bag.LanternOil,
                FirstLegendaryGiven = FirstLegendaryGiven,
                Filter = (int)Filter,
                AutoSalvage = AutoSalvage,
                Augments = new List<string>(Hero.Augments),
                AugOffer = Hero.AugOffer != null ? new List<string>(Hero.AugOffer) : null,
                AugRerolled = Hero.AugRerolled,
                AugDeals = Hero.AugDeals,
                BranchSets = new List<string>(Hero.BranchSets),
                AugSwitchSets = Hero.AugSwitchSets,
                CustomRules = FlattenRules(CustomRules),
                CustomRulesSet = true,
                AutoEquip = AutoEquip,
                AutoEquipDeclined = Bag.Items.FindAll(i => AutoEquipDeclined.Contains(i.Uid)).ConvertAll(i => i.Uid), // only what is still in the bag
                LootRng = _lootRng.State,
                CombatRng = _combatRng.State,
                KillsPerMinute = KillsPerMinute,
                XpPerMinute = XpPerMinute,
                DropsPerMinute = DropsPerMinute,
            };
            for (int i = 0; i < Hero.Gear.Spots.Length; i++)
                if (Hero.Gear.Spots[i] != null) s.Equipped.Add(new EquippedEntry { Spot = i, Item = Hero.Gear.Spots[i] });
            s.Bag.AddRange(Bag.Items);
            foreach (var it in RecentDrops) s.RecentDropUids.Add(it.Uid);
            s.Preset = Hero.Preset;
            s.TalentFocus = Hero.TalentFocus;
            s.UseCustomRing = Hero.UseCustomRing;
            s.SkillSet = SkillSetVersion;
            s.CustomSkills.AddRange(Hero.CustomSkills);
            foreach (var g in Hero.CustomSigils) s.CustomSigils.Add((int)g);
            s.RuneCounts.AddRange(Bag.RuneCounts);
            s.RunewordsFound.AddRange(Hero.RunewordsFound);
            foreach (var kv in Hero.KillsById) { s.KillIds.Add(kv.Key); s.KillCounts.Add(kv.Value); }
            s.Achieved.AddRange(Hero.Achieved);
            s.Letters.AddRange(Hero.Letters);
            s.LettersRead.AddRange(Hero.LettersRead);
            s.GrandCount = Hero.GrandCount; s.MaxChainDepth = Hero.MaxChainDepth; s.IgniteCount = Hero.IgniteCount; s.LanternColor = Hero.LanternColor;
            s.Order = (int)Order;
            s.CampUnlocked = CampUnlocked;
            s.OilStoreLevel = OilStoreLevel;
            s.CartographerLevel = CartographerLevel;
            s.FloorLock = FloorLock;
            s.CodexFirst = CodexFirst;
            s.TopLoop = TopLoop;
            s.AutoIgnite = AutoIgnite;
            s.Stance = (int)Stance;
            s.Gauge = Hero.Gauge;
            foreach (var kv in Hero.SkillUses) { s.SkillUseIds.Add(kv.Key); s.SkillUseCounts.Add(kv.Value); }
            s.Evolved.AddRange(Hero.Evolved);
            s.TreeVersion = 1;
            s.TreeNodes.AddRange(Hero.TreeOrder);
            s.TreeAuto = Hero.TreeAuto;
            s.LegacyEvolved.AddRange(Hero.LegacyEvolved);
            s.ResonanceCounts.AddRange(Hero.ResonanceCounts);
            return s;
        }

        /// <summary>
        /// GDD §13: time away (capped at 8 h) × the last 30 minutes' kill and XP rate × 80%.
        /// Drops are rolled with the same odds and filter; legendaries are always kept. Deterministic.
        /// </summary>
        public OfflineReport ApplyOffline(double seconds)
        {
            var r = new OfflineReport { Seconds = seconds, LevelBefore = Hero.Level, LevelAfter = Hero.Level };
            if (seconds < OfflineRules.MinSeconds) return r;
            r.CappedSeconds = Math.Min(seconds, OfflineCapSeconds);
            double minutes = r.CappedSeconds / 60.0;
            r.Kills = (int)Math.Round(KillsPerMinute * minutes * OfflineRules.Efficiency);
            r.XpGained = (long)Math.Round(XpPerMinute * minutes * OfflineRules.Efficiency);
            int drops = (int)Math.Round(DropsPerMinute * minutes * OfflineRules.Efficiency);

            int ash0 = Bag.AshShards;
            Events.Clear();
            _eventsAtTickEnd = 0;
            // XP rate already includes gear XP bonuses, so add it raw.
            int wick0 = Hero.WickLevel;
            Hero.Xp += (int)Math.Min(int.MaxValue - Hero.Xp, r.XpGained);
            TotalXp += r.XpGained;
            while (Hero.Xp >= Hero.XpToNext)
            {
                if (Hero.Level >= LevelCap) { PastCap(); break; } // GDD §11.5 grey ember, or the wick at 통합 100
                Hero.Xp -= Hero.XpToNext;
                Hero.Level++;
            }
            r.WickLevelsGained = Hero.WickLevel - wick0;
            HeroStats.Recompute(Hero);
            Hero.Stats.Hp = Hero.Stats.MaxHp;
            RefreshRing();
            ReequipFromBag();

            // 숙련 grows while away too: each ring skill as often as its cooldown (or the swing) allows, at the offline
            // efficiency and about half the time spent casting (walking between packs is the rest).
            if (r.Kills > 0)
            {
                var ring = new List<SkillDef>(Hero.Ring.Slots) { Hero.Ring.Basic };
                foreach (var s in ring)
                {
                    if (s == null) continue;
                    double every = s == Hero.Ring.Basic ? 1.0 / Math.Max(0.1, Hero.Stats.AttackSpeed) * 3.0 : Math.Max(s.Cooldown, 1.0 / Math.Max(0.1, Hero.Stats.AttackSpeed));
                    int add = (int)Math.Round(minutes * 60.0 / every * OfflineRules.Efficiency * 0.5);
                    Hero.SkillUses.TryGetValue(s.BaseId, out int u0);
                    int u1 = (int)Math.Min(int.MaxValue / 2, (long)u0 + add);
                    Hero.SkillUses[s.BaseId] = u1;
                    r.SkillLevelsGained += SkillBook.SkillLevel(u1) - SkillBook.SkillLevel(u0);
                }
            }

            var dropped = new List<Item>();
            for (int i = 0; i < drops; i++)
            {
                var item = DropItem(Hero.Pos, Rarity.Common);
                dropped.Add(item);
                if (item.Rarity >= Rarity.Legendary) r.Legendaries.Add(item);
            }
            Events.Clear();
            _eventsAtTickEnd = 0;
            Kills += r.Kills;
            ResetMinuteBaseline();
            foreach (var it in dropped)
            {
                if (Array.IndexOf(Hero.Gear.Spots, it) >= 0) r.Equipped.Add(it); // still worn at the end
                else if (Bag.Items.Contains(it)) r.ItemsKept++;
            }
            r.LevelAfter = Hero.Level;
            r.AshGained = Bag.AshShards - ash0;
            return r;
        }

        /// <summary>Rebuilds a world from a save. The current floor restarts from its entrance.</summary>
        public static SimWorld FromSave(SaveData s)
        {
            var w = new SimWorld(s.Seed, string.IsNullOrEmpty(s.ClassId) ? "ashknight" : s.ClassId);
            var h = w.Hero;
            h.Level = Math.Max(1, s.Level);
            h.Xp = Math.Max(0, s.Xp);
            h.EmberLevel = Math.Max(0, Math.Min(EmberCap, s.EmberLevel));
            h.OverflowXp = Math.Max(0L, s.OverflowXp);
            h.WickLevel = Math.Max(0, s.WickLevel);
            h.WickXp = Math.Max(0L, s.WickXp);
            h.Rebirths = Math.Max(0, s.Rebirths);
            // Saves from before rebirth existed: an act the hero is already past was beaten to its boss.
            h.DeepestActCleared = Math.Max(s.DeepestActCleared, s.FloorIndex / ActLength - 1);
            // Drop items whose base no longer exists (renamed tables) instead of crashing later.
            int maxUid = 0;
            foreach (var e in s.Equipped)
                if (e?.Item != null && e.Spot >= 0 && e.Spot < h.Gear.Spots.Length && ItemData.HasBase(e.Item.BaseId))
                {
                    ItemGen.AdoptForClass(e.Item, h.ClassId); // gear from before class bases becomes this class's
                    ItemGen.AdoptUnique(e.Item); // a unique from before 11-24 gets its id and its own power
                    h.Gear.Spots[e.Spot] = e.Item;
                    maxUid = Math.Max(maxUid, e.Item.Uid);
                }
            HeroStats.Recompute(h);
            h.Stats.Hp = h.Stats.MaxHp * Math.Max(0.05f, Math.Min(1f, s.HpFraction));

            w.Deaths = s.Deaths;
            w.Kills = s.Kills;
            w._minuteKills0 = s.Kills;
            w.Bag.Items.Clear();
            foreach (var it in s.Bag)
                if (it != null && ItemData.HasBase(it.BaseId)) { ItemGen.AdoptForClass(it, h.ClassId); ItemGen.AdoptUnique(it); w.Bag.Items.Add(it); maxUid = Math.Max(maxUid, it.Uid); }
            w.NextItemUid = Math.Max(Math.Max(1, s.NextItemUid), maxUid + 1); // never reuse a uid
            w._dropsTotal = 0;
            w.Bag.AshShards = s.AshShards;
            w.Bag.LanternOil = Math.Max(0, s.LanternOil);
            w.FirstLegendaryGiven = s.FirstLegendaryGiven;
            w.Filter = (FilterPreset)Math.Max(0, Math.Min((int)FilterPreset.Custom, s.Filter)); // every preset survives a load
            w.AutoSalvage = Math.Max(-1, Math.Min(AutoSalvageMax, s.AutoSalvage));
            w.LoadAugments(s);
            w.Bag.Trim(); // a save from the 60-place bag (or one that overflowed) comes back to forty
            if (s.CustomRules != null && (s.CustomRulesSet || s.CustomRules.Count >= 3))
            {
                w.CustomRules.Clear();
                for (int i = 0; i + 2 < s.CustomRules.Count && w.CustomRules.Count < LootFilter.MaxCustomRules; i += 3)
                    w.CustomRules.Add(new LootRule((RuleCond)Math.Max(0, Math.Min((int)RuleCond.ClassFitAtLeast, s.CustomRules[i])), s.CustomRules[i + 1],
                        s.CustomRules[i + 2] == (int)LootAction.Salvage ? LootAction.Salvage : LootAction.Keep));
            }
            w.AutoEquip = s.AutoEquip;
            if (s.AutoEquipDeclined != null) foreach (int uid in s.AutoEquipDeclined) w.AutoEquipDeclined.Add(uid);
            if (!string.IsNullOrEmpty(s.LootRng)) w._lootRng.State = s.LootRng;
            if (!string.IsNullOrEmpty(s.CombatRng)) w._combatRng.State = s.CombatRng;
            w.SeedEfficiency(s.KillsPerMinute, s.XpPerMinute,
                s.DropsPerMinute > 0f ? s.DropsPerMinute : s.KillsPerMinute * DropTable.NormalChance);
            w.ResetMinuteBaseline();
            foreach (int uid in s.RecentDropUids)
            {
                Item found = null;
                foreach (var it in h.Gear.Spots) if (it != null && it.Uid == uid) found = it;
                if (found == null) foreach (var it in w.Bag.Items) if (it.Uid == uid) found = it;
                if (found != null && w.RecentDrops.Count < 3) w.RecentDrops.Add(found);
            }
            h.Preset = Math.Max(0, Math.Min(SkillBook.PresetsFor(h.ClassId).Length - 1, s.Preset));
            h.TalentFocus = Math.Max(-1, Math.Min(2, s.TalentFocus));
            h.UseCustomRing = s.UseCustomRing;
            if (s.SkillSet < SkillSetVersion) { h.UseCustomRing = false; h.Preset = 0; } // the class skill sets were rebuilt
            for (int i = 0; i < 4; i++)
            {
                if (s.CustomSkills != null && i < s.CustomSkills.Count) h.CustomSkills[i] = s.CustomSkills[i];
                if (s.CustomSigils != null && i < s.CustomSigils.Count) h.CustomSigils[i] = (Sigil)s.CustomSigils[i];
            }
            if (s.RuneCounts != null)
                for (int i = 0; i < Math.Min(s.RuneCounts.Count, w.Bag.RuneCounts.Length); i++) w.Bag.RuneCounts[i] = Math.Max(0, s.RuneCounts[i]);
            if (s.RunewordsFound != null) foreach (var id in s.RunewordsFound) h.RunewordsFound.Add(id);
            if (s.KillIds != null && s.KillCounts != null)
                for (int i = 0; i < s.KillIds.Count && i < s.KillCounts.Count; i++) h.KillsById[s.KillIds[i]] = s.KillCounts[i];
            if (s.Achieved != null) foreach (var id in s.Achieved) h.Achieved.Add(id);
            if (s.Letters != null) foreach (var id in s.Letters) h.Letters.Add(id);
            if (s.LettersRead != null) foreach (var id in s.LettersRead) h.LettersRead.Add(id);
            h.GrandCount = s.GrandCount; h.MaxChainDepth = s.MaxChainDepth; h.IgniteCount = s.IgniteCount;
            h.LanternColor = Math.Max(0, Math.Min(LanternColors.Length - 1, s.LanternColor));
            w.Order = (ExpeditionOrder)Math.Max(0, Math.Min(3, s.Order));
            w.FloorLock = s.FloorLock;
            w.CodexFirst = s.CodexFirst;
            w.TopLoop = s.TopLoop;
            w.AutoIgnite = s.AutoIgnite;
            w.Stance = (Stance)Math.Max(0, Math.Min(2, s.Stance));
            w.CampUnlocked = s.CampUnlocked;
            w.OilStoreLevel = Math.Max(0, Math.Min(MaxOilStore, s.OilStoreLevel));
            w.CartographerLevel = Math.Max(0, Math.Min(MaxCartographer, s.CartographerLevel));
            h.Gauge = Math.Max(0f, Math.Min(GaugeMax, s.Gauge));
            if (s.SkillUseIds != null && s.SkillUseCounts != null)
                for (int i = 0; i < Math.Min(s.SkillUseIds.Count, s.SkillUseCounts.Count); i++) h.SkillUses[s.SkillUseIds[i]] = s.SkillUseCounts[i];
            if (s.Evolved != null) foreach (var id in s.Evolved) if (SkillBook.Get(id) != null) h.Evolved.Add(id);
            // 기억의 나무: a save from before the tree keeps its evolutions as legacy and grows the tree by 자동 분배.
            if (s.TreeVersion <= 0) { foreach (var id in h.Evolved) h.LegacyEvolved.Add(id); h.TreeAuto = true; }
            else
            {
                h.TreeAuto = s.TreeAuto;
                if (s.LegacyEvolved != null) foreach (var id in s.LegacyEvolved) if (SkillBook.Get(id) != null) h.LegacyEvolved.Add(id);
                var tree = SkillTree.For(h.ClassId);
                if (s.TreeNodes != null)
                    foreach (var id in s.TreeNodes)
                        if (tree.Get(id) != null && h.TreeNodes.Add(id)) h.TreeOrder.Add(id);
                if (s.SkillSet < SkillSetVersion) h.TreeAuto = true; // rebuilt skill sets: the tree regrows around the new build
            }
            if (s.ResonanceCounts != null)
                for (int i = 0; i < Math.Min(s.ResonanceCounts.Count, h.ResonanceCounts.Length); i++) h.ResonanceCounts[i] = s.ResonanceCounts[i];
            w.RefreshRing();
            w.GoToFloor(Math.Max(0, s.FloorIndex));
            return w;
        }

        public SimWorld(int seed) : this(seed, "ashknight") { }

        /// <summary>A new delve for <paramref name="classId"/> (GDD §7.1: one character per class).</summary>
        public SimWorld(int seed, string classId)
        {
            _seed = seed;
            _rng = new SimRng((ulong)seed);
            _combatRng = new SimRng(unchecked((ulong)seed * 0x9E3779B1UL + 17UL));
            _lootRng = new SimRng(unchecked((ulong)seed * 0x85EBCA77UL + 29UL));
            Hero = new Hero { Id = _nextId++, ClassId = string.IsNullOrEmpty(classId) ? "ashknight" : classId };
            Hero.Ring = SkillBook.BuildRing(Hero.ClassId, 0, 1, null);
            HeroStats.Recompute(Hero);
            Hero.Stats.Hp = Hero.Stats.MaxHp;
            GoToFloor(0);
        }

        // ---------- floors ----------

        public void GoToFloor(int index)
        {
            FloorIndex = index;
            // 빠른 하강 (GDD §11.5): beaten acts are re-climbed with half the rooms.
            Floor = FloorGenerator.Generate(unchecked(_seed * 1000 + index), IsActBossFloor ? 2 : FastDescent ? Math.Max(4, RoomsFor(FloorInZone) / 2) : RoomsFor(FloorInZone));
            if (FloorInZone == 0) Events.Add(new SimEvent { Type = SimEventType.ZoneEntered, Value = Zone, Depth = ActLoop });
            _rng = new SimRng(unchecked((ulong)_seed * 7919UL + (ulong)index));
            _enemies.Clear();
            _spawnLater.Clear();
            ClearTelegraphs();
            ResetSkillState();
            ResetEvents();
            _path.Clear();
            _exploreRoom = null;
            _descendAtTick = -1;
            var (sx, sy) = Floor.Rooms[0].Rect.Center;
            Hero.Pos = Hero.PrevPos = new Vec2(sx + 0.5f, sy + 0.5f);
            foreach (var room in Floor.Rooms) SpawnRoom(room);
        }

        /// <summary>
        /// Dense packs (희승 2026-11-19: "몬스터도 엄청 많이"; GDD §6.1 밀도는 레벨 디자인으로): about three times the old
        /// head count, each one weaker, so marks and death chains have bodies to run through.
        /// </summary>
        public static float PackHp = 2.4f, PackDmg = 0.55f;
        /// <summary>Balance knobs (2026-11-19 floor-pace pass): per-floor HP/damage growth and the zone-boundary wall.</summary>
        public static float HpGrowth = 1.15f, DmgGrowth = 1.07f, ZoneWall = 0.25f;
        /// <summary>
        /// From act III on, life and damage grow a little more gently per floor. 60 h sims (after the auto-equip fix): ×1.15
        /// to the end left ranged heroes in act IV at 60 h, ×1.06 cleared act VI in 11–21 h; ×1.10 lands acts IV–V around
        /// the GDD's 18 h / 30 h (melee ahead, ranged behind).
        /// </summary>
        public static float HpGrowthLate = 1.10f, DmgGrowthLate = 1.05f;
        public static int LateFrom = ActLength * 2;

        /// <summary>Enemy life and damage multipliers on a floor, before the zone wall and the room's own multipliers.</summary>
        public static (float hp, float dmg) GrowthAt(int floor) => (Grow(HpGrowth, HpGrowthLate, floor), Grow(DmgGrowth, DmgGrowthLate, floor));

        static float Grow(float early, float late, int floor) =>
            (float)(Math.Pow(early, Math.Min(floor, LateFrom)) * Math.Pow(late, Math.Max(0, floor - LateFrom)));

        /// <summary>GDD §6.1-1: enemy life grows faster with depth than before (exponential), with a step at each zone
        /// boundary; rewards (area level XP) still grow faster in the easy stretches.</summary>
        (float hp, float dmg) FloorScaling()
        {
            var (hp, dmg) = GrowthAt(FloorIndex);
            float early = EarlyWeight(FloorIndex);
            return (hp * (1f + ZoneWall * Math.Min(Zone, 2)) * (1f + (EarlyHpMul - 1f) * early), dmg * (1f + (EarlyDmgMul - 1f) * early));
        }

        /// <summary>
        /// 희승 11-22: "적들이 너무 쉽게 죽어 초반부터 … 디아블로2 플레이해보면 되게 빡빡하다가 마지막에 만렙 이후부터 학살".
        /// Measured before: in act I a monster died 0.3–2 s after its first hit and the hero never fell below 94% life.
        /// Acts I–II monsters are tougher by these factors, easing to none by act III (where the old curve already bites).
        /// </summary>
        public static float EarlyHpMul = 2.2f, EarlyDmgMul = 3.0f;

        /// <summary>
        /// Half on the very first floor (a Lv1 hunter fell to 9% life there), full from the third, then falling straight to
        /// 0 at act III.
        /// </summary>
        static float EarlyWeight(int floor) => Math.Min(1f, 0.5f + 0.25f * floor) * Math.Max(0f, 1f - floor / (float)LateFrom);

        /// <summary>GDD §5.4: one prefix in zone 1, up to two in zone 2, up to three deeper.</summary>
        void RollAffixes(Enemy e)
        {
            int n = 1 + (Zone >= 1 && _rng.Chance(0.6f) ? 1 : 0) + (Zone >= 2 && _rng.Chance(0.5f) ? 1 : 0);
            for (int i = 0; i < n; i++) e.Affixes |= EliteAffixes.All[_rng.Range(0, EliteAffixes.All.Length)];
            if ((e.Affixes & EliteAffix.Hasted) != 0) { e.Stats.MoveSpeed *= 1.5f; e.Stats.AttackSpeed *= 1.5f; }
            if ((e.Affixes & EliteAffix.Shielded) != 0) e.AffixShield = e.Stats.MaxHp * 0.3f;
        }

        void SpawnRoom(Room room)
        {
            // GDD §6.1-1: HP climbs per floor with a step at each zone boundary (a wall); XP climbs faster (see Kill).
            var (hpMul, dmgMul) = FloorScaling();
            var ranks = ActNow.PatternFor(Zone, FloorInZone);
            switch (room.Kind)
            {
                case RoomKind.Start:
                case RoomKind.Rest:
                    return;
                case RoomKind.Treasure:
                {
                    int n = _rng.Range(6, 10); // a guard on the chest
                    for (int i = 0; i < n; i++)
                        Spawn(ActNow.Ranks[ranks[_rng.Range(0, ranks.Length)]], room, hpMul * PackHp, dmgMul * PackDmg);
                    return;
                }
                case RoomKind.Horde:
                {
                    int n = _rng.Range(26, 35);
                    for (int i = 0; i < n; i++)
                        Spawn(ActNow.Ranks[ranks[_rng.Range(0, ranks.Length)]], room, hpMul * 0.5f, dmgMul * 0.35f);
                    return;
                }
                case RoomKind.Boss:
                {
                    // Act boss floor: 무덤 군주. Every other floor ends on a floor boss (a scaled-up 무덤파기 거구).
                    var boss = IsActBossFloor
                        ? Spawn(ActNow.Boss, room, hpMul * 12f, dmgMul * 1.6f)
                        : Spawn(ActNow.Elite, room, hpMul * 6f, dmgMul * 1.4f);
                    boss.Boss = true;
                    boss.Pos = boss.PrevPos = CenterOf(room);
                    for (int i = 0; i < 8; i++) Spawn(ActNow.Ranks[ranks[0]], room, hpMul * PackHp, dmgMul * PackDmg);
                    return;
                }
                case RoomKind.Event:
                    SetUpEventRoom(room, hpMul, dmgMul);
                    return;
                default:
                {
                    // Hordes grow with the descent and with every rebirth: more to sweep as the hero's effects multiply.
                    int n = Math.Min(40, _rng.Range(12, 19) + ActLoop * 3 + Hero.Rebirths * 2);
                    // 점검 11-22: packs stand in clumps around a few gathering points (Diablo's packs), not spread over
                    // the room cell by cell like a draughts board.
                    int clumps = 1 + n / 6;
                    var centres = new Vec2[clumps];
                    for (int c = 0; c < clumps; c++) centres[c] = Spawn(ActNow.Ranks[ranks[_rng.Range(0, ranks.Length)]], room, hpMul * PackHp, dmgMul * PackDmg).Pos;
                    for (int i = clumps; i < n; i++)
                        SpawnNear(ActNow.Ranks[ranks[_rng.Range(0, ranks.Length)]], room, centres[i % clumps], hpMul * PackHp, dmgMul * PackDmg);
                    // GDD §5.4 / §11.4: sometimes an elite leads the pack (×3 HP, two drops).
                    if (_rng.Chance(0.3f))
                    {
                        var elite = Spawn(ActNow.Elite, room, hpMul * 3f, dmgMul * 1.3f);
                        elite.Elite = true;
                        RollAffixes(elite);
                    }
                    return;
                }
            }
        }

        static Vec2 CenterOf(Room room)
        {
            var (cx, cy) = room.Rect.Center;
            return new Vec2(cx + 0.5f, cy + 0.5f);
        }

        Enemy Spawn(EnemyDef def, Room room, float hpMul, float dmgMul)
        {
            // Organic rooms: the bounding box has wall corners, so draw until the point lands on floor.
            var r = room.Rect;
            var pos = CenterOf(room);
            for (int t = 0; t < 16; t++)
            {
                var p = new Vec2(_rng.Range(r.X + 1, r.X + r.W - 1) + 0.5f, _rng.Range(r.Y + 1, r.Y + r.H - 1) + 0.5f);
                if (Floor.IsWalkable(p)) { pos = p; break; }
            }
            // Anywhere in the cell, not on its centre (a pack on cell centres read as a grid).
            var off = new Vec2(pos.X + (_rng.NextFloat() - 0.5f) * 0.7f, pos.Y + (_rng.NextFloat() - 0.5f) * 0.7f);
            if (Floor.IsWalkable(off)) pos = off;
            var e = SpawnEnemy(def, pos, hpMul, dmgMul);
            e.HomeRoom = room.Id;
            return e;
        }

        /// <summary>One of a clump: within about two cells of <paramref name="centre"/>, on floor (else anywhere in the room).</summary>
        Enemy SpawnNear(EnemyDef def, Room room, Vec2 centre, float hpMul, float dmgMul)
        {
            for (int t = 0; t < 8; t++)
            {
                float a = _rng.NextFloat() * (float)Math.PI * 2f, r = 0.9f + _rng.NextFloat() * 1.9f;
                var p = new Vec2(centre.X + (float)Math.Cos(a) * r, centre.Y + (float)Math.Sin(a) * r);
                if (!Floor.IsWalkable(p) || Floor.RoomAt(p)?.Id != room.Id) continue;
                var e = SpawnEnemy(def, p, hpMul, dmgMul);
                e.HomeRoom = room.Id;
                return e;
            }
            return Spawn(def, room, hpMul, dmgMul);
        }

        /// <summary>Enemies born mid-tick (증식하는 splits) wait here, so no list is changed while it is being walked.</summary>
        readonly List<Enemy> _spawnLater = new List<Enemy>();

        public Enemy SpawnEnemy(EnemyDef def, Vec2 pos, float hpMul = 1f, float dmgMul = 1f) => SpawnEnemy(def, pos, hpMul, dmgMul, false);

        Enemy SpawnEnemy(EnemyDef def, Vec2 pos, float hpMul, float dmgMul, bool later)
        {
            var e = new Enemy
            {
                Id = _nextId++,
                Def = def,
                Pos = pos,
                PrevPos = pos,
                Stats = new Stats
                {
                    MaxHp = def.MaxHp * hpMul, Hp = def.MaxHp * hpMul,
                    Damage = def.Damage * dmgMul, AttackSpeed = def.AttackSpeed, MoveSpeed = def.MoveSpeed,
                },
            };
            (later ? _spawnLater : _enemies).Add(e);
            return e;
        }

        /// <summary>GDD §7.2 해금 페이싱: say what this level opened (skills, ring slots, glyphs, talents, ultimate).</summary>
        void AnnounceUnlocks(int level)
        {
            void Say(string text) => Events.Add(new SimEvent { Type = SimEventType.Unlocked, Pos = Hero.Pos, SkillId = text });
            foreach (var s in SkillBook.For(Hero.ClassId)) if (s.Unlock == level) Say("새 스킬 · " + s.Name);
            if (SkillBook.SlotsAt(level) > SkillBook.SlotsAt(level - 1)) Say("스킬 칸 " + SkillBook.SlotsAt(level) + "개");
            if (level == SkillBook.SigilLevel) Say("보조 문양 개방");
            if (level == SkillBook.UltimateLevel) Say("궁극기 개방");
        }

        public void ClearEnemies() { _enemies.Clear(); _spawnLater.Clear(); ResetSkillState(); }

        /// <summary>
        /// Tests: swap the floor for an open arena (every cell walkable) so walls never get between a blast and its
        /// targets. A boss room that is never visited keeps the floor from ending.
        /// </summary>
        /// <summary>Tests: play on a hand-made floor (no monsters).</summary>
        public void UseFloorForTest(Floor f)
        {
            Floor = f;
            ClearEnemies();
            _exploreRoom = null;
        }

        public void UseOpenFloor(int size = 128)
        {
            var f = new Floor(size, size);
            var all = new GridRect(1, 1, size - 2, size - 2);
            f.Carve(all);
            f.Rooms.Add(new Room { Id = 0, Kind = RoomKind.Start, Rect = all });
            f.Rooms.Add(new Room { Id = 1, Kind = RoomKind.Boss, Rect = new GridRect(size - 2, size - 2, 1, 1) });
            Floor = f;
            ClearEnemies();
        }

        // ---------- tick ----------

        int _eventsAtTickEnd;

        /// <summary>
        /// One Sim step. Events raised between ticks by player actions (rebirth, crafting at the camp, gambling) are
        /// carried into this tick's list so the view still sees them.
        /// </summary>
        public void Tick()
        {
            if (Events.Count > _eventsAtTickEnd)
            {
                var carried = Events.GetRange(_eventsAtTickEnd, Events.Count - _eventsAtTickEnd);
                Events.Clear();
                Events.AddRange(carried);
            }
            else Events.Clear();
            TickBody();
            TickAchievements();
            TickLetters();
            _eventsAtTickEnd = Events.Count;
        }

        void TickBody()
        {
            TickCount++;
            Hero.PrevPos = Hero.Pos;
            if (_spawnLater.Count > 0) { _enemies.AddRange(_spawnLater); _spawnLater.Clear(); }
            foreach (var e in _enemies) e.PrevPos = e.Pos;
            TickIgnite();

            if (CheckHeroDeath()) return;
            if (HeroBrainEnabled && Time >= Hero.StunnedUntil) HeroThink();
            if (EnemyBrainEnabled) foreach (var e in _enemies) if (e.Alive) { EnemyThink(e); BossThink(e); }
            TickTelegraphs();
            // Decide death before anything can heal (regen, absorb, level-up) or advance the floor.
            if (CheckHeroDeath()) return;
            TickSkills();
            ProcessChain();
            TryGrandResonance();
            if (CheckHeroDeath()) return;
            Regen();
            UpdateRooms();
            TickEvents();
            TickAugments();
            TrackEfficiency();
        }

        public const double RetreatWindowSeconds = 900.0;
        int _lastDeathZone = -1;
        double _lastDeathTime = -1e9;

        bool CheckHeroDeath()
        {
            if (Hero.Stats.Hp > 0f) return false;
            // 불사 (defense keystone): once a minute, stand back up at 30%.
            if (Talents.Has(Hero, Talents.Defense, 2) && Time >= Hero.CheatDeathReady)
            {
                Hero.CheatDeathReady = Time + 60.0;
                Hero.Stats.Hp = Hero.Stats.MaxHp * 0.3f;
                Events.Add(new SimEvent { Type = SimEventType.Rested, TargetId = -1, Pos = Hero.Pos });
                return false;
            }
            Deaths++;
            // 물러서기: a second death in the same zone within 15 minutes steps back a zone to grow before trying again,
            // instead of dying at the same wall every few minutes.
            int zone = ZoneStartFloor;
            bool again = zone == _lastDeathZone && Time - _lastDeathTime < RetreatWindowSeconds;
            _lastDeathZone = zone;
            _lastDeathTime = Time;
            int back = again ? Math.Max(ActLoop * ActLength, zone - FloorsPerZone) : zone;
            Events.Add(new SimEvent { Type = SimEventType.HeroDied, Pos = Hero.Pos, TargetId = Hero.Id, Value = back < zone ? 1 : 0 });
            Hero.Stats.Hp = Hero.Stats.MaxHp;
            Hero.Alive = true;
            GoToFloor(back); // checkpoint: first floor of this zone (GDD §6.1), or the one before
            return true;
        }

        void Regen()
        {
            bool inCombat = false;
            foreach (var e in _enemies) if (e.Alive && e.Aggro) { inCombat = true; break; }
            float rate = (inCombat ? 0.01f : 0.05f) + Hero.Derived.RegenPctPerSec + ShrineRegen;
            Hero.Stats.Hp = Math.Min(Hero.Stats.MaxHp, Hero.Stats.Hp + Hero.Stats.MaxHp * rate * Dt);
        }

        void UpdateRooms()
        {
            bool allCleared = true;
            var here = Floor.RoomAt(Hero.Pos);
            if (here != null && !here.Visited)
            {
                here.Visited = true;
                if (here.Kind == RoomKind.Rest)
                {
                    Hero.Stats.Hp = Hero.Stats.MaxHp; // rest room: refill (GDD §6.1 휴식방)
                    Events.Add(new SimEvent { Type = SimEventType.Rested, TargetId = here.Id, Pos = CenterOf(here) });
                }
                if (here.Kind == RoomKind.Event) OfferEvent(here);
            }
            foreach (var room in Floor.Rooms)
            {
                if (!room.Cleared && (room.Visited || !NeedsVisit(room)))
                {
                    bool any = false;
                    foreach (var e in _enemies) if (e.Alive && e.HomeRoom == room.Id) { any = true; break; }
                    if (!any)
                    {
                        room.Cleared = true;
                        Events.Add(new SimEvent { Type = SimEventType.RoomCleared, TargetId = room.Id, Pos = CenterOf(room) });
                        OnRoomCleared(room);
                        if (room.Kind == RoomKind.Treasure)
                        {
                            var chest = ChestPos(Floor, room);
                            Events.Add(new SimEvent { Type = SimEventType.ChestOpened, TargetId = room.Id, Pos = chest });
                            for (int i = 0; i < DropTable.ChestDrops; i++) DropItem(chest, i == 0 ? Rarity.Rare : Rarity.Common);
                        }
                    }
                }
                if (room.Kind == RoomKind.Boss) allCleared = room.Cleared; // the floor ends when its boss falls
            }
            if (allCleared)
            {
                // Linger a moment so the boss's drops land and are seen before the floor changes (review 3-1).
                if (_descendAtTick < 0) _descendAtTick = TickCount + DescendDelayTicks;
                else if (TickCount >= _descendAtTick)
                {
                    Events.Add(new SimEvent { Type = SimEventType.FloorCleared, Value = FloorIndex });
                    bool stay = FloorLock || (CodexFirst && !FloorCodexDone()); // 층 고정 / 도감 먼저: the same floor again
                    // 꼭대기 반복: the last floor of a zone (or the act boss) leads back to that zone's first floor.
                    bool loop = TopLoop && (IsActBossFloor || FloorInZone == FloorsPerZone - 1);
                    GoToFloor(stay ? FloorIndex : loop ? ZoneStartFloor : FloorIndex + 1);
                }
            }
        }

        // ---------- hero ----------

        void HeroThink()
        {
            if (DodgeTelegraph()) return; // a boss's drawn danger first
            if (WhirlThink()) return;     // 휠윈드: keep walking through the pack while the blades spin
            if (Time < _castLock) return; // the shot or swing plays out before the hero moves on
            if (KiteFromBoss()) return;   // a bow or a staff keeps its distance from a boss
            var target = PickTarget();
            if (target != null)
            {
                _lastFightAt = Time;
                float dist = Vec2.Dist(Hero.Pos, target.Pos);
                // Already shooting this one: a little slack before walking again, so a step back by the target
                // does not turn into shoot-walk-shoot (희승: 궁수가 멈칫멈칫한다).
                float slack = target.Id == _engagedId ? 0.5f : 0f;
                float reach = EngageReach();
                // A melee enemy coming in from just outside reach: wait for it (up to 0.8 s, in case it is held up)
                // rather than take one short step and stop again.
                if (dist > reach + slack && dist <= reach + 1.5f && target.Aggro && target.Def.Range <= 0f && Time >= target.StunUntil && _waitTicks < 8)
                {
                    _waitTicks++;
                    return;
                }
                _waitTicks = 0;
                if (dist > reach + slack) { _engagedId = -1; if (!HoldOutsideTelegraph(target.Pos)) MoveHeroToward(target.Pos); }
                else { _engagedId = target.Id; _path.Clear(); TryCast(target, dist - slack); }
                return;
            }
            if (RestBetweenPacks()) return; // 태세: catch breath before the next pack
            var nextRoom = NextRoomToExplore();
            if (nextRoom != null)
            {
                var goal = RoomGoal(nextRoom);
                MoveHeroToward(goal, TravelFactor(nextRoom, goal));
            }
        }

        // ---------- 비전투 이동 (docs/research/1122_movement_camera.md: 전투 사이 걷기 3초 이내) ----------
        /// <summary>Between fights the hero walks this much faster: the walk between packs is the idle game's dead time.</summary>
        public const float TravelMul = 2f;
        /// <summary>It starts this long after the last fight and takes as long again to reach full speed (no jolt).</summary>
        public const float TravelDelay = 0.3f, TravelEase = 0.3f;
        /// <summary>Within this many tiles of the boss room the hero walks in at its own pace.</summary>
        const float TravelBossCalm = 5f;
        double _lastFightAt = double.NegativeInfinity;

        float TravelFactor(Room next, Vec2 goal)
        {
            if (next.Kind == RoomKind.Boss && Vec2.Dist(Hero.Pos, goal) <= TravelBossCalm) return 1f;
            float k = (float)((Time - _lastFightAt - TravelDelay) / TravelEase);
            return 1f + (TravelMul - 1f) * Mathf01(k);
        }

        static float Mathf01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        // ---------- 동선 (점검 11-22, 희승: "빈 방들 굳이굳이 들어가서 동선낭비", "떡하니 길이 보이는데 돌아가는") ----------

        /// <summary>
        /// A room the hero must still walk into: one with monsters left, or a treasure, event or boss room. A fought-out
        /// room (its pack chased the hero out and died) counts as cleared without a visit.
        /// </summary>
        bool NeedsVisit(Room room) =>
            room.Kind == RoomKind.Treasure || room.Kind == RoomKind.Event || room.Kind == RoomKind.Boss || AliveIn(room) > 0;

        int AliveIn(Room room)
        {
            int n = 0;
            foreach (var e in _enemies) if (e.Alive && e.HomeRoom == room.Id) n++;
            return n;
        }

        /// <summary>Where to walk for a room: its nearest living monster (straight at the pack), else its middle.</summary>
        Vec2 RoomGoal(Room room)
        {
            Enemy best = null;
            float bd = float.MaxValue;
            foreach (var e in _enemies)
            {
                if (!e.Alive || e.HomeRoom != room.Id) continue;
                float d = Vec2.DistSq(e.Pos, Hero.Pos);
                if (d < bd) { bd = d; best = e; }
            }
            return best != null ? best.Pos : CenterOf(room);
        }

        /// <summary>Walking steps (8-way, cells) from <paramref name="from"/> to every cell; -1 where it cannot reach.</summary>
        int[] StepsFrom(Vec2 from)
        {
            int w = Floor.Width, h = Floor.Height;
            var d = new int[w * h];
            for (int i = 0; i < d.Length; i++) d[i] = -1;
            int sx = (int)from.X, sy = (int)from.Y;
            if (!Floor.IsWalkable(sx, sy)) return d;
            var q = new Queue<int>();
            d[sy * w + sx] = 0;
            q.Enqueue(sy * w + sx);
            while (q.Count > 0)
            {
                int c = q.Dequeue(), cx = c % w, cy = c / w;
                for (int k = 0; k < 8; k++)
                {
                    int dx = k < 4 ? (k == 0 ? 1 : k == 1 ? -1 : 0) : (k == 4 || k == 5 ? 1 : -1);
                    int dy = k < 4 ? (k == 2 ? 1 : k == 3 ? -1 : 0) : (k == 4 || k == 6 ? 1 : -1);
                    int nx = cx + dx, ny = cy + dy;
                    if (!Floor.IsWalkable(nx, ny) || d[ny * w + nx] >= 0) continue;
                    if (dx != 0 && dy != 0 && (!Floor.IsWalkable(cx + dx, cy) || !Floor.IsWalkable(cx, cy + dy))) continue;
                    d[ny * w + nx] = d[c] + 1;
                    q.Enqueue(ny * w + nx);
                }
            }
            return d;
        }

        int StepsAt(int[] field, Vec2 p)
        {
            int x = (int)p.X, y = (int)p.Y;
            return x < 0 || y < 0 || x >= Floor.Width || y >= Floor.Height ? -1 : field[y * Floor.Width + x];
        }

        Room _exploreRoom;
        long _exploreUntil;
        int[] _bossSteps;
        Floor _bossStepsFloor;
        /// <summary>How strongly the sweep leans away from the boss: rooms far from it first, so the walk ends there.</summary>
        public const float SweepBias = 0.5f;

        /// <summary>
        /// 점검 11-22: 바람사냥꾼 stalled at the Act V boss, shooting on the spot while it walked up and struck. A ranged
        /// hero steps back from a boss that comes within <see cref="KiteNear"/> until it is <see cref="KiteFar"/> away
        /// again (Diablo's Amazon), unless 돌격, cornered, or stepping into a drawn danger.
        /// </summary>
        public const float KiteNear = 1.8f, KiteFar = 3.5f;
        bool _kiting;

        bool KiteFromBoss()
        {
            if (HeroStats.IsMeleeClass(Hero.ClassId) || Stance == Stance.Charge) { _kiting = false; return false; }
            Enemy boss = null;
            float best = float.MaxValue;
            foreach (var e in _enemies)
            {
                if (!e.Alive || !e.Boss || !e.Aggro || Time < e.StunUntil) continue;
                float d = Vec2.Dist(Hero.Pos, e.Pos);
                if (d < best) { best = d; boss = e; }
            }
            if (boss == null || best >= (_kiting ? KiteFar : KiteNear)) { _kiting = false; return false; }
            var away = Hero.Pos - boss.Pos;
            away = away.LengthSq > 1e-4f ? away.Normalized : new Vec2(-Hero.Facing.Y, Hero.Facing.X);
            var step = Slide(Hero.Pos, Vec2.MoveToward(Hero.Pos, Hero.Pos + away * KiteFar, Hero.Stats.MoveSpeed * ShrineMoveMul * Dt));
            if (Vec2.Dist(step, Hero.Pos) < 1e-4f || InTelegraph(step)) { _kiting = false; return false; } // cornered: fight on
            Hero.Facing = (step - Hero.Pos).Normalized;
            Hero.Pos = step;
            _path.Clear();
            _engagedId = -1;
            _kiting = true;
            return true;
        }

        /// <summary>
        /// "전부 소탕" directive (GDD §5.2 default): the nearest uncleared room by room-graph distance,
        /// boss room last. Ties go to the lower room id (deterministic).
        /// </summary>
        Room NextRoomToExplore()
        {
            // Keep the room once chosen until it is done (or 3 s pass): re-picking every tick between two near-equal
            // rooms walked the hero back and forth along a wall.
            if (_exploreRoom != null && !_exploreRoom.Cleared && TickCount < _exploreUntil && Floor.Rooms.Contains(_exploreRoom)
                && NeedsVisit(_exploreRoom))
                return _exploreRoom;

            // Walking steps, not rooms-between: a room two doors away down a short corridor beat one next door around a
            // long wall, and the hero took the long way round with the path in plain sight.
            var steps = StepsFrom(Hero.Pos);
            Room boss = null;
            foreach (var room in Floor.Rooms) if (room.Kind == RoomKind.Boss) { boss = room; break; }
            if (boss != null && _bossStepsFloor != Floor) { _bossSteps = StepsFrom(CenterOf(boss)); _bossStepsFloor = Floor; }

            Room best = null;
            float bestScore = float.MaxValue;
            foreach (var room in Floor.Rooms)
            {
                if (room.Cleared || room.Kind == RoomKind.Boss || !NeedsVisit(room)) continue;
                int walk = StepsAt(steps, RoomGoal(room));
                if (walk < 0) continue; // walled off: never walk at a wall
                int toBoss = boss != null ? Math.Max(0, StepsAt(_bossSteps, CenterOf(room))) : 0;
                float score = RoomScore(room, walk, toBoss);
                if (float.IsPositiveInfinity(score)) continue; // this order skips it
                if (best == null || score < bestScore) { best = room; bestScore = score; }
            }
            _exploreRoom = best ?? boss;
            _exploreUntil = TickCount + 30;
            return _exploreRoom;
        }

        // ---------- GDD §5.2 원정 지령 (M2 Task 4) ----------

        public ExpeditionOrder Order = ExpeditionOrder.ClearAll;
        /// <summary>방치 스위치 "층 고정": after the floor boss falls, the same floor again (farming).</summary>
        public bool FloorLock;
        /// <summary>방치 스위치 "꼭대기 반복" (GDD §5.2): keep farming the zone the hero has reached (ember before a rebirth).</summary>
        public bool TopLoop;
        /// <summary>
        /// 자동 점화: for someone who leaves the hunt running — the lantern kindles itself when an elite or boss, or a pack of
        /// eight, is close. Shorter than a hand-lit one (20 s against 30), so watching still pays.
        /// </summary>
        public bool AutoIgnite;

        /// <summary>
        /// Lower is visited sooner; infinity means this order does not bother with the room. <paramref name="walk"/> is the
        /// hero's walking steps to it; rooms far from the boss (<paramref name="toBoss"/>) go first, so the sweep clears the
        /// side branches on its way and ends at the boss instead of walking back for them.
        /// </summary>
        float RoomScore(Room room, int walk, int toBoss)
        {
            float d = walk - SweepBias * toBoss;
            switch (Order)
            {
                case ExpeditionOrder.BossRush:
                    return float.PositiveInfinity; // only rooms on the way get fought; the next target is the boss room
                case ExpeditionOrder.TreasureHunt:
                    if (room.Kind == RoomKind.Treasure || room.Kind == RoomKind.Event) return walk;
                    return float.PositiveInfinity;
                case ExpeditionOrder.XpFirst:
                    return d - Math.Min(AliveIn(room), 40) * 2f; // packs pull harder than distance
                default:
                    return d;
            }
        }

        Room NearestRoom(Vec2 p)
        {
            Room best = Floor.Rooms[0];
            float bd = float.MaxValue;
            foreach (var r in Floor.Rooms)
            {
                float d = Vec2.DistSq(p, CenterOf(r));
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        /// <summary>The chest stands against the room's back wall, on the back-edge cell nearest the room's middle.</summary>
        public static Vec2 ChestPos(Floor floor, Room room)
        {
            var (cx, cy) = room.Rect.Center;
            (int x, int y) best = (cx, cy);
            int bd = int.MaxValue;
            foreach (var (x, y) in floor.BackEdge(room))
            {
                if (floor.IsWalkable(x, y - 1)) continue; // prefer the up-right (−y) wall, like the old rect top edge
                int d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d < bd) { bd = d; best = (x, y); }
            }
            return new Vec2(best.x + 0.5f, best.y + 0.5f);
        }

        Enemy PickTarget()
        {
            var here = Floor.RoomAt(Hero.Pos);
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var e in _enemies)
            {
                if (!e.Alive) continue;
                if (!e.Aggro && (here == null || e.HomeRoom != here.Id)) continue;
                float d = Vec2.DistSq(Hero.Pos, e.Pos);
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        void MoveHeroToward(Vec2 goal, float speedMul = 1f)
        {
            var goalCell = new Vec2((int)goal.X + 0.5f, (int)goal.Y + 0.5f);
            if (_path.Count == 0 || !_pathGoal.Equals(goalCell) || TickCount - _pathTick >= 5)
            {
                _path = Pathfinder.Find(Floor, Hero.Pos, goalCell);
                _pathGoal = goalCell;
                _pathTick = TickCount;
            }
            if (_path.Count == 0 && ((int)Hero.Pos.X != (int)goal.X || (int)Hero.Pos.Y != (int)goal.Y))
                return; // unreachable: never walk through walls
            // Spend the whole stride: reaching a waypoint carries the leftover distance on to the next one,
            // so the hero never stalls on cell centres (that pulsing read as stutter).
            float remaining = Hero.Stats.MoveSpeed * ShrineMoveMul * speedMul * Dt;
            var start = Hero.Pos;
            var pos = Hero.Pos;
            while (remaining > 1e-5f)
            {
                var next = _path.Count > 0 ? _path[0] : goal; // empty path = same cell: step straight in
                float dist = Vec2.Dist(pos, next);
                if (dist > remaining) { pos = Vec2.MoveToward(pos, next, remaining); break; }
                pos = next;
                remaining -= dist;
                if (_path.Count == 0) break;
                _path.RemoveAt(0);
            }
            var delta = pos - start;
            if (delta.LengthSq > 1e-6f) Hero.Facing = delta.Normalized;
            Hero.Pos = pos;
        }

        // ---------- enemies ----------

        void EnemyThink(Enemy e)
        {
            float dist = Vec2.Dist(e.Pos, Hero.Pos);
            if (!e.HasHome) { e.HasHome = true; e.Home = e.Pos; e.WanderTo = e.Pos; e.WanderAt = Time + 0.5 + Hash01(e.Id, 1) * 2.0; }
            if (!e.Aggro && dist <= AggroRadius && InSight(e.Pos, Hero.Pos)) Alert(e);
            if (Time < e.StunUntil) return;
            if (!e.Aggro) { Wander(e); return; }

            var toHero = Hero.Pos - e.Pos;
            if (toHero.LengthSq > 1e-6f) e.Facing = toHero.Normalized;
            // M2 Task 2: archers stop at range with a clear line and shoot (instant damage, the arrow is the Shot event).
            if (e.Def.Range > 0f && dist <= e.Def.Range && InSight(e.Pos, Hero.Pos))
            {
                if (Time >= e.AttackReadyAt)
                {
                    e.AttackReadyAt = Time + 1.0 / e.Stats.AttackSpeed;
                    float shot = IncomingDamage(e.Stats.Damage);
                    Hero.Stats.Hp -= shot;
                    Events.Add(new SimEvent { Type = SimEventType.Shot, SourceId = e.Id, TargetId = Hero.Id, From = e.Pos, Pos = Hero.Pos, Value = shot });
                    Events.Add(new SimEvent { Type = SimEventType.Hit, SourceId = e.Id, TargetId = Hero.Id, Pos = Hero.Pos, Value = shot });
                    OnHeroHit(e, shot);
                }
                return;
            }
            if (dist > MeleeRange * 0.9f)
            {
                // Far off they run at the hero; close in they settle to their own pace for the swing.
                float step = e.Stats.MoveSpeed * (dist > 2.5f ? ChargeMul : 1f) * Dt;
                e.Pos = ChaseStep(e, step);
                Separate(e);
            }
            else if (Time >= e.AttackReadyAt)
            {
                e.AttackReadyAt = Time + 1.0 / e.Stats.AttackSpeed;
                float taken = IncomingDamage(e.Stats.Damage);
                Hero.Stats.Hp -= taken;
                Events.Add(new SimEvent { Type = SimEventType.Hit, SourceId = e.Id, TargetId = Hero.Id, Pos = Hero.Pos, Value = taken });
                OnHeroHit(e, taken);
                if ((e.Affixes & EliteAffix.Vampiric) != 0) e.Stats.Hp = Math.Min(e.Stats.MaxHp, e.Stats.Hp + taken * 2f);
            }
        }

        /// <summary>Wakes an enemy and the rest of its pack nearby (Diablo-style: one sees you, the room comes).</summary>
        void Alert(Enemy e)
        {
            e.Aggro = true;
            foreach (var o in _enemies)
                if (o.Alive && !o.Aggro && o.HomeRoom == e.HomeRoom && Vec2.DistSq(o.Pos, e.Pos) <= AlertRadius * AlertRadius) o.Aggro = true;
        }

        /// <summary>Deterministic 0..1 from two ints (no RNG state: wandering never shifts combat rolls).</summary>
        static float Hash01(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Unaggroed: stroll to a spot near home, stand a moment, pick another.</summary>
        void Wander(Enemy e)
        {
            if (Vec2.DistSq(e.Pos, e.WanderTo) > 0.01f)
            {
                var to = e.WanderTo - e.Pos;
                e.Facing = to.Normalized;
                var next = Slide(e.Pos, Vec2.MoveToward(e.Pos, e.WanderTo, e.Stats.MoveSpeed * WanderMul * Dt));
                if (next.Equals(e.Pos)) e.WanderTo = e.Pos; // blocked: stop here
                e.Pos = next;
                return;
            }
            if (Time < e.WanderAt) return;
            int k = (int)(Time * 10.0);
            float a = Hash01(e.Id, k) * (float)Math.PI * 2f, r = 0.6f + Hash01(k, e.Id) * 1.6f;
            var p = e.Home + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * r;
            e.WanderTo = Floor.IsWalkable(p) && InSight(e.Pos, p) ? p : e.Home;
            e.WanderAt = Time + 1.5 + Hash01(e.Id, k + 7) * 2.5;
        }

        /// <summary>
        /// One step toward the hero: straight when the way is clear, otherwise down the flow field (distance-to-hero over
        /// the floor's cells), so a boss walks round a wall instead of pressing into it (희승: 보스가 벽에 막혀 맞다가 죽었다).
        /// </summary>
        Vec2 ChaseStep(Enemy e, float step)
        {
            if (Pathfinder.ClearLine(Floor, e.Pos, Hero.Pos)) return Slide(e.Pos, Vec2.MoveToward(e.Pos, Hero.Pos, step));
            BuildFlow();
            int cx = (int)e.Pos.X, cy = (int)e.Pos.Y;
            int best = FlowAt(cx, cy), bx = cx, by = cy;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (dx != 0 && dy != 0 && (!Floor.IsWalkable(cx + dx, cy) || !Floor.IsWalkable(cx, cy + dy))) continue;
                    int v = FlowAt(cx + dx, cy + dy);
                    if (v < best) { best = v; bx = cx + dx; by = cy + dy; }
                }
            if (best == int.MaxValue) return Slide(e.Pos, Vec2.MoveToward(e.Pos, Hero.Pos, step)); // cut off: old behaviour
            var goal = bx == cx && by == cy ? Hero.Pos : new Vec2(bx + 0.5f, by + 0.5f);
            return Slide(e.Pos, Vec2.MoveToward(e.Pos, goal, step));
        }

        int FlowAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Floor.Width || y >= Floor.Height || !Floor.IsWalkable(x, y)) return int.MaxValue;
            return _flow[y * Floor.Width + x];
        }

        /// <summary>BFS from the hero's cell, rebuilt only when the hero changes cell or the floor changes.</summary>
        void BuildFlow()
        {
            int w = Floor.Width, h = Floor.Height;
            int cell = (int)Hero.Pos.Y * w + (int)Hero.Pos.X;
            if (_flowFloor == Floor && _flowCell == cell && _flow != null) return;
            _flowFloor = Floor;
            _flowCell = cell;
            if (_flow == null || _flow.Length != w * h) _flow = new int[w * h];
            for (int i = 0; i < _flow.Length; i++) _flow[i] = int.MaxValue;
            if (cell < 0 || cell >= _flow.Length) return;
            var q = new Queue<int>();
            _flow[cell] = 0;
            q.Enqueue(cell);
            while (q.Count > 0)
            {
                int c = q.Dequeue(), x = c % w, y = c / w, d = _flow[c] + 1;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (!Floor.IsWalkable(nx, ny)) continue;
                        if (dx != 0 && dy != 0 && (!Floor.IsWalkable(x + dx, y) || !Floor.IsWalkable(x, y + dy))) continue;
                        int n = ny * w + nx;
                        if (_flow[n] <= d) continue;
                        _flow[n] = d;
                        q.Enqueue(n);
                    }
            }
        }

        Vec2 Slide(Vec2 from, Vec2 to)
        {
            if (Floor.IsWalkable(to)) return to;
            var xOnly = new Vec2(to.X, from.Y);
            if (Floor.IsWalkable(xOnly)) return xOnly;
            var yOnly = new Vec2(from.X, to.Y);
            if (Floor.IsWalkable(yOnly)) return yOnly;
            return from;
        }

        void Separate(Enemy e)
        {
            const float minDist = 0.6f;
            foreach (var o in _enemies)
            {
                if (o == e || !o.Alive) continue;
                var d = e.Pos - o.Pos;
                float len = d.Length;
                if (len >= minDist) continue;
                var push = len < 1e-4f ? new Vec2(0.05f * ((e.Id & 1) == 0 ? 1 : -1), 0.05f) : d * ((minDist - len) * 0.5f / len);
                e.Pos = Slide(e.Pos, e.Pos + push);
            }
        }

        /// <summary>Tests: award XP as a kill would (runs the level, ember and wick rules).</summary>
        public void GrantXpForTest(int xp) => GainXp(xp);

        void GainXp(int xp)
        {
            // First minutes: the ring opens a slot at Lv2 and Lv4, so those levels come twice / one and a half times as fast.
            float early = Hero.Level <= 2 ? 2f : Hero.Level <= 4 ? 1.5f : 1f;
            int gained = (int)Math.Round(xp * (1f + Hero.Derived.XpPct) * early);
            Hero.Xp += gained;
            TotalXp += gained;
            while (Hero.Xp >= Hero.XpToNext)
            {
                if (Hero.Level >= LevelCap)
                {
                    // GDD §11.5: past Lv60 XP only piles up as grey ember (it turns on at rebirth) — or, at 통합 100, feeds the wick.
                    PastCap();
                    break;
                }
                Hero.Xp -= Hero.XpToNext;
                Hero.Level++;
                HeroStats.Recompute(Hero);
                Hero.Stats.Hp = Hero.Stats.MaxHp;
                RefreshRing();
                if (Hero.Rebirths == 0) AnnounceUnlocks(Hero.Level); // a reborn hero already has them all
                Events.Add(new SimEvent { Type = SimEventType.LevelUp, TargetId = Hero.Id, Pos = Hero.Pos, Value = Hero.Level });
                ReequipFromBag();
            }
        }

        /// <summary>
        /// After a level-up, items in the bag that were too high to wear may now be upgrades (review 1-5).
        /// Best first, deterministic order (bag order). Replaced gear goes back into the bag.
        /// </summary>
        void ReequipFromBag()
        {
            if (!AutoEquip) return;
            for (int i = 0; i < Bag.Items.Count; i++)
            {
                var it = Bag.Items[i];
                if (it.ReqLevel > Hero.IntegratedLevel) continue;
                var old = TryAutoEquip(it, out bool equipped);
                if (!equipped) continue;
                Bag.Items.RemoveAt(i);
                if (old != null) Bag.Items.Insert(i, old);
                else i--;
            }
        }

        /// <summary>Monster level for item level: M1 act I zone 1 = 1 + floor (GDD §8.2).</summary>
        public int AreaLevel => 1 + FloorInAct * 3 / 4 + ActLoop * 10; // act I spans Lv 1–10 (GDD §6.3)

        /// <summary>
        /// Rolls one item at <paramref name="pos"/> (at least <paramref name="minRarity"/>), auto-equips it if better,
        /// otherwise runs it through the loot filter into the bag or salvage. Lv3 guarantees the first legendary (GDD §8.1).
        /// </summary>
        public Item DropItem(Vec2 pos, Rarity minRarity) => DropItem(pos, minRarity, null, 0f);

        Item DropItem(Vec2 pos, Rarity minRarity, Slot? slot, float extraMagicFind)
        {
            var rarity = ItemGen.RollRarity(_lootRng, Hero.IntegratedLevel, Hero.Derived.MagicFindPct + extraMagicFind);
            if (rarity < minRarity) rarity = minRarity;
            if (!FirstLegendaryGiven && Hero.Level >= DropTable.LegendaryGuaranteeLevel && rarity < Rarity.Legendary) rarity = Rarity.Legendary;
            if (rarity >= Rarity.Legendary) FirstLegendaryGiven = true;

            var item = ItemGen.Roll(_lootRng, NextItemUid++, AreaLevel, rarity, slot, ItemGen.GreaterChance(AreaLevel), Hero.ClassId);
            if (slot == null) _dropsTotal++; // a gamble is bought, not found: keep it out of the offline drop rate
            Events.Add(new SimEvent { Type = SimEventType.Drop, Pos = pos, Item = item, Value = (int)item.Rarity });

            var keep = item;
            if (AutoEquip)
            {
                keep = TryAutoEquip(item, out bool equipped);
                if (equipped) Remember(item);
            }
            if (keep == null) return item;
            // 자동 분해: whatever falls at or below the chosen rarity (a new drop, or the piece it replaced) goes to ash.
            if (IsJunk(keep)) Bag.AshShards += LootFilter.SalvageValue(keep);
            else
            {
                var salvaged = Bag.Add(keep);
                if (keep == item && salvaged != item) Remember(item);
            }
            // A6: the piece the auto-equip took off may be ash already (자동 분해, or the full bag took it).
            if (keep != item && LastAutoSwap != null && LastAutoSwap.Old == keep && !Bag.Items.Contains(keep))
                LastAutoSwap.AshPaid = LootFilter.SalvageValue(keep);
            return item;
        }

        void Remember(Item item)
        {
            RecentDrops.Insert(0, item);
            if (RecentDrops.Count > 3) RecentDrops.RemoveAt(3);
        }

        /// <summary>
        /// Equips if the item is wearable and a ▲ (UX plan §3.0 다: 전투력 +1% or more, and not a ◆ trade — those stay in
        /// the bag for the player to weigh, so auto-equip and the bag's arrows never disagree). Returns the replaced item
        /// (or the item itself if not equipped). The event's Value is the 전투력 change.
        /// </summary>
        public Item TryAutoEquip(Item item, out bool equipped)
        {
            equipped = false;
            if (item.ReqLevel > Hero.IntegratedLevel) return item; // GDD §8.2: compares with the 통합 레벨
            if (AutoEquipDeclined.Contains(item.Uid)) return item; // the player undid this one (A6)
            GearCompare best = default;
            bool found = false;
            foreach (var spot in Equipment.SpotsFor(item.Slot))
            {
                if (Hero.Gear.Spots[(int)spot] is Item worn && worn.Locked) continue; // a locked piece stays on (잠금)
                var c = HeroStats.Compare(Hero, item, spot);
                if (c.AutoEquips && (!found || c.Power > best.Power)) { best = c; found = true; }
            }
            if (!found) return item;
            var old = Hero.Gear.Equip(item, best.Spot);
            HeroStats.Recompute(Hero);
            SocketRunes();
            equipped = true;
            LastAutoSwap = new AutoSwap { New = item, Old = old, Spot = best.Spot, Power = best.Power };
            Events.Add(new SimEvent { Type = SimEventType.Equip, TargetId = item.Uid, Pos = Hero.Pos, Value = best.Power - 1f, Item = item });
            return old;
        }

        // ---------- determinism ----------

        static List<int> FlattenRules(List<LootRule> rules)
        {
            var list = new List<int>();
            foreach (var r in rules) { list.Add((int)r.Cond); list.Add(r.Value); list.Add((int)r.Action); }
            return list;
        }

        public ulong StateHash()
        {
            ulong h = 1469598103934665603UL;
            void Mix(float f) { h ^= (uint)BitConverter.SingleToInt32Bits(f); h *= 1099511628211UL; }
            void MixD(double d) { h ^= (ulong)BitConverter.DoubleToInt64Bits(d); h *= 1099511628211UL; }
            void MixI(int i) { h ^= (uint)i; h *= 1099511628211UL; }
            MixI(TickCount); MixI(FloorIndex); MixI(Deaths); MixI(_pending.Count); MixI(_nextCascade);
            Mix(Hero.Pos.X); Mix(Hero.Pos.Y); Mix(Hero.Stats.Hp); Mix(Hero.Stats.MaxHp); MixI(Hero.Xp); MixI(Hero.Level);
            MixD(Hero.AttackReadyAt); Mix(Hero.Gauge); Mix(Hero.Heat); MixI(_blasts.Count); MixI(_copies.Count); MixI(_grounds.Count);
            MixI(Bag.Items.Count); MixI(Bag.AshShards); MixI(NextItemUid); MixI(Kills);
            foreach (var it in Hero.Gear.Spots) MixI(it?.Uid ?? 0);
            for (int i = 0; i < Hero.Ring.Slots.Length; i++) MixD(Hero.Ring.CooldownLeft(i, Time));
            foreach (var e in _enemies)
            {
                Mix(e.Pos.X); Mix(e.Pos.Y); Mix(e.Stats.Hp); MixI(e.Alive ? 1 : 0); MixI((int)e.Mark);
                MixI(e.Aggro ? 1 : 0); MixI(e.Stacks); MixD(e.StunUntil); MixD(e.AttackReadyAt);
            }
            return h;
        }
    }
}
