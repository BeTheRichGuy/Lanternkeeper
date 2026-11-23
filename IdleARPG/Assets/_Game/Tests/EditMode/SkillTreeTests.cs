using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>기억의 나무 (docs/specs/2026-11-21-skill-tree-gear-art.md 가안).</summary>
    public class SkillTreeTests
    {
        [TestCase("ashknight")]
        [TestCase("oathknight")]
        [TestCase("windhunter")]
        [TestCase("starseer")]
        public void Every_class_grows_one_tree_with_all_its_skills(string cls)
        {
            var t = SkillTree.For(cls);
            int skills = t.Nodes.Count(n => n.Kind == TreeNodeKind.Skill);
            Assert.That(skills, Is.EqualTo(SkillBook.For(cls).Count(s => s.Role != SkillRole.Ultimate)), "every ring skill is a node");
            Assert.That(t.Nodes.Count(n => n.Kind == TreeNodeKind.Mod), Is.EqualTo(skills * 2));
            Assert.That(t.Nodes.Count(n => n.Kind == TreeNodeKind.Evolution), Is.EqualTo(skills));
            Assert.That(t.Nodes.Count(n => n.Kind == TreeNodeKind.Key), Is.EqualTo(9));
            Assert.That(t.Nodes.Count(n => n.Kind == TreeNodeKind.Ultimate), Is.EqualTo(2));
            foreach (var n in t.Nodes) if (n.Parent != null) Assert.That(t.Get(n.Parent), Is.Not.Null, n.Id + " hangs on nothing");
            int total = t.Nodes.Sum(n => n.Cost);
            Assert.That(total, Is.GreaterThan(SkillTree.Points(new Hero { Level = 60 })), "at Lv60 a build is chosen, not everything");
        }

        [TestCase("ashknight")]
        [TestCase("oathknight")]
        [TestCase("windhunter")]
        [TestCase("starseer")]
        public void Auto_growth_fills_the_ring_and_never_overspends(string cls)
        {
            foreach (int lv in new[] { 1, 4, 8, 16, 30, 60 })
            {
                var w = new SimWorld(5, cls);
                w.Hero.Level = lv;
                w.RefreshRing();
                Assert.That(SkillTree.Spent(w.Hero), Is.LessThanOrEqualTo(SkillTree.Points(w.Hero)), cls + " Lv" + lv);
                int filled = w.Hero.Ring.Slots.Count(s => s != null);
                Assert.That(filled, Is.EqualTo(SkillBook.SlotsAt(lv)), cls + " Lv" + lv + ": every open slot has a skill");
                if (lv >= SkillBook.UltimateLevel) Assert.That(w.Hero.Ring.Ultimate, Is.Not.Null, cls + " Lv" + lv);
            }
        }

        [Test]
        public void Auto_growth_is_deterministic()
        {
            var a = new SimWorld(5, "windhunter"); a.Hero.Level = 37; a.RefreshRing();
            var b = new SimWorld(9, "windhunter"); b.Hero.Level = 37; b.RefreshRing();
            Assert.That(a.Hero.TreeOrder, Is.EqualTo(b.Hero.TreeOrder));
        }

        [Test]
        public void Practice_ten_opens_the_evolution_and_auto_growth_takes_it()
        {
            var w = new SimWorld(5, "ashknight");
            w.Hero.Level = 10;
            w.RefreshRing();
            var skill = w.Hero.Ring.Slots.First(s => s != null && s.EvolveTo != null);
            Assert.That(w.Hero.Evolved, Has.No.Member(skill.Id));
            w.Hero.SkillUses[skill.Id] = SkillBook.UsesFor(SkillTree.EvoMastery);
            w.RefreshRing();
            Assert.That(w.Hero.Evolved, Has.Member(skill.Id));
            Assert.That(w.Hero.Ring.Slots.Any(s => s != null && s.Id == skill.EvolveTo), Is.True, "the ring uses the evolved skill");
            Assert.That(w.Events.Any(e => e.Type == SimEventType.Evolved && e.SkillId == skill.EvolveTo), Is.True);
        }

        [Test]
        public void By_hand_an_evolution_waits_for_practice()
        {
            var w = new SimWorld(5, "ashknight");
            w.Hero.Level = 20;
            w.ResetTree();
            Assert.That(w.TakeTreeNode("s:ember_slash"), Is.True);
            Assert.That(w.TakeTreeNode("e:ember_slash"), Is.False, "숙련 10 first");
            w.Hero.SkillUses["ember_slash"] = SkillBook.UsesFor(SkillTree.EvoMastery);
            Assert.That(w.TakeTreeNode("e:ember_slash"), Is.True);
            Assert.That(w.Hero.Evolved, Has.Member("ember_slash"));
            Assert.That(w.Hero.TreeAuto, Is.False);
        }

        [Test]
        public void A_variant_changes_its_skill_only()
        {
            var w = new SimWorld(5, "windhunter");
            w.Hero.Level = 20;
            w.ResetTree();
            w.TakeTreeNode("s:chain_arrow");
            w.TakeTreeNode("m:chain_arrow:0"); // opener: 분신 +1
            var mods = SkillTree.WithTreeMods(w.Hero, SkillBook.Get("chain_arrow"), SkillMods.Neutral);
            Assert.That(mods.ExtraCopies, Is.EqualTo(1));
            var other = SkillTree.WithTreeMods(w.Hero, SkillBook.Get("venom_arrow"), SkillMods.Neutral);
            Assert.That(other.ExtraCopies, Is.EqualTo(0));
        }

        [Test]
        public void Only_skills_taken_in_the_tree_go_in_the_ring()
        {
            var w = new SimWorld(5, "windhunter");
            w.Hero.Level = 20;
            w.ResetTree();
            Assert.That(w.Hero.Ring.Slots.All(s => s == null), Is.True, "an empty tree, an empty ring");
            w.TakeTreeNode("s:venom_arrow");
            Assert.That(w.Hero.Ring.Slots.Count(s => s != null), Is.EqualTo(1));
            Assert.That(SkillBook.Available("windhunter", 20, new[] { "venom_arrow" }).Select(s => s.Id), Is.EqualTo(new[] { "venom_arrow" }));
        }

        [Test]
        public void A_hand_tree_survives_a_save_and_shrinks_after_rebirth()
        {
            var w = new SimWorld(5, "starseer");
            w.Hero.Level = 30;
            w.ResetTree();
            w.TakeTreeNode("s:fireball");
            w.TakeTreeNode("s:combustion");
            w.TakeTreeNode("m:fireball:0");
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.Hero.TreeAuto, Is.False);
            Assert.That(b.Hero.TreeOrder, Is.EqualTo(new[] { "s:fireball", "s:combustion", "m:fireball:0" }));

            // Fill it, then drop to Lv1: the latest picks go until it fits.
            foreach (var n in SkillTree.For("starseer").Nodes) b.TakeTreeNode(n.Id);
            b.Hero.Level = 1;
            b.RefreshRing();
            Assert.That(SkillTree.Spent(b.Hero), Is.LessThanOrEqualTo(SkillTree.Points(b.Hero)));
            Assert.That(b.Hero.TreeOrder[0], Is.EqualTo("s:fireball"));
        }

        [Test]
        public void A_save_from_before_the_tree_keeps_its_evolutions_and_grows_by_itself()
        {
            var w = new SimWorld(5, "ashknight");
            w.Hero.Level = 25;
            var s = w.ToSave();
            s.TreeVersion = 0;
            s.TreeNodes.Clear();
            s.Evolved.Clear();
            s.Evolved.Add("frost_brand"); // evolved under the old two-of-three rule, with little practice
            var b = SimWorld.FromSave(s);
            Assert.That(b.Hero.TreeAuto, Is.True);
            Assert.That(b.Hero.Evolved, Has.Member("frost_brand"));
            Assert.That(b.Hero.TreeNodes.Count, Is.GreaterThan(0));
        }
    }
}
