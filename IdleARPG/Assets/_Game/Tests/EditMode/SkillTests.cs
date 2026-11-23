using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class SkillTests
    {
        static SkillDef S(string id, float cd) => new SkillDef { Id = id, Cooldown = cd, Power = 1f };

        static SkillRing Ring() =>
            new SkillRing(S("basic", 0.5f), new[] { S("a", 2f), S("b", 2f), S("c", 2f), S("d", 2f) });

        [Test]
        public void Ring_casts_in_clockwise_order_when_all_ready()
        {
            var ring = Ring();
            string order = "";
            for (int i = 0; i < 4; i++)
            {
                var s = ring.NextReady(0f);
                ring.MarkUsed(s, 0f);
                order += s.Id;
            }
            Assert.That(order, Is.EqualTo("abcd"));
        }

        [Test]
        public void Ring_skips_cooling_slot()
        {
            var ring = Ring();
            ring.MarkUsed(ring.Slots[0], 0f); // "a" cooling until t=2
            Assert.That(ring.NextReady(1f).Id, Is.EqualTo("b"));
        }

        [Test]
        public void Falls_back_to_basic_when_none_ready()
        {
            var ring = Ring();
            foreach (var s in ring.Slots) ring.MarkUsed(s, 0f);
            Assert.That(ring.NextReady(1f).Id, Is.EqualTo("basic"));
        }

        // Spec §2: the cycle 잿불 → 서리 → 전격 → 부패 → 공허 → 잿불; the next element detonates a mark.
        [TestCase(Element.Ember, Element.Frost, Resonance.Shatter)]
        [TestCase(Element.Frost, Element.Shock, Resonance.Overload)]
        [TestCase(Element.Shock, Element.Rot, Resonance.Plague)]
        [TestCase(Element.Rot, Element.Void, Resonance.Devour)]
        [TestCase(Element.Void, Element.Ember, Resonance.Collapse)]
        [TestCase(Element.Frost, Element.Ember, Resonance.Spark)]   // backwards: only a spark
        [TestCase(Element.Ember, Element.Shock, Resonance.Spark)]   // not neighbours: a spark
        [TestCase(Element.Ember, Element.Ember, Resonance.Stack)]
        [TestCase(Element.None, Element.Ember, Resonance.None)]
        [TestCase(Element.Ember, Element.None, Resonance.None)]
        public void Resonance_table_matches_gdd(Element mark, Element hit, Resonance expected)
        {
            Assert.That(Elements.Resolve(mark, hit), Is.EqualTo(expected));
        }

        static SimWorld EmptyWorld()
        {
            var w = new SimWorld(11) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.UseOpenFloor();
            return w;
        }

        static EnemyDef Weak(float hp) => new EnemyDef { Id = "test", MaxHp = hp, Damage = 0f, MoveSpeed = 0f, Xp = 0 };

        [Test]
        public void Marked_enemy_death_spreads_mark_to_neighbors()
        {
            var w = EmptyWorld();
            var a = w.SpawnEnemy(Weak(1f), new Vec2(50f, 50f));
            var b = w.SpawnEnemy(Weak(1000f), new Vec2(51f, 50f));
            a.Mark = Element.Ember;
            w.ApplyHit(a, 999f, Element.None);
            for (int i = 0; i < 3; i++) w.Tick();
            Assert.That(a.Alive, Is.False);
            Assert.That(b.Mark, Is.EqualTo(Element.Ember));
        }

        [Test]
        public void ChainStopsAtCap()
        {
            var w = EmptyWorld();
            var line = Enumerable.Range(0, 20).Select(i => w.SpawnEnemy(Weak(1f), new Vec2(50f + i * 1.5f, 50f))).ToList();
            line[0].Mark = Element.Ember;
            w.ApplyHit(line[0], 999f, Element.None);
            int pops = 0;
            for (int i = 0; i < 30; i++) { w.Tick(); pops += w.Events.Count(e => e.Type == SimEventType.ChainPop); }
            Assert.That(pops, Is.EqualTo(SimWorld.ChainCap));
            Assert.That(line.Count(e => !e.Alive), Is.EqualTo(SimWorld.ChainCap + 1));
        }

        [Test]
        public void Chain_never_hits_same_enemy_twice_in_one_cascade()
        {
            var w = EmptyWorld();
            var a = w.SpawnEnemy(Weak(1f), new Vec2(50f, 50f));
            var b = w.SpawnEnemy(Weak(1f), new Vec2(51f, 50f));
            var c = w.SpawnEnemy(Weak(1000f), new Vec2(51.5f, 50.5f));
            a.Mark = Element.Ember;
            w.ApplyHit(a, 999f, Element.None);
            int hitsOnC = 0;
            for (int i = 0; i < 10; i++) { w.Tick(); hitsOnC += w.Events.Count(e => e.Type == SimEventType.Hit && e.TargetId == c.Id && e.FromChain); }
            Assert.That(b.Alive, Is.False);
            Assert.That(hitsOnC, Is.EqualTo(1));
        }
    }
}
