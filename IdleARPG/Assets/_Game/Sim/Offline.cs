using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>What happened while the game was closed (GDD §13), shown in the return summary.</summary>
    public sealed class OfflineReport
    {
        public double Seconds, CappedSeconds;
        public int Kills, LevelBefore, LevelAfter, AshGained, ItemsKept;
        public long XpGained;
        /// <summary>Skill levels (숙련) gained while away, summed over the ring.</summary>
        public int SkillLevelsGained;
        /// <summary>심지 levels gained while away.</summary>
        public int WickLevelsGained;
        public readonly List<Item> Legendaries = new List<Item>();
        public readonly List<Item> Equipped = new List<Item>();
        public bool Empty => Kills == 0 && XpGained == 0;
    }

    public static class OfflineRules
    {
        public const double CapSeconds = 8 * 3600;   // GDD §13: base 8 h; SimWorld.OfflineCapSeconds adds the camp oil store
        public const double MinSeconds = 60;
        public const float Efficiency = 0.8f;        // GDD §13: 80% of the recent rate
    }
}
