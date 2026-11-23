using System;
using System.Collections.Generic;

namespace Lanternkeeper.Sim
{
    public enum TreeNodeKind { Root, Skill, Mod, Passive, Key, Evolution, Ultimate }

    /// <summary>What a 변형 node does to its skill.</summary>
    public enum TreeMod { Power, Area, Cooldown, Copy }

    public sealed class TreeNode
    {
        public string Id;
        public TreeNodeKind Kind;
        /// <summary>0 offense, 1 defense, 2 synergy (the talent branches); −1 for the root and the ultimates.</summary>
        public int Branch = -1;
        /// <summary>Node that must be taken first (null: the root).</summary>
        public string Parent;
        /// <summary>Skill, evolution base or ultimate id.</summary>
        public string SkillId;
        public TreeMod Mod;
        public int KeyIndex, Cost = 1;
        /// <summary>Layout in tree cells: x across (branch columns), y up from the root (row).</summary>
        public float X, Y;
    }

    public sealed class TreeDef
    {
        public string ClassId;
        public readonly List<TreeNode> Nodes = new List<TreeNode>();
        public readonly Dictionary<string, TreeNode> ById = new Dictionary<string, TreeNode>();
        public TreeNode Get(string id) => id != null && ById.TryGetValue(id, out var n) ? n : null;
    }

    /// <summary>
    /// 기억의 나무 (docs/specs/2026-11-21-skill-tree-gear-art.md 가안): one tree per class. From the lantern root three
    /// branches (the talent branches: offense, defense, synergy) climb as a chain skill → passive → skill …; each skill
    /// carries two 변형 nodes and its evolution, each branch three keystones (the old talent keystones) that open once
    /// enough points sit in the branch. Two ultimates crown the trunk at Lv30 (one may be taken).
    /// A point per 통합 레벨. 자동 분배 (default) follows the ring in use; by hand the player picks, and respec is free.
    /// </summary>
    public static class SkillTree
    {
        public const string RootId = "root";
        public const int KeyCost = 2, PassiveWeight = 5, EvoMastery = 10;
        /// <summary>Points already in the branch a keystone asks for. A four-skill branch holds 16 without evolutions
        /// (skills, passives, 변형), so its tip keystone must open at 15.</summary>
        public static readonly int[] KeyThreshold = { 4, 9, 15 };

        /// <summary>Skills per branch, in unlock order (talent branch themes: 잿불·불굴·잿더미 and so on).</summary>
        static readonly Dictionary<string, string[][]> Branches = new Dictionary<string, string[][]>
        {
            ["ashknight"] = new[]
            {
                new[] { "ember_slash", "storm_cleave", "forge_strike", "fissure", "smith_hammer" },
                new[] { "heat_absorb", "thorn_mail", "molten_heart", "frostfire_aura" },
                new[] { "ember_whirl", "ash_leap", "frost_brand", "chain_hook", "cinder_echo" },
            },
            ["oathknight"] = new[]
            {
                new[] { "blessed_hammer", "judgment", "heaven_spears", "shield_throw", "bell_of_judgment", "holy_charge" },
                new[] { "zeal", "shield_bash", "rallying_cry", "oath_barrier", "unbroken_vow", "warding_sigil" },
                new[] { "hallowed_aura", "martyr_chains", "consecrate", "grace_echo" },
            },
            ["windhunter"] = new[]
            {
                new[] { "chain_arrow", "gale_volley", "ricochet_arrow", "storm_eye", "arrow_rain" },
                new[] { "whirling_blades", "roll_shot", "shock_trap", "wind_path", "shadow_decoy" },
                new[] { "venom_arrow", "rot_cloud", "hunters_mark", "void_lance", "viper_nest" },
            },
            ["starseer"] = new[]
            {
                new[] { "fireball", "combustion", "meteor", "nebula_burst", "supernova" },
                new[] { "ice_bolt", "ice_shatter", "frost_nova", "blizzard", "starfall_field" },
                new[] { "chain_lightning", "thunder_nova", "lightning_storm", "ball_lightning" },
            },
        };

        static readonly Dictionary<string, TreeDef> _trees = new Dictionary<string, TreeDef>();

        public static TreeDef For(string classId)
        {
            if (string.IsNullOrEmpty(classId) || !Branches.ContainsKey(classId)) classId = "ashknight";
            if (_trees.TryGetValue(classId, out var t)) return t;
            return _trees[classId] = Build(classId);
        }

        static TreeDef Build(string classId)
        {
            var t = new TreeDef { ClassId = classId };
            void Add(TreeNode n) { t.Nodes.Add(n); t.ById[n.Id] = n; }
            Add(new TreeNode { Id = RootId, Kind = TreeNodeKind.Root, Cost = 0, X = 0f, Y = 0f });
            var branches = Branches[classId];
            for (int b = 0; b < 3; b++)
            {
                // Three columns, wide apart: 변형 on both sides of a skill, evolution and keystones on the branch's outer
                // side (the middle branch's outer side is to the right), so no node reads as part of the next branch.
                float bx = (b - 1) * 5f;
                float outer = b == 0 ? -1f : 1f;
                string prev = RootId;
                int passive = 0;
                var skills = branches[b];
                float y = 1f;
                for (int i = 0; i < skills.Length; i++)
                {
                    // Skills chain straight to each other; the passive between two skills hangs off the lower one, so
                    // reaching a ring skill never costs passives (at Lv8 a four-skill ring must fit in the points).
                    if (i > 0)
                    {
                        Add(new TreeNode { Id = "p:" + b + ":" + passive++, Kind = TreeNodeKind.Passive, Branch = b, Parent = prev, X = bx, Y = y });
                        y += 0.8f;
                    }
                    var def = SkillBook.Get(skills[i]);
                    var s = new TreeNode { Id = "s:" + skills[i], Kind = TreeNodeKind.Skill, Branch = b, Parent = prev, SkillId = skills[i], X = bx, Y = y };
                    Add(s);
                    var mods = ModsForRole(def.Role);
                    Add(new TreeNode { Id = "m:" + skills[i] + ":0", Kind = TreeNodeKind.Mod, Branch = b, Parent = s.Id, SkillId = skills[i], Mod = mods[0], X = bx - 1f, Y = y });
                    Add(new TreeNode { Id = "m:" + skills[i] + ":1", Kind = TreeNodeKind.Mod, Branch = b, Parent = s.Id, SkillId = skills[i], Mod = mods[1], X = bx + 1f, Y = y });
                    Add(new TreeNode { Id = "e:" + skills[i], Kind = TreeNodeKind.Evolution, Branch = b, Parent = s.Id, SkillId = skills[i], X = bx + outer * 2f, Y = y + 0.25f });
                    prev = s.Id;
                    // Keystones sit off the chain after the 2nd and 4th skill and at the tip (thresholds, not gates).
                    if (i == 1 || i == 3)
                    {
                        int k = i == 1 ? 0 : 1;
                        Add(new TreeNode { Id = "k:" + b + ":" + k, Kind = TreeNodeKind.Key, Branch = b, Parent = s.Id, KeyIndex = k, Cost = KeyCost, X = bx + outer * 1.2f, Y = y + 0.95f });
                    }
                    y += 1.6f;
                }
                Add(new TreeNode { Id = "p:" + b + ":" + passive, Kind = TreeNodeKind.Passive, Branch = b, Parent = prev, X = bx, Y = y });
                Add(new TreeNode { Id = "k:" + b + ":2", Kind = TreeNodeKind.Key, Branch = b, Parent = prev, KeyIndex = 2, Cost = KeyCost, X = bx, Y = y + 1f });
            }
            // Two ultimates crown the trunk.
            float top = 0f;
            foreach (var n in t.Nodes) top = Math.Max(top, n.Y);
            int u = 0;
            foreach (var s in SkillBook.For(classId))
                if (s.Role == SkillRole.Ultimate)
                {
                    Add(new TreeNode { Id = "u:" + s.Id, Kind = TreeNodeKind.Ultimate, SkillId = s.Id, Parent = RootId, X = u == 0 ? -1.2f : 1.2f, Y = top + 1.1f });
                    u++;
                }
            return t;
        }

        static TreeMod[] ModsForRole(SkillRole r)
        {
            switch (r)
            {
                case SkillRole.Opener: return new[] { TreeMod.Copy, TreeMod.Cooldown };
                case SkillRole.Extender: return new[] { TreeMod.Area, TreeMod.Cooldown };
                case SkillRole.Finisher: return new[] { TreeMod.Power, TreeMod.Area };
                case SkillRole.Defense: return new[] { TreeMod.Cooldown, TreeMod.Power };
                default: return new[] { TreeMod.Area, TreeMod.Power };
            }
        }

        public static string ModText(TreeMod m)
        {
            switch (m)
            {
                case TreeMod.Copy: return "분신 +1";
                case TreeMod.Area: return "범위 +20%";
                case TreeMod.Cooldown: return "재사용 대기시간 −15%";
                default: return "위력 +20%";
            }
        }

        // ---------- points ----------

        /// <summary>A point per 통합 레벨 plus four to start (the old talents began at Lv5): about 64 at Lv60 for a tree
        /// of about 88, so a build is chosen; past 통합 84 the whole tree is lit.</summary>
        public const int StartBonus = 4;

        public static int Points(Hero h) => Math.Max(1, h.IntegratedLevel) + StartBonus;

        public static int Spent(Hero h)
        {
            var t = For(h.ClassId);
            int n = 0;
            foreach (var id in h.TreeOrder) { var node = t.Get(id); if (node != null) n += node.Cost; }
            return n;
        }

        public static int BranchPoints(Hero h, int branch)
        {
            var t = For(h.ClassId);
            int n = 0;
            foreach (var id in h.TreeOrder)
            {
                var node = t.Get(id);
                if (node != null && node.Branch == branch && node.Kind != TreeNodeKind.Key) n += node.Cost;
            }
            return n;
        }

        public static bool Has(Hero h, string id) => id == RootId || h.TreeNodes.Contains(id);

        /// <summary>Why a node cannot be taken now, or null if it can.</summary>
        public static string Blocker(Hero h, TreeNode n)
        {
            if (n == null) return "존재하지 않는 노드";
            if (Has(h, n.Id)) return "습득 완료";
            if (n.Parent != null && !Has(h, n.Parent)) return "선행 노드 필요";
            if (Spent(h) + n.Cost > Points(h)) return "포인트 부족";
            switch (n.Kind)
            {
                case TreeNodeKind.Skill:
                    var def = SkillBook.Get(n.SkillId);
                    if (def.Unlock > h.IntegratedLevel) return "통합 레벨 " + def.Unlock + " 필요";
                    break;
                case TreeNodeKind.Key:
                    if (BranchPoints(h, n.Branch) < KeyThreshold[n.KeyIndex]) return "해당 갈래에 " + KeyThreshold[n.KeyIndex] + "포인트 필요";
                    break;
                case TreeNodeKind.Evolution:
                    if (MasteryOf(h, n.SkillId) < EvoMastery) return "숙련 " + EvoMastery + " 필요";
                    break;
                case TreeNodeKind.Ultimate:
                    if (h.IntegratedLevel < SkillBook.UltimateLevel) return "통합 레벨 " + SkillBook.UltimateLevel + " 필요";
                    foreach (var other in h.TreeNodes) if (other.StartsWith("u:", StringComparison.Ordinal)) return "궁극기는 하나만 선택 가능";
                    break;
            }
            return null;
        }

        public static int MasteryOf(Hero h, string skillId)
        {
            h.SkillUses.TryGetValue(skillId, out int uses);
            return SkillBook.SkillLevel(uses);
        }

        /// <summary>Takes a node by hand (turns 자동 분배 off). False if it cannot be taken.</summary>
        public static bool Allocate(Hero h, string id)
        {
            var n = For(h.ClassId).Get(id);
            if (Blocker(h, n) != null) return false;
            h.TreeAuto = false;
            h.TreeNodes.Add(id);
            h.TreeOrder.Add(id);
            return true;
        }

        /// <summary>Clears every point (respec is free); stays in hand mode.</summary>
        public static void ResetAll(Hero h)
        {
            h.TreeAuto = false;
            h.TreeNodes.Clear();
            h.TreeOrder.Clear();
        }

        // ---------- what the tree gives ----------

        public static bool SkillUnlocked(Hero h, string skillId) => h.TreeNodes.Contains("s:" + skillId) || h.TreeNodes.Contains("u:" + skillId);

        public static bool HasKey(Hero h, int branch, int k) => h.TreeNodes.Contains("k:" + branch + ":" + k);

        /// <summary>Adds the skill's taken 변형 to the ring's glyph mods.</summary>
        public static SkillMods WithTreeMods(Hero h, SkillDef skill, SkillMods m)
        {
            if (skill == null) return m;
            string b = skill.BaseId;
            for (int i = 0; i < 2; i++)
            {
                string id = "m:" + b + ":" + i;
                if (!h.TreeNodes.Contains(id)) continue;
                var n = For(h.ClassId).Get(id);
                if (n == null) continue;
                switch (n.Mod)
                {
                    // 60 h sim (11-21): at +30% / +25% / −20% ranged DPS rose ×6 and every class reached act VI 7–12 h
                    // sooner than before the tree; toned to +20% / +20% / −15%.
                    case TreeMod.Power: m.PowerMul *= 1.2f; break;
                    case TreeMod.Area: m.RadiusMul *= 1.2f; break;
                    case TreeMod.Cooldown: m.CooldownMul *= 0.85f; break;
                    case TreeMod.Copy: m.ExtraCopies += 1; break;
                }
            }
            return m;
        }

        /// <summary>
        /// Re-derives what the tree gives: talent points per branch (passives × 5), and evolutions (taken evolution nodes,
        /// plus the ones earned before the tree existed). Returns the evolutions that are new.
        /// </summary>
        public static List<string> Apply(Hero h)
        {
            var t = For(h.ClassId);
            for (int b = 0; b < 3; b++) h.TalentPoints[b] = 0;
            foreach (var id in h.TreeNodes)
            {
                var n = t.Get(id);
                if (n != null && n.Kind == TreeNodeKind.Passive && n.Branch >= 0) h.TalentPoints[n.Branch] += PassiveWeight;
            }
            var fresh = new List<string>();
            var want = new HashSet<string>(h.LegacyEvolved);
            foreach (var id in h.TreeNodes) if (id.StartsWith("e:", StringComparison.Ordinal)) want.Add(id.Substring(2));
            foreach (var id in want) if (!h.Evolved.Contains(id)) fresh.Add(id);
            h.Evolved.Clear();
            foreach (var id in want) h.Evolved.Add(id);
            fresh.Sort(StringComparer.Ordinal);
            return fresh;
        }

        // ---------- 자동 분배 ----------

        /// <summary>
        /// Spends every point from scratch along the build in use: the ring's skills first (walking each branch up to
        /// them), their evolutions once practised, their 변형, then passives and keystones of the branch holding most
        /// of the ring, then the rest. Deterministic: the same hero always grows the same tree.
        /// </summary>
        public static void AutoAllocate(Hero h, IList<string> wanted)
        {
            var t = For(h.ClassId);
            h.TreeNodes.Clear();
            h.TreeOrder.Clear();
            var ring = new List<string>();
            foreach (var id in wanted) if (id != null && t.Get("s:" + id) != null && !ring.Contains(id)) ring.Add(id);
            var branchCount = new int[3];
            foreach (var id in ring) branchCount[t.Get("s:" + id).Branch]++;
            int focus = 0;
            for (int b = 1; b < 3; b++) if (branchCount[b] > branchCount[focus]) focus = b;

            bool Take(TreeNode n)
            {
                if (Blocker(h, n) != null) return false;
                h.TreeNodes.Add(n.Id);
                h.TreeOrder.Add(n.Id);
                return true;
            }
            // Walk the chain toward a node: take the first missing ancestor that can be taken.
            bool Toward(TreeNode target)
            {
                var path = new List<TreeNode>();
                for (var n = target; n != null && n.Id != RootId; n = t.Get(n.Parent)) path.Add(n);
                for (int i = path.Count - 1; i >= 0; i--)
                {
                    if (Has(h, path[i].Id)) continue;
                    return Take(path[i]);
                }
                return false;
            }
            bool TakeInBranch(int b)
            {
                // Keystones as soon as they open, then the next passive up the chain, then its skills.
                foreach (var n in t.Nodes) if (n.Branch == b && n.Kind == TreeNodeKind.Key && Take(n)) return true;
                foreach (var n in t.Nodes) if (n.Branch == b && n.Kind == TreeNodeKind.Passive && Take(n)) return true;
                foreach (var n in t.Nodes) if (n.Branch == b && n.Kind == TreeNodeKind.Skill && Take(n)) return true;
                return false;
            }

            for (int guard = 0; guard < 400 && Spent(h) < Points(h); guard++)
            {
                bool took = false;
                foreach (var id in ring) if (!took && !Has(h, "s:" + id)) took = Toward(t.Get("s:" + id));
                if (!took) foreach (var id in ring) if (!took && Has(h, "s:" + id)) took = Take(t.Get("e:" + id));
                if (!took) foreach (var id in ring) for (int i = 0; i < 2 && !took; i++) if (Has(h, "s:" + id)) took = Take(t.Get("m:" + id + ":" + i));
                if (!took && h.IntegratedLevel >= SkillBook.UltimateLevel)
                {
                    // The build's own ultimate first (it is in `wanted`), else the first one.
                    foreach (var id in wanted) if (!took && id != null) { var u = t.Get("u:" + id); if (u != null) took = Take(u); }
                    foreach (var n in t.Nodes) if (!took && n.Kind == TreeNodeKind.Ultimate) took = Take(n);
                }
                if (!took) took = TakeInBranch(focus);
                if (!took) for (int b = 0; b < 3 && !took; b++) if (b != focus) took = TakeInBranch(b);
                if (!took) foreach (var n in t.Nodes) if (!took && (n.Kind == TreeNodeKind.Mod || n.Kind == TreeNodeKind.Evolution)) took = Take(n);
                if (!took) break;
            }
        }

        /// <summary>By hand after the points shrank (rebirth): drop the latest picks until the tree fits again.</summary>
        public static void TrimToPoints(Hero h)
        {
            while (h.TreeOrder.Count > 0 && Spent(h) > Points(h))
            {
                string last = h.TreeOrder[h.TreeOrder.Count - 1];
                h.TreeOrder.RemoveAt(h.TreeOrder.Count - 1);
                h.TreeNodes.Remove(last);
            }
        }
    }
}
