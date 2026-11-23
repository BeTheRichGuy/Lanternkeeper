using System;

namespace Lanternkeeper.Sim
{
    public enum TalentStat { DamagePct, CritChance, AttackSpeed, ElementPct, MaxHpPct, DamageReduction, Regen, ResonancePct, ChainRadius, GaugeRate }

    /// <summary>
    /// GDD §7.2 탤런트: one point per level from Lv5, three branches per class (공격 / 방어 / 공명·전파), each 40 points
    /// deep with a keystone at 10, 25 and 40 that changes a rule. Idle-first: points flow automatically along the
    /// focus branch (the ring preset's, or the player's pick), so the build grows without menus; respec is free.
    /// </summary>
    public static class Talents
    {
        public const int StartLevel = 5, BranchCap = 40;
        public static readonly int[] Keystones = { 10, 25, 40 };
        public const int Offense = 0, Defense = 1, Synergy = 2;

        /// <summary>What one point in each branch buys, repeating along the branch.</summary>
        static readonly TalentStat[][] Pattern =
        {
            new[] { TalentStat.DamagePct, TalentStat.ElementPct, TalentStat.CritChance, TalentStat.DamagePct, TalentStat.AttackSpeed },
            new[] { TalentStat.MaxHpPct, TalentStat.DamageReduction, TalentStat.MaxHpPct, TalentStat.Regen },
            new[] { TalentStat.ResonancePct, TalentStat.ChainRadius, TalentStat.ResonancePct, TalentStat.GaugeRate },
        };

        public static int PointsAt(int level) => Math.Max(0, level - StartLevel + 1);

        public static string BranchName(string classId, int b)
        {
            switch (classId)
            {
                case "oathknight": return b == 0 ? "심판" : b == 1 ? "수호" : "순교";
                case "windhunter": return b == 0 ? "폭풍" : b == 1 ? "생존" : "역병";
                case "starseer": return b == 0 ? "별불" : b == 1 ? "별의 장막" : "공허";
                default: return b == 0 ? "잿불" : b == 1 ? "불굴" : "잿더미";
            }
        }

        public static string KeystoneName(int b, int k) => KeystoneNames[b, k];
        public static string KeystoneText(int b, int k) => KeystoneTexts[b, k];

        static readonly string[,] KeystoneNames =
        {
            { "불씨 심장", "학살자", "종말의 불꽃" },
            { "단단한 껍질", "피의 맹세", "불사" },
            { "공명의 물결", "긴 사슬", "이중 대공명" },
        };

        static readonly string[,] KeystoneTexts =
        {
            { "치명타 피해 +30%", "생명력이 50% 이상인 적에게 주는 피해 +25%", "자원 최대치 강화 위력 ×1.5 → ×2" },
            { "받는 피해 −8%", "처치 시 생명력 1% 회복", "치명적인 피해를 입을 때 60초에 한 번 생명력 30%로 생존" },
            { "공명 게이지 충전 속도 +25%", "사망 전파 횟수 +1", "대공명이 다음 원소로 한 번 더 발동 (위력 60%)" },
        };

        /// <summary>A keystone is a node of 기억의 나무 now (11-21); the branch stats come from its passive nodes
        /// (<see cref="SkillTree.Apply"/> fills <see cref="Hero.TalentPoints"/>).</summary>
        public static bool Has(Hero h, int branch, int keystone) => SkillTree.HasKey(h, branch, keystone);

        /// <summary>Adds the allocated points on top of level + gear (called from HeroStats.Recompute).</summary>
        public static void Apply(Hero h)
        {
            var d = h.Derived;
            var s = h.Stats;
            float hpPct = 0f;
            h.GaugeRate = 1f;
            for (int b = 0; b < 3; b++)
                for (int i = 0; i < h.TalentPoints[b]; i++)
                {
                    switch (Pattern[b][i % Pattern[b].Length])
                    {
                        case TalentStat.DamagePct: d.DamagePct += 0.02f; break;
                        case TalentStat.ElementPct: d.EmberPct += 0.03f; d.FrostPct += 0.03f; d.ShockPct += 0.03f; break;
                        case TalentStat.CritChance: d.CritChance = Math.Min(0.75f, d.CritChance + 0.01f); break;
                        case TalentStat.AttackSpeed: s.AttackSpeed *= 1.02f; break;
                        case TalentStat.MaxHpPct: hpPct += 0.03f; break;
                        case TalentStat.DamageReduction: d.DamageReduction = Math.Min(0.6f, d.DamageReduction + 0.01f); break;
                        case TalentStat.Regen: d.RegenPctPerSec += 0.0005f; break;
                        case TalentStat.ResonancePct: d.ResonancePct += 0.03f; break;
                        case TalentStat.ChainRadius: d.ChainRadiusPct += 0.03f; break;
                        case TalentStat.GaugeRate: h.GaugeRate += 0.03f; break;
                    }
                }
            s.MaxHp *= 1f + hpPct;
            if (Has(h, Offense, 0)) d.CritMult += 0.3f;
            if (Has(h, Defense, 0)) d.DamageReduction = Math.Min(0.6f, d.DamageReduction + 0.08f);
            if (Has(h, Synergy, 0)) h.GaugeRate += 0.25f;
        }
    }
}
