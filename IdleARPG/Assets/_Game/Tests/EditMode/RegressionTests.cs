using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>Cases found in the M0 code review (2026-11-19).</summary>
    public class RegressionTests
    {
        static SimWorld EmptyWorld(bool enemiesThink = false)
        {
            var w = new SimWorld(11) { HeroBrainEnabled = false, EnemyBrainEnabled = enemiesThink };
            w.UseOpenFloor();
            return w;
        }

        static EnemyDef Def(float hp, float dmg = 0f) => new EnemyDef { Id = "test", MaxHp = hp, Damage = dmg, MoveSpeed = 0f, Xp = 0 };

        [Test]
        public void A_chain_started_on_a_living_giant_does_not_read_its_max_life()
        {
            // 녹은 심장 on a marked boss: the pop used to be worth half the boss's life and hit its neighbours (and the
            // boss through other pops) for it. Now it is worth the hero's strength.
            var w = EmptyWorld();
            var boss = w.SpawnEnemy(Def(1e8f), w.Hero.Pos + new Vec2(1f, 0f));
            var near = w.SpawnEnemy(Def(1e8f), w.Hero.Pos + new Vec2(1.6f, 0f));
            w.Hero.MoltenUntil = 1e9;
            boss.Mark = Element.Ember;
            w.ApplyHit(boss, 10f, Element.None, false, 0, 0, new SimWorld.HitCtx { Slot = -1, Direct = true });
            for (int i = 0; i < 5; i++) w.Tick();
            Assert.That(1e8f - near.Stats.Hp, Is.LessThan(1e4f));
        }

        [Test]
        public void Interleaved_cascades_never_hit_an_enemy_twice_from_the_same_cascade()
        {
            var w = EmptyWorld();
            var a = w.SpawnEnemy(Def(1f), new Vec2(50f, 50f));
            var b = w.SpawnEnemy(Def(1f), new Vec2(52.5f, 50f));
            var x = w.SpawnEnemy(Def(1000f), new Vec2(51.2f, 50f));
            var z = w.SpawnEnemy(Def(1f), new Vec2(51.2f, 51f)); // dies to A's pop, then pops in A's cascade next to X
            a.Mark = Element.Ember;
            b.Mark = Element.Ember;
            w.ApplyHit(a, 999f, Element.None); // cascade 1
            w.ApplyHit(b, 999f, Element.None); // cascade 2
            int hitsOnX = 0;
            for (int i = 0; i < 10; i++) { w.Tick(); hitsOnX += w.Events.Count(e => e.Type == SimEventType.Hit && e.TargetId == x.Id && e.FromChain); }
            Assert.That(z.Alive, Is.False);
            Assert.That(hitsOnX, Is.EqualTo(2), "once from each cascade, never twice from cascade 1");
        }

        [Test]
        public void Regen_cannot_revive_a_hero_knocked_below_zero()
        {
            var w = EmptyWorld(enemiesThink: true);
            // 1.5 after the melee guard (the default hero is melee).
            var e = w.SpawnEnemy(Def(1000f, dmg: 1.5f / (1f - HeroStats.MeleeGuardPct / 100f)), w.Hero.Pos + new Vec2(0.5f, 0f));
            e.Aggro = true;
            w.Hero.Stats.Hp = 1.4f; // 1.5 hit → -0.1; in-combat regen would add +0.2 the same tick
            w.Tick();
            Assert.That(w.Events.Any(ev => ev.Type == SimEventType.HeroDied), Is.True);
            Assert.That(w.Deaths, Is.EqualTo(1));
        }

        [Test]
        public void Death_on_a_later_zone_returns_to_that_zones_first_floor()
        {
            var w = new SimWorld(4);
            w.GoToFloor(6); // zone 2, third floor
            w.Hero.Stats.Hp = 0f;
            w.Tick();
            Assert.That(w.FloorIndex, Is.EqualTo(4));
        }

        [Test]
        public void Shatter_deals_one_and_a_half_and_consumes_the_mark()
        {
            var w = EmptyWorld();
            var e = w.SpawnEnemy(Def(1000f), new Vec2(50f, 50f));
            e.Mark = Element.Ember;
            w.ApplyHit(e, 10f, Element.Frost);
            Assert.That(e.Stats.Hp, Is.EqualTo(1000f - 15f).Within(1e-3f));
            Assert.That(e.Mark, Is.EqualTo(Element.None));
        }

        [Test]
        public void Overload_stuns_for_one_second()
        {
            var w = EmptyWorld(enemiesThink: true);
            var e = w.SpawnEnemy(new EnemyDef { Id = "test", MaxHp = 1000f, Damage = 0f, MoveSpeed = 2f }, w.Hero.Pos + new Vec2(3f, 0f));
            e.Aggro = true;
            e.Mark = Element.Frost;
            w.ApplyHit(e, 1f, Element.Shock);
            var before = e.Pos;
            for (int i = 0; i < 9; i++) w.Tick();
            Assert.That(e.Pos, Is.EqualTo(before), "stunned enemy must not move");
            for (int i = 0; i < 3; i++) w.Tick();
            Assert.That(e.Pos, Is.Not.EqualTo(before), "stun wears off after 1 s");
        }

        [Test]
        public void Stack_caps_at_five()
        {
            var w = EmptyWorld();
            var e = w.SpawnEnemy(Def(100000f), new Vec2(50f, 50f));
            for (int i = 0; i < 10; i++) w.ApplyHit(e, 1f, Element.Ember);
            Assert.That(e.Stacks, Is.EqualTo(5));
            Assert.That(e.Mark, Is.EqualTo(Element.Ember));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Soak_20000_ticks_stays_sane_and_keeps_progressing(int seed)
        {
            var w = new SimWorld(seed);
            int lastProgress = 0, progressEvents = 0;
            for (int t = 1; t <= 20000; t++)
            {
                w.Tick();
                if (w.Events.Any(e => e.Type == SimEventType.FloorCleared || e.Type == SimEventType.HeroDied)) { lastProgress = t; progressEvents++; }
                Assert.That(float.IsNaN(w.Hero.Pos.X) || float.IsNaN(w.Hero.Pos.Y), Is.False, $"tick {t}: NaN hero");
                Assert.That(w.Floor.IsWalkable(w.Hero.Pos), Is.True, $"tick {t}: hero off the floor at {w.Hero.Pos}");
                // Dense packs by design (8–14 rooms of 12–34): a floor holds a few hundred; this catches a leak, not density.
                Assert.That(w.Enemies.Count, Is.LessThanOrEqualTo(400), $"tick {t}: enemy list grew");
                Assert.That(t - lastProgress, Is.LessThan(12000), $"tick {t}: no floor clear or death for 20 minutes (stuck?)");
            }
            Assert.That(progressEvents, Is.GreaterThan(1));
        }
    }
}
