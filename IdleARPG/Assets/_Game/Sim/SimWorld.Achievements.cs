using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>A goal on the codex's third page; some light the lantern in a new colour (GDD §12.5 cosmetics).</summary>
    public sealed class AchievementDef
    {
        public string Id, Name, Text;
        public Func<SimWorld, bool> Done;
        /// <summary>Lantern colour this unlocks (index into <see cref="SimWorld.LanternColors"/>), or 0 for none.</summary>
        public int Lantern;
    }

    /// <summary>GDD §12.5 도전 과제 (a first set of 19) and the lantern colours some of them unlock (X cycles them).</summary>
    public sealed partial class SimWorld
    {
        public static readonly (string name, float r, float g, float b)[] LanternColors =
        {
            ("잿불", 1f, 0.80f, 0.56f),
            ("공허 보라", 0.78f, 0.62f, 1f),
            ("성스러운 금", 1f, 0.9f, 0.55f),
            ("물빛 청록", 0.55f, 0.92f, 0.95f),
            ("핏빛", 1f, 0.5f, 0.45f),
            ("별빛", 0.92f, 0.94f, 1f),
            ("포자 연두", 0.65f, 1f, 0.6f),
            ("쇳물 주황", 1f, 0.62f, 0.3f),
            ("심연 남빛", 0.62f, 0.66f, 1f),
        };

        static int Evolved(SimWorld w) => w.Hero.Evolved.Count;

        static int Medals(SimWorld w, int tier)
        {
            int n = 0;
            foreach (var kv in w.Hero.KillsById) if (MedalOf(kv.Value) >= tier) n++;
            return n;
        }

        static bool AnyMastery3(SimWorld w)
        {
            foreach (int c in w.Hero.ResonanceCounts) if (Elements.MasteryTier(c) >= 3) return true;
            return false;
        }

        public static readonly AchievementDef[] Achievements =
        {
            new AchievementDef { Id = "kills_100", Name = "첫 걸음", Text = "적 100마리 처치", Done = w => w.Kills >= 100 },
            new AchievementDef { Id = "kills_10k", Name = "학살자", Text = "적 10,000마리 처치", Done = w => w.Kills >= 10000, Lantern = 4 },
            new AchievementDef { Id = "chain_5", Name = "연쇄 반응", Text = "사망 전파 5연쇄", Done = w => w.Hero.MaxChainDepth >= 5, Lantern = 1 },
            new AchievementDef { Id = "grand_1", Name = "대공명", Text = "첫 대공명 달성", Done = w => w.Hero.GrandCount >= 1 },
            new AchievementDef { Id = "grand_100", Name = "공명의 대가", Text = "대공명 100회 달성", Done = w => w.Hero.GrandCount >= 100 },
            new AchievementDef { Id = "act1", Name = "모래 왕의 안식", Text = "석관 군주 처치", Done = w => w.Hero.DeepestActCleared >= 0, Lantern = 2 },
            new AchievementDef { Id = "act2", Name = "신목의 침묵", Text = "뿌리 왕관 거목 처치", Done = w => w.Hero.DeepestActCleared >= 1, Lantern = 3 },
            new AchievementDef { Id = "act3", Name = "멎은 심장", Text = "맥박 군주 처치", Done = w => w.Hero.DeepestActCleared >= 2, Lantern = 5 },
            new AchievementDef { Id = "act4", Name = "열린 문", Text = "얼음 속 거인 처치", Done = w => w.Hero.DeepestActCleared >= 3, Lantern = 6 },
            new AchievementDef { Id = "act5", Name = "식은 별", Text = "쇳물 거인 처치", Done = w => w.Hero.DeepestActCleared >= 4, Lantern = 7 },
            new AchievementDef { Id = "act6", Name = "꺼진 하늘", Text = "꺼진 별의 사도 처치", Done = w => w.Hero.DeepestActCleared >= 5, Lantern = 8 },
            new AchievementDef { Id = "legendary", Name = "전설의 시작", Text = "첫 전설 장비 획득", Done = w => w.FirstLegendaryGiven },
            new AchievementDef { Id = "runeword_1", Name = "룬의 말", Text = "룬어 완성", Done = w => w.Hero.RunewordsFound.Count >= 1 },
            new AchievementDef { Id = "runeword_5", Name = "룬 학자", Text = "룬어 5종 완성", Done = w => w.Hero.RunewordsFound.Count >= 5 },
            new AchievementDef { Id = "evolve_1", Name = "첫 진화", Text = "첫 스킬 진화", Done = w => Evolved(w) >= 1 },
            new AchievementDef { Id = "evolve_5", Name = "진화의 고리", Text = "스킬 진화 5종 달성", Done = w => Evolved(w) >= 5 },
            new AchievementDef { Id = "bronze_10", Name = "도감 수집가", Text = "도감 동메달 10종 달성", Done = w => Medals(w, 1) >= 10 },
            new AchievementDef { Id = "gold_1", Name = "금빛 사냥꾼", Text = "도감 금메달 1종 달성", Done = w => Medals(w, 3) >= 1 },
            new AchievementDef { Id = "mastery_3", Name = "공명 숙련 III", Text = "공명 하나를 숙련 III까지", Done = AnyMastery3 },
            new AchievementDef { Id = "lv30", Name = "궁극", Text = "Lv 30", Done = w => w.Hero.IntegratedLevel >= 30 },
            new AchievementDef { Id = "ignite_10", Name = "등불지기", Text = "등불 점화 10회", Done = w => w.Hero.IgniteCount >= 10 },
            new AchievementDef { Id = "rebirth_1", Name = "잊고, 다시", Text = "첫 환생", Done = w => w.Hero.Rebirths >= 1, Lantern = 5 },
        };

        public bool LanternUnlocked(int color)
        {
            if (color == 0) return true;
            foreach (var a in Achievements) if (a.Lantern == color && Hero.Achieved.Contains(a.Id)) return true;
            return false;
        }

        /// <summary>X: the next unlocked lantern colour.</summary>
        public void CycleLantern()
        {
            for (int i = 1; i <= LanternColors.Length; i++)
            {
                int c = (Hero.LanternColor + i) % LanternColors.Length;
                if (LanternUnlocked(c)) { Hero.LanternColor = c; return; }
            }
        }

        /// <summary>After each tick: count what the tick did, then award goals newly met.</summary>
        void TickAchievements()
        {
            foreach (var ev in Events)
            {
                if (ev.Type == SimEventType.GrandResonance) Hero.GrandCount++;
                else if (ev.Type == SimEventType.ChainPop && ev.Depth > Hero.MaxChainDepth) Hero.MaxChainDepth = ev.Depth;
                else if (ev.Type == SimEventType.Ignited) Hero.IgniteCount++;
            }
            if (TickCount % 10 != 0) return; // goals are checked once a second
            foreach (var a in Achievements)
            {
                if (Hero.Achieved.Contains(a.Id) || !a.Done(this)) continue;
                Hero.Achieved.Add(a.Id);
                Events.Add(new SimEvent { Type = SimEventType.Achievement, Pos = Hero.Pos, SkillId = a.Name, Value = a.Lantern });
            }
        }
    }
}
