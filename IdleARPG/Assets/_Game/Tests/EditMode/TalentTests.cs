using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §7.2 talent keystones, now nodes of 기억의 나무 (11-21).</summary>
    public class TalentTests
    {
        /// <summary>By hand: the branch's skills and passives (and 변형 if needed) until the keystone opens, then it.</summary>
        public static void GiveKey(SimWorld w, int branch, int k)
        {
            var t = SkillTree.For(w.Hero.ClassId);
            w.ResetTree();
            string key = "k:" + branch + ":" + k;
            // Skills and the passives between them in tree order (a passive hangs off the skill below it), then 변형.
            foreach (var n in t.Nodes)
                if (n.Branch == branch && (n.Kind == TreeNodeKind.Skill || n.Kind == TreeNodeKind.Passive)) w.TakeTreeNode(n.Id);
            foreach (var n in t.Nodes)
            {
                if (SkillTree.BranchPoints(w.Hero, branch) >= SkillTree.KeyThreshold[k]) break;
                if (n.Branch == branch && n.Kind == TreeNodeKind.Mod) w.TakeTreeNode(n.Id);
            }
            for (int j = 0; j <= k; j++) w.TakeTreeNode("k:" + branch + ":" + j);
            Assert.That(Talents.Has(w.Hero, branch, k), Is.True, key + " should be taken");
        }

        [Test]
        public void Defense_keystone_cuts_damage_and_passives_raise_life()
        {
            var w = new SimWorld(81);
            w.Hero.Level = 20;
            w.ResetTree();
            float hp0 = w.Hero.Stats.MaxHp;
            GiveKey(w, Talents.Defense, 0);
            Assert.That(w.Hero.TalentPoints[Talents.Defense], Is.GreaterThan(0));
            Assert.That(w.Hero.Stats.MaxHp, Is.GreaterThan(hp0));
            Assert.That(w.Hero.Derived.DamageReduction, Is.GreaterThanOrEqualTo(0.08f));
        }

        [Test]
        public void Synergy_keystone_adds_a_chain_hop()
        {
            var w = new SimWorld(82) { HeroBrainEnabled = false };
            w.Hero.Level = 40;
            w.ResetTree();
            int cap0 = w.EffectiveChainCap;
            GiveKey(w, Talents.Synergy, 1);
            Assert.That(w.EffectiveChainCap, Is.EqualTo(cap0 + 1));
        }

        [Test]
        public void Deathless_keystone_stands_back_up_once_a_minute()
        {
            var w = new SimWorld(83) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.Hero.Level = 60;
            GiveKey(w, Talents.Defense, 2);
            w.Hero.Stats.Hp = -5f;
            w.Tick();
            Assert.That(w.Deaths, Is.EqualTo(0));
            Assert.That(w.Hero.Stats.Hp, Is.GreaterThan(0f));
            w.Hero.Stats.Hp = -5f;
            w.Tick();
            Assert.That(w.Deaths, Is.EqualTo(1), "only once a minute");
        }

        [Test]
        public void Double_grand_resonance_rolls_on_to_the_next_element()
        {
            var w = new SimWorld(84) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.UseOpenFloor();
            w.Hero.Level = 60;
            GiveKey(w, Talents.Synergy, 2);
            w.SpawnEnemy(new EnemyDef { Id = "t", MaxHp = 1e6f }, w.Hero.Pos + new Vec2(1f, 0f));
            w.Hero.Gauge = SimWorld.GaugeMax;
            w.Tick();
            Assert.That(w.Events.Count(e => e.Type == SimEventType.GrandResonance), Is.EqualTo(2));
        }
    }
}
