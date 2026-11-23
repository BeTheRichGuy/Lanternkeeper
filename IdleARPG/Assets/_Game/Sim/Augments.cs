using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    public enum AugTier { Silver, Gold, Prism }

    public enum AugKind { Element, SkillPower, CritOnUse, Proc, CritChance, AttackSpeed, MaxHp, MoveSpeed, Cooldown }

    /// <summary>One 증강 card.</summary>
    public sealed class AugmentDef
    {
        public string Id, Name, Text;
        public AugTier Tier;
        public AugKind Kind;
        /// <summary>Skill-tree branch it leans on (0..2); −1 for the general cards every build can use.</summary>
        public int Branch = -1;
        public Element Elem;
        public string SkillId, ProcSkillId;
        /// <summary>A share (0.5 = +50%), or the chance of a <see cref="AugKind.Proc"/>.</summary>
        public float Value;
    }

    /// <summary>
    /// 환생 증강 카드 (희승 11-22): "죽었다 태어날때마다 정해지는 카드가 있어. 약간 롤 증바람의 증강 같은 느낌 … 0~20레벨에는
    /// 카드 1개가 맥스. 20~40레벨에는 2개, 40~60레벨에는 3개 … 이 증강들이 다시 태어날때마다 빌드를 강제". Cards are dealt
    /// three at a time when a slot opens (one right after a rebirth, one at Lv20, one at Lv40); each slot may be re-dealt
    /// once. Most cards lean on one skill-tree branch (its element, one of its skills, or a skill that sets off another), so
    /// each life pulls the build toward a branch the last one may have left alone.
    /// </summary>
    public static class Augments
    {
        public const int MaxSlots = 3;
        /// <summary>A card is picked for the player after this long (방치 — nobody may be watching).</summary>
        public const double AutoPickSeconds = 30.0;
        /// <summary>Seconds a <see cref="AugKind.CritOnUse"/> card's crit damage lasts after its skill.</summary>
        public const double CritOnUseSeconds = 3.0;

        public static int SlotsAt(int level) => level >= 40 ? 3 : level >= 20 ? 2 : 1;

        static readonly Dictionary<string, List<AugmentDef>> ByClass = new Dictionary<string, List<AugmentDef>>();
        static readonly Dictionary<string, AugmentDef> ById = new Dictionary<string, AugmentDef>();

        public static AugmentDef Get(string id)
        {
            if (id == null) return null;
            if (!ById.ContainsKey(id)) foreach (var cls in SkillBook.PlayableClasses) For(cls);
            return ById.TryGetValue(id, out var a) ? a : null;
        }

        public static IReadOnlyList<AugmentDef> For(string classId)
        {
            classId = classId ?? "ashknight";
            if (!ByClass.TryGetValue(classId, out var list)) { list = Build(classId); ByClass[classId] = list; }
            return list;
        }

        public static string ElementName(Element e) =>
            e == Element.Ember ? "잿불" : e == Element.Frost ? "서리" : e == Element.Shock ? "전격" : e == Element.Rot ? "역병" : e == Element.Void ? "공허" : "물리";

        public static string TierName(AugTier t) => t == AugTier.Prism ? "프리즘" : t == AugTier.Gold ? "금" : "은";

        /// <summary>The skills of one branch, top of the tree first.</summary>
        public static List<string> BranchSkills(string classId, int branch)
        {
            var nodes = new List<TreeNode>();
            foreach (var n in SkillTree.For(classId).Nodes) if (n.Kind == TreeNodeKind.Skill && n.Branch == branch) nodes.Add(n);
            nodes.Sort((a, b) => a.Y.CompareTo(b.Y));
            var ids = new List<string>();
            foreach (var n in nodes) ids.Add(n.SkillId);
            return ids;
        }

        static bool Damaging(SkillDef s) => s.Shape != SkillShape.Buff && s.Shape != SkillShape.Aura && s.Shape != SkillShape.Armed;

        static List<AugmentDef> Build(string classId)
        {
            var list = new List<AugmentDef>();
            void Add(AugmentDef a) { a.Id = classId + ":" + list.Count; list.Add(a); ById[a.Id] = a; }
            var elemDone = new HashSet<Element>();
            for (int b = 0; b < 3; b++)
            {
                var skills = BranchSkills(classId, b);
                if (skills.Count == 0) continue;
                // The branch's element: the one most of its skills carry.
                var count = new int[6];
                foreach (var id in skills) { var s = SkillBook.Get(id); if (s != null) count[(int)s.Elem]++; }
                var elem = Element.None;
                for (int e = 1; e < 6; e++) if (count[e] > 0 && (elem == Element.None || count[e] > count[(int)elem])) elem = (Element)e;
                if (elem != Element.None && !elemDone.Contains(elem)) // one pair of cards per element (two branches may share it)
                {
                    elemDone.Add(elem);
                    string en = ElementName(elem);
                    Add(new AugmentDef { Name = en + " 각인", Text = en + " 피해 +30%", Tier = AugTier.Silver, Kind = AugKind.Element, Branch = b, Elem = elem, Value = 0.30f });
                    Add(new AugmentDef { Name = en + "의 군주", Text = en + " 피해 +55%", Tier = AugTier.Gold, Kind = AugKind.Element, Branch = b, Elem = elem, Value = 0.55f });
                }
                int main = Math.Min(3, skills.Count);
                for (int i = 0; i < main; i++)
                {
                    var s = SkillBook.Get(skills[i]);
                    if (s == null || !Damaging(s)) continue;
                    string sn = s.Name;
                    Add(new AugmentDef { Name = sn + " 숙달", Text = sn + " 피해 +40%", Tier = AugTier.Silver, Kind = AugKind.SkillPower, Branch = b, SkillId = s.Id, Value = 0.40f });
                    Add(new AugmentDef { Name = sn + " 급소", Text = sn + " 사용 후 3초간 치명타 피해 +60%", Tier = AugTier.Gold, Kind = AugKind.CritOnUse, Branch = b, SkillId = s.Id, Value = 0.60f });
                    // Sets off the next damaging skill down the same branch.
                    for (int k = 1; k < skills.Count; k++)
                    {
                        var t = SkillBook.Get(skills[(i + k) % skills.Count]);
                        if (t == null || t.Id == s.Id || !Damaging(t)) continue;
                        Add(new AugmentDef { Name = sn + " → " + t.Name, Text = sn + " 사용 시 25% 확률로 " + t.Name + " 발동", Tier = AugTier.Prism, Kind = AugKind.Proc, Branch = b, SkillId = s.Id, ProcSkillId = t.Id, Value = 0.25f });
                        break;
                    }
                }
            }
            Add(new AugmentDef { Name = "날 선 눈", Text = "치명타 확률 +6%", Tier = AugTier.Silver, Kind = AugKind.CritChance, Value = 0.06f });
            Add(new AugmentDef { Name = "가쁜 숨", Text = "공격 속도 +12%", Tier = AugTier.Silver, Kind = AugKind.AttackSpeed, Value = 0.12f });
            Add(new AugmentDef { Name = "질긴 심장", Text = "최대 생명력 +15%", Tier = AugTier.Silver, Kind = AugKind.MaxHp, Value = 0.15f });
            Add(new AugmentDef { Name = "바람걸음", Text = "이동 속도 +10%", Tier = AugTier.Silver, Kind = AugKind.MoveSpeed, Value = 0.10f });
            Add(new AugmentDef { Name = "끊이지 않는 기도", Text = "재사용 대기시간 −12%", Tier = AugTier.Gold, Kind = AugKind.Cooldown, Value = 0.12f });
            Add(new AugmentDef { Name = "처형인의 눈", Text = "치명타 확률 +10%", Tier = AugTier.Gold, Kind = AugKind.CritChance, Value = 0.10f });
            return list;
        }

        // ---------- effects ----------

        /// <summary>HeroStats.Recompute: the chosen cards' standing effects (and the caches combat reads).</summary>
        public static void Apply(Hero h)
        {
            Array.Clear(h.AugElem, 0, h.AugElem.Length);
            h.AugPower.Clear();
            h.AugCrit.Clear();
            h.AugProcs.Clear();
            foreach (var id in h.Augments)
            {
                var a = Get(id);
                if (a == null) continue;
                switch (a.Kind)
                {
                    case AugKind.Element: h.AugElem[(int)a.Elem] += a.Value; break;
                    case AugKind.SkillPower: h.AugPower[a.SkillId] = (h.AugPower.TryGetValue(a.SkillId, out var p) ? p : 0f) + a.Value; break;
                    case AugKind.CritOnUse: h.AugCrit[a.SkillId] = (h.AugCrit.TryGetValue(a.SkillId, out var c) ? c : 0f) + a.Value; break;
                    case AugKind.Proc: h.AugProcs.Add(a); break;
                    case AugKind.CritChance: h.Derived.CritChance += a.Value; break;
                    case AugKind.AttackSpeed: h.Stats.AttackSpeed *= 1f + a.Value; break;
                    case AugKind.MaxHp: h.Stats.MaxHp *= 1f + a.Value; break;
                    case AugKind.MoveSpeed: h.Stats.MoveSpeed *= 1f + a.Value; break;
                    case AugKind.Cooldown: h.Derived.CooldownCut = Math.Min(0.6f, h.Derived.CooldownCut + a.Value); break;
                }
            }
        }

        public static float PowerFor(Hero h, string skillBaseId) => skillBaseId != null && h.AugPower.TryGetValue(skillBaseId, out var v) ? v : 0f;

        // ---------- dealing ----------

        static int Weight(AugTier t) => t == AugTier.Prism ? 1 : t == AugTier.Gold ? 3 : 6;

        /// <summary>
        /// Three cards for the next slot. Deterministic per (seed, life, deal number); the second card comes from another
        /// branch than the first, so every deal offers a turn.
        /// </summary>
        public static string[] Deal(Hero h, int seed, int deal)
        {
            var rng = new SimRng(unchecked((ulong)seed * 0x9E3779B97F4A7C15UL ^ ((ulong)h.Rebirths * 7919UL + (ulong)deal * 104729UL + 1UL)));
            var pool = new List<AugmentDef>();
            foreach (var a in For(h.ClassId)) if (!h.Augments.Contains(a.Id)) pool.Add(a);
            var picked = new List<AugmentDef>();
            for (int k = 0; k < 3 && pool.Count > 0; k++)
            {
                var cands = pool;
                if (k == 1)
                {
                    var other = pool.FindAll(a => a.Branch != picked[0].Branch);
                    if (other.Count > 0) cands = other;
                }
                int total = 0;
                foreach (var a in cands) total += Weight(a.Tier);
                int roll = rng.Range(0, total);
                var pick = cands[cands.Count - 1];
                foreach (var a in cands) { roll -= Weight(a.Tier); if (roll < 0) { pick = a; break; } }
                picked.Add(pick);
                pool.Remove(pick);
            }
            var ids = new string[picked.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = picked[i].Id;
            return ids;
        }

        /// <summary>
        /// The card picked when nobody chooses: one for the branch the build already uses, else a general one, the rarest
        /// first. Turning the build is the player's call — a 20 h sim where the idle pick turned the 서약기사 to its defence
        /// branch left it in act V at Lv40.
        /// </summary>
        public static int BestPick(string[] offer, int branchInUse)
        {
            int best = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < offer.Length; i++)
            {
                var a = Get(offer[i]);
                if (a == null) continue;
                float score = (a.Branch == branchInUse ? 20f : a.Branch < 0 ? 10f : 0f) + (int)a.Tier * 5f;
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }
    }
}
