using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>바람사냥꾼 (spec §9-1).</summary>
    public class WindTests
    {
        static EnemyDef Dummy(float hp = 1e6f) => new EnemyDef { Id = "test", MaxHp = hp, Damage = 0f, MoveSpeed = 0f, Xp = 0 };

        static SimWorld Hunter(params string[] ring)
        {
            var w = new SimWorld(61, "windhunter") { EnemyBrainEnabled = false };
            w.UseOpenFloor();
            w.Hero.Level = 30;
            var slots = new SkillDef[4];
            for (int i = 0; i < ring.Length; i++) slots[i] = SkillBook.Get(ring[i]);
            w.Hero.Ring = new SkillRing(SkillBook.BowBasic, slots);
            return w;
        }

        [Test]
        public void Hunter_shoots_from_range_with_the_bow()
        {
            var w = Hunter();
            var e = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(3.8f, 0f));
            e.Aggro = true;
            var start = w.Hero.Pos;
            bool shot = false;
            for (int i = 0; i < 20 && !shot; i++) { w.Tick(); shot = w.Events.Any(ev => ev.Type == SimEventType.Shot && ev.SourceId == w.Hero.Id); }
            Assert.That(shot, Is.True);
            Assert.That(Vec2.Dist(w.Hero.Pos, start), Is.LessThan(0.5f), "no need to walk up to it");
        }

        [Test]
        public void Hunter_steps_back_from_a_boss_then_shoots_again()
        {
            var w = Hunter();
            var boss = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(1.2f, 0f));
            boss.Boss = true;
            boss.Aggro = true;
            bool shotAfter = false;
            for (int i = 0; i < 60 && !shotAfter; i++)
            {
                w.Tick();
                shotAfter = Vec2.Dist(w.Hero.Pos, boss.Pos) >= SimWorld.KiteFar - 0.1f
                    && w.Events.Any(ev => ev.Type == SimEventType.Shot && ev.SourceId == w.Hero.Id);
            }
            Assert.That(shotAfter, Is.True, "backs off to kiting distance, then shoots");
        }

        [Test]
        public void Melee_hero_does_not_kite_a_boss()
        {
            var w = new SimWorld(61, "ashknight") { EnemyBrainEnabled = false };
            w.UseOpenFloor();
            var boss = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(1.2f, 0f));
            boss.Boss = true;
            boss.Aggro = true;
            for (int i = 0; i < 20; i++) w.Tick();
            Assert.That(Vec2.Dist(w.Hero.Pos, boss.Pos), Is.LessThan(SimWorld.KiteNear));
        }

        [Test]
        public void Roll_shot_backs_away_from_the_target()
        {
            var w = Hunter("roll_shot");
            var e = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(1.5f, 0f));
            e.Aggro = true;
            float d0 = Vec2.Dist(w.Hero.Pos, e.Pos);
            bool rolled = false;
            for (int i = 0; i < 20 && !rolled; i++) { w.Tick(); rolled = w.Events.Any(ev => ev.Type == SimEventType.Leap && ev.SkillId == "roll_shot"); }
            Assert.That(rolled, Is.True);
            Assert.That(Vec2.Dist(w.Hero.Pos, e.Pos), Is.GreaterThan(d0 + 1.5f));
        }

        [Test]
        public void Trap_starts_the_death_chain_of_a_marked_enemy_without_killing_it()
        {
            var w = Hunter("shock_trap");
            var e = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(3f, 0f));
            e.Mark = Element.Ember;
            e.Aggro = true;
            bool popped = false;
            for (int i = 0; i < 40 && !popped; i++) { w.Tick(); popped = w.Events.Any(ev => ev.Type == SimEventType.ChainPop); }
            Assert.That(popped, Is.True);
            Assert.That(e.Alive, Is.True);
        }

        [Test]
        public void Hunted_enemies_take_more_damage()
        {
            var w = Hunter();
            var a = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(2f, 0f));
            var b = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(-2f, 0f));
            a.HuntedUntil = 100.0;
            a.HuntedPct = 0.3f;
            w.ApplyHit(a, 100f, Element.None);
            w.ApplyHit(b, 100f, Element.None);
            Assert.That(1e6f - a.Stats.Hp, Is.EqualTo(130f).Within(1e-2f));
            Assert.That(1e6f - b.Stats.Hp, Is.EqualTo(100f).Within(1e-2f));
        }

        [Test]
        public void Focus_refills_over_time()
        {
            var w = Hunter();
            w.Hero.Heat = 0f;
            for (int i = 0; i < 50; i++) w.Tick(); // 5 s, no casts
            Assert.That(w.Hero.Heat, Is.EqualTo(30f).Within(1f));
        }

        [Test]
        public void Every_windhunter_skill_casts_without_errors()
        {
            foreach (var skill in SkillBook.Windhunter)
            {
                var w = new SimWorld(7, "windhunter") { EnemyBrainEnabled = false };
                w.UseOpenFloor();
                for (int i = 0; i < 6; i++) w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(1.5f + i * 0.4f, (i % 2) * 0.5f)).Aggro = true;
                w.Hero.Level = 30;
                var slots = new SkillDef[4];
                if (skill.Role == SkillRole.Ultimate) w.Hero.Ring = new SkillRing(SkillBook.BowBasic, slots) { Ultimate = skill };
                else { slots[0] = skill; w.Hero.Ring = new SkillRing(SkillBook.BowBasic, slots); }
                bool cast = false;
                for (int i = 0; i < 80; i++)
                {
                    w.Tick();
                    cast |= w.Events.Any(ev => ev.Type == SimEventType.Cast && ev.SkillId == skill.Id);
                }
                Assert.That(cast, Is.True, skill.Id);
            }
        }

        [Test]
        public void Windhunter_save_keeps_the_class_and_bow()
        {
            var a = new SimWorld(9, "windhunter");
            for (int i = 0; i < 600; i++) a.Tick();
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(a.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.Hero.ClassId, Is.EqualTo("windhunter"));
            Assert.That(b.Hero.Ring.Basic.Id, Is.EqualTo("bow_shot"));
        }
    }
}
