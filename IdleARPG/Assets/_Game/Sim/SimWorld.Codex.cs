namespace Lanternkeeper.Sim
{
    /// <summary>
    /// GDD §12.5 적 도감 메달: kills of each enemy kind earn bronze, silver and gold (100 / 1,000 / 10,000). Every medal
    /// is +1% damage against that kind, for good. The codex shows the count, the medal and who it once was (§26.6).
    /// </summary>
    public sealed partial class SimWorld
    {
        public static readonly int[] MedalKills = { 100, 1000, 10000 };

        public static int MedalOf(int kills) => kills >= MedalKills[2] ? 3 : kills >= MedalKills[1] ? 2 : kills >= MedalKills[0] ? 1 : 0;

        public static string MedalName(int tier) => tier == 3 ? "금" : tier == 2 ? "은" : tier == 1 ? "동" : "—";

        public int KillsOf(string id) => Hero.KillsById.TryGetValue(id, out int n) ? n : 0;

        float MedalDamageMul(Enemy e) => 1f + 0.01f * MedalOf(KillsOf(e.Def.Id));

        /// <summary>
        /// 방치 스위치 "도감 먼저" (GDD §5.2, 지도 제작자 Lv3): the floor repeats until every kind it fields has bronze.
        /// </summary>
        public bool CodexFirst;

        /// <summary>True when every enemy kind this floor fields has at least a bronze medal.</summary>
        public bool FloorCodexDone()
        {
            foreach (int r in ActNow.PatternFor(Zone, FloorInZone))
                if (MedalOf(KillsOf(ActNow.Ranks[r].Id)) == 0) return false;
            return true;
        }

        void CountKill(Enemy e)
        {
            string id = e.Def.Id;
            int before = KillsOf(id);
            Hero.KillsById[id] = before + 1;
            int tier = MedalOf(before + 1);
            if (tier > MedalOf(before))
                Events.Add(new SimEvent { Type = SimEventType.Medal, Pos = e.Pos, SkillId = e.Def.Name, Value = tier });
        }
    }
}
