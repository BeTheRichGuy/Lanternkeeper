using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §5.3 등불 점화.</summary>
    public class LanternTests
    {
        static float DamageDealt(bool ignite)
        {
            var w = new SimWorld(31) { EnemyBrainEnabled = false, AutoEquip = false }; // the hidden cache must not change the gear
            w.ClearEnemies();
            var e = w.SpawnEnemy(new EnemyDef { Id = "test", MaxHp = 1e7f, Damage = 0f, MoveSpeed = 0f }, w.Hero.Pos + new Vec2(0.6f, 0f));
            e.Aggro = true;
            if (ignite) w.Ignite();
            // Sum the hits: 1e7 − life in float loses the small hits (its step is 1).
            float dealt = 0f;
            for (int i = 0; i < 150; i++)
            {
                w.Tick();
                foreach (var ev in w.Events) if (ev.Type == SimEventType.Hit && ev.TargetId == e.Id) dealt += ev.Value;
            }
            return dealt;
        }

        [Test]
        public void Ignition_hits_fifteen_percent_harder()
        {
            Assert.That(DamageDealt(true) / DamageDealt(false), Is.EqualTo(1f + SimWorld.IgniteDamage).Within(1e-3f));
        }

        [Test]
        public void The_first_kindling_on_a_floor_finds_a_hidden_cache()
        {
            var w = new SimWorld(33) { HeroBrainEnabled = false, EnemyBrainEnabled = false, AutoEquip = false };
            w.Ignite();
            w.Tick();
            Assert.That(w.Events.Count(e => e.Type == SimEventType.Drop && e.Item.Rarity >= Rarity.Rare), Is.EqualTo(2));
            Assert.That(w.Events.Exists(e => e.Type == SimEventType.ChestOpened && e.SkillId == "hidden_cache"), Is.True);
        }

        [Test]
        public void Ignition_lasts_thirty_seconds_and_recharges_in_three_minutes()
        {
            var w = new SimWorld(32) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            Assert.That(w.Ignite(), Is.True);
            w.Tick();
            Assert.That(w.Events.Exists(ev => ev.Type == SimEventType.Ignited), Is.True);
            Assert.That(w.Ignited, Is.True);
            Assert.That(w.Ignite(), Is.False, "recharging");
            for (int i = 0; i < 300; i++) w.Tick();
            Assert.That(w.Ignited, Is.False);
            for (int i = 0; i < 1500; i++) w.Tick();
            Assert.That(w.Ignite(), Is.True);
        }
    }
}
