namespace Lanternkeeper.Sim
{
    public sealed partial class SimWorld
    {
        /// <summary>What opens at which integrated level (the HUD's next-goal line and the unlock notices).</summary>
        static readonly (int level, string what)[] Unlocks =
        {
            (2, "스킬 2칸"), (4, "스킬 3칸"), (8, "스킬 4칸"), (SkillBook.SigilLevel, "보조 문양"),
            (20, "증강 카드 2칸"), (SkillBook.UltimateLevel, "궁극기"), (40, "증강 카드 3칸"),
        };

        /// <summary>
        /// The one line under the level ETA (UX plan D1; Nomad Idle's reviews: "13시간 했는데 목적을 모르겠다"): the
        /// most useful next thing - rebirth when it is there, the act boss while the camp is shut, an unlock a few levels
        /// off, else the act boss and how many floors away it waits. Plain words, no hidden state.
        /// </summary>
        public string NextGoal()
        {
            var h = Hero;
            if (CanRebirth) return "다음: 야영지(Y) 모닥불에서 환생 → 잔불 레벨";
            int floorsLeft = ActBossFloorInAct - FloorInAct;
            string boss = ActNow.Boss != null ? ActNow.Boss.Name : "막 보스";
            string where = floorsLeft > 0 ? floorsLeft + "층 남음" : "이 층";
            if (!CampUnlocked) return "다음: " + boss + " 처치 → 야영지 개방 (" + where + ")";
            int lv = h.IntegratedLevel;
            foreach (var (at, what) in Unlocks)
            {
                if (lv >= at) continue;
                if (at - lv <= 3) return "다음: Lv " + at + " → " + what + " 개방 (" + (at - lv) + "레벨 남음)";
                break;
            }
            return "다음: " + boss + " 처치 (" + where + ")";
        }
    }
}
