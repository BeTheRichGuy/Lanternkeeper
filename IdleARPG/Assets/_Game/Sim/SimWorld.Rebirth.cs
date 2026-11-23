using System;

namespace Lanternkeeper.Sim
{
    /// <summary>
    /// GDD §11.5 환생과 잔불. Lv60 is the cap of a life: XP after it only piles up, and what it would buy shows as grey
    /// ember levels (no effect yet). Rebirth (at Lv60 only) turns them on: level back to 1, ember levels kept and stacking,
    /// 통합 레벨 = level + ember (≤ 100) drives stats, skills and talents. No item is ever lost; worn gear above the
    /// integrated level just stops working until it is reached. Acts already beaten to their boss are re-climbed fast:
    /// half the rooms, and each kill counts 2–5 times for XP and drops (deeper acts more).
    /// </summary>
    public sealed partial class SimWorld
    {
        public const int LevelCap = 60, EmberCap = 40, IntegratedCap = 100;

        /// <summary>XP an ember level costs: the same as the level it stands for (Lv 60 + k).</summary>
        public static long EmberCost(int emberLevel) => XpCurve.ToNext(LevelCap - 1 + emberLevel);

        /// <summary>Grey ember levels the piled-up XP would turn on at the next rebirth.</summary>
        public int PendingEmber
        {
            get
            {
                long xp = Hero.OverflowXp + (Hero.Level >= LevelCap ? Hero.Xp : 0);
                int n = 0;
                while (Hero.EmberLevel + n < EmberCap && xp >= EmberCost(Hero.EmberLevel + n + 1))
                {
                    xp -= EmberCost(Hero.EmberLevel + n + 1);
                    n++;
                }
                return n;
            }
        }

        public bool CanRebirth => Hero.Level >= LevelCap;

        /// <summary>XP a wick level costs: the Lv100 step, 10% more each level (endless, slow).</summary>
        public static long WickCost(int wickLevel) => (long)(XpCurve.ToNext(IntegratedCap - 1) * (1.0 + 0.1 * wickLevel));

        /// <summary>
        /// XP past the Lv60 cap: grey ember while ember can still grow; at 통합 100 (Lv60 + ember 40) it burns into the
        /// 심지 instead — the endless axis: every wick level +1% damage, +0.5% area, +0.2% attack speed, +0.1% move speed.
        /// </summary>
        void PastCap()
        {
            if (Hero.EmberLevel < EmberCap) { Hero.OverflowXp += Hero.Xp; Hero.Xp = 0; return; }
            Hero.WickXp += Hero.Xp;
            Hero.Xp = 0;
            bool up = false;
            while (Hero.WickXp >= WickCost(Hero.WickLevel))
            {
                Hero.WickXp -= WickCost(Hero.WickLevel);
                Hero.WickLevel++;
                up = true;
                Events.Add(new SimEvent { Type = SimEventType.WickLevelUp, TargetId = Hero.Id, Pos = Hero.Pos, Value = Hero.WickLevel });
            }
            if (up) HeroStats.Recompute(Hero);
        }

        /// <summary>환생: Lv60 only. Grey ember turns on, level 1, back to the top of the descent (fast where beaten).</summary>
        public bool Rebirth()
        {
            if (!CanRebirth) return false;
            Hero.OverflowXp += Hero.Xp; // the part-level at the cap is ember too
            Hero.Xp = 0;
            int gain = PendingEmber;
            for (int i = 1; i <= gain; i++) Hero.OverflowXp -= EmberCost(Hero.EmberLevel + i);
            Hero.EmberLevel += gain;
            Hero.Rebirths++;
            Hero.Level = 1;
            Hero.Xp = 0;
            ClearAugments(); // a new life deals new cards
            RefreshRing();
            HeroStats.Recompute(Hero);
            Hero.Stats.Hp = Hero.Stats.MaxHp;
            ReequipFromBag();
            GoToFloor(0);
            Events.Add(new SimEvent { Type = SimEventType.Rebirth, Pos = Hero.Pos, TargetId = Hero.Id, Value = Hero.EmberLevel, Depth = Hero.Rebirths });
            return true;
        }

        /// <summary>빠른 하강: true on a floor of an act already beaten to its boss, after at least one rebirth.</summary>
        public bool FastDescent => Hero.Rebirths > 0 && ActLoop <= Hero.DeepestActCleared;

        /// <summary>재등반 배속: each kill counts this many times for XP and drops (×2, deeper beaten acts up to ×5).</summary>
        public int RepeatMul => FastDescent ? Math.Min(5, 2 + Hero.DeepestActCleared - ActLoop) : 1;

        void NoteActCleared(Enemy e)
        {
            if (e.Boss && IsActBossFloor) Hero.DeepestActCleared = Math.Max(Hero.DeepestActCleared, ActLoop);
        }
    }
}
