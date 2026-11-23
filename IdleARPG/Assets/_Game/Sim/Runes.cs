using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    /// <summary>GDD §8.7: twelve runes. Each adds a stat to its item; the right ones in the right order make a runeword.</summary>
    public enum Rune { None, Ash, Frost, Thunder, Rot, Void, Lantern, Oath, Blood, Star, Wind, Bone, Gate }

    public sealed class RunewordDef
    {
        public string Id, Name, Text;
        public Rune[] Recipe;
    }

    public static class Runes
    {
        public const int Count = 13; // including None

        public static string Name(Rune r)
        {
            switch (r)
            {
                case Rune.Ash: return "잿";
                case Rune.Frost: return "서리";
                case Rune.Thunder: return "천둥";
                case Rune.Rot: return "부패";
                case Rune.Void: return "공허";
                case Rune.Lantern: return "등불";
                case Rune.Oath: return "맹세";
                case Rune.Blood: return "피";
                case Rune.Star: return "별";
                case Rune.Wind: return "바람";
                case Rune.Bone: return "뼈";
                case Rune.Gate: return "문";
                default: return "";
            }
        }

        /// <summary>What a socketed rune adds to its item.</summary>
        public static (Stat stat, float value) StatOf(Rune r)
        {
            switch (r)
            {
                case Rune.Ash: return (Stat.EmberPct, 5f);
                case Rune.Frost: return (Stat.FrostPct, 5f);
                case Rune.Thunder: return (Stat.ShockPct, 5f);
                case Rune.Rot: return (Stat.ResonancePct, 3f);
                case Rune.Void: return (Stat.CritDamagePct, 8f);
                case Rune.Lantern: return (Stat.ChainRadiusPct, 4f);
                case Rune.Oath: return (Stat.DamageReductionPct, 2f);
                case Rune.Blood: return (Stat.RegenPct, 0.15f);
                case Rune.Star: return (Stat.CritChancePct, 1.5f);
                case Rune.Wind: return (Stat.AttackSpeedPct, 3f);
                case Rune.Bone: return (Stat.MaxHp, 15f);
                case Rune.Gate: return (Stat.XpPct, 4f);
                default: return (Stat.FlatDamage, 0f);
            }
        }

        /// <summary>Ten runewords (D2 homage, effects in this game's systems). Order matters.</summary>
        public static readonly RunewordDef[] Words =
        {
            new RunewordDef { Id = "rw_cycle",         Name = "순환",       Recipe = new[] { Rune.Ash, Rune.Frost, Rune.Thunder },  Text = "정방향 공명 피해 +25%" },
            new RunewordDef { Id = "rw_lanternkeeper", Name = "등불지기",   Recipe = new[] { Rune.Lantern, Rune.Oath, Rune.Lantern }, Text = "사망 전파 횟수 +1" },
            new RunewordDef { Id = "rw_starpath",      Name = "별의 길",    Recipe = new[] { Rune.Star, Rune.Wind, Rune.Star },     Text = "공명 게이지 충전 속도 +20%" },
            new RunewordDef { Id = "rw_bonecitadel",   Name = "뼈의 성채",  Recipe = new[] { Rune.Bone, Rune.Oath, Rune.Bone },     Text = "받는 피해 −10%" },
            new RunewordDef { Id = "rw_firering",      Name = "화염의 고리", Recipe = new[] { Rune.Ash, Rune.Ash },                 Text = "자원 최대치 강화 위력 +50%" },
            new RunewordDef { Id = "rw_icecage",       Name = "얼음 감옥",  Recipe = new[] { Rune.Frost, Rune.Gate },               Text = "과부하 기절 지속시간 +0.5초" },
            new RunewordDef { Id = "rw_stormvein",     Name = "폭풍 맥",    Recipe = new[] { Rune.Thunder, Rune.Wind },             Text = "공격 속도 +15%" },
            new RunewordDef { Id = "rw_plaguebreath",  Name = "역병의 숨",  Recipe = new[] { Rune.Rot, Rune.Rot },                  Text = "역병 전이 반경 +1칸" },
            new RunewordDef { Id = "rw_voidgate",      Name = "공허의 문",  Recipe = new[] { Rune.Void, Rune.Gate },                Text = "붕괴의 끌어당김 두 배" },
            new RunewordDef { Id = "rw_bloodpact",     Name = "피의 계약",  Recipe = new[] { Rune.Blood, Rune.Bone },               Text = "처치 시 생명력 0.5% 회복" },
        };

        public static RunewordDef Match(List<int> runes)
        {
            if (runes == null || runes.Count < 2) return null;
            foreach (var w in Words)
            {
                if (w.Recipe.Length != runes.Count) continue;
                bool ok = true;
                for (int i = 0; i < runes.Count && ok; i++) ok = (int)w.Recipe[i] == runes[i];
                if (ok) return w;
            }
            return null;
        }

        /// <summary>Stat-shaped runeword effects, applied after gear and talents (rule-shaped ones live in the sim).</summary>
        public static void ApplyWords(Hero h)
        {
            var d = h.Derived;
            if (d.Runewords.Contains("rw_starpath")) h.GaugeRate += 0.2f;
            if (d.Runewords.Contains("rw_bonecitadel")) d.DamageReduction = Math.Min(0.6f, d.DamageReduction + 0.1f);
            if (d.Runewords.Contains("rw_stormvein")) h.Stats.AttackSpeed *= 1.15f;
        }

        /// <summary>Rune drop: commons rarely, elites often, bosses always (two). Deeper runes come later.</summary>
        public static Rune Roll(SimRng rng, int areaLevel)
        {
            int top = Math.Min(Count - 1, 4 + areaLevel / 2); // early floors: the elemental runes; deeper: all twelve
            return (Rune)rng.Range(1, top + 1);
        }

        /// <summary>
        /// Idle auto-socketing: an equipped item with empty sockets takes the first runeword (in list order) whose recipe
        /// fits its socket count exactly and whose runes are all in the bag. Partial fills are never made (runes wait).
        /// </summary>
        public static bool AutoSocket(Item item, int[] bag)
        {
            if (item == null || item.Sockets < 2 || item.RuneSlots == null || item.RuneSlots.Count > 0) return false;
            foreach (var w in Words)
            {
                if (w.Recipe.Length != item.Sockets) continue;
                var need = new int[Count];
                foreach (var r in w.Recipe) need[(int)r]++;
                bool ok = true;
                for (int i = 1; i < Count && ok; i++) ok = bag[i] >= need[i];
                if (!ok) continue;
                for (int i = 1; i < Count; i++) bag[i] -= need[i];
                foreach (var r in w.Recipe) item.RuneSlots.Add((int)r);
                return true;
            }
            return false;
        }
    }
}
