using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class CombatTests
    {
        [Test]
        public void Damage_formula_matches_gdd()
        {
            float d = Damage.Compute(100f, 0.5f, 1.2f, crit: true, critMult: 1.5f, vuln: 0.2f, resist: 0.25f);
            Assert.That(d, Is.EqualTo(100f * 1.5f * 1.2f * 1.5f * 1.2f * 0.75f).Within(1e-3f));
        }

        static SimWorld Arena()
        {
            var w = new SimWorld(21) { HeroBrainEnabled = false };
            w.UseOpenFloor();
            return w;
        }

        /// <summary>M2 Task 2: an archer in range with a clear line stands and shoots instead of closing in.</summary>
        [Test]
        public void Archer_in_range_holds_position_and_shoots()
        {
            var w = Arena();
            var archer = w.SpawnEnemy(EnemyRoster.Crypt[2], w.Hero.Pos + new Vec2(3.5f, 0f));
            archer.Aggro = true;
            var start = archer.Pos;
            float hp = w.Hero.Stats.Hp;
            int shots = 0;
            for (int i = 0; i < 40; i++) { w.Tick(); shots += w.Events.Count(e => e.Type == SimEventType.Shot); }
            Assert.That(archer.Pos, Is.EqualTo(start), "stays at range");
            Assert.That(shots, Is.GreaterThan(0));
            Assert.That(w.Hero.Stats.Hp, Is.LessThan(hp));
        }

        [Test]
        public void Archer_out_of_range_walks_closer()
        {
            var w = Arena();
            var archer = w.SpawnEnemy(EnemyRoster.Crypt[2], w.Hero.Pos + new Vec2(4.9f, 0f)); // inside aggro (5), outside range (4.5)
            archer.Aggro = true;
            float d0 = Vec2.Dist(archer.Pos, w.Hero.Pos);
            for (int i = 0; i < 10; i++) w.Tick();
            Assert.That(Vec2.Dist(archer.Pos, w.Hero.Pos), Is.LessThan(d0));
            Assert.That(Vec2.Dist(archer.Pos, w.Hero.Pos), Is.GreaterThan(4f), "stops once in range");
        }

        [Test]
        public void Hero_clears_first_floor_within_10_minutes_sim_time()
        {
            // GDD §6.3: act I (≈15 floors) takes ≈2.8 h, so ≈11 min per floor; the first floor should be faster.
            var world = new SimWorld(1);
            bool cleared = false;
            for (int i = 0; i < 6000 && !cleared; i++)
            {
                world.Tick();
                cleared = world.Events.Any(e => e.Type == SimEventType.FloorCleared);
            }
            Assert.That(cleared, Is.True, $"hero hp {world.Hero.Stats.Hp:0}, enemies left {world.Enemies.Count(e => e.Alive)}");
        }

        [Test]
        public void HeroDeathRespawnsAtCheckpoint()
        {
            var world = new SimWorld(2);
            world.GoToFloor(2);
            world.Hero.Stats.Hp = 0f;
            world.Tick();
            Assert.That(world.Events.Any(e => e.Type == SimEventType.HeroDied), Is.True);
            Assert.That(world.FloorIndex, Is.EqualTo(0), "checkpoint = first floor of the zone");
            Assert.That(world.Hero.Stats.Hp, Is.EqualTo(world.Hero.Stats.MaxHp));
            Assert.That(world.Hero.Alive, Is.True);
        }

        [Test]
        public void Enemies_chase_only_after_aggro_radius()
        {
            var world = new SimWorld(3) { HeroBrainEnabled = false };
            var far = world.Enemies.First(e => world.Floor.RoomAt(e.Pos)?.Kind == RoomKind.Boss);
            var start = far.Pos;
            for (int i = 0; i < 40; i++) world.Tick();
            Assert.That(far.Aggro, Is.False, "enemy far from the hero must not notice it");
            Assert.That(Vec2.Dist(far.Pos, start), Is.LessThan(2.5f), "it only strolls about where it stood");

            var room = world.Floor.RoomAt(far.Pos);
            var (cx, cy) = room.Rect.Center;
            var toCenter = new Vec2(cx + 0.5f, cy + 0.5f) - far.Pos;
            var dir = toCenter.Length < 1f ? new Vec2(1f, 0f) : toCenter.Normalized;
            world.Hero.Pos = far.Pos + dir * 3f; // stays inside the 11x11 boss room
            float before = Vec2.Dist(far.Pos, world.Hero.Pos);
            for (int i = 0; i < 5; i++) world.Tick();
            Assert.That(Vec2.Dist(far.Pos, world.Hero.Pos), Is.LessThan(before), "enemy within aggro radius must close in");
        }

        [Test]
        public void World_same_seed_same_hash_after_600_ticks()
        {
            var a = new SimWorld(77);
            var b = new SimWorld(77);
            for (int i = 0; i < 600; i++) { a.Tick(); b.Tick(); }
            Assert.That(a.StateHash(), Is.EqualTo(b.StateHash()));
        }
    }
}
