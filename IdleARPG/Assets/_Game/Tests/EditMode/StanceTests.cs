using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §5.2 태세.</summary>
    public class StanceTests
    {
        static float DamageDealt(Stance stance)
        {
            var w = new SimWorld(41) { EnemyBrainEnabled = false, Stance = stance };
            w.ClearEnemies();
            var e = w.SpawnEnemy(new EnemyDef { Id = "test", MaxHp = 1e7f, Damage = 0f, MoveSpeed = 0f }, w.Hero.Pos + new Vec2(0.6f, 0f));
            e.Aggro = true;
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
        public void Charge_hits_harder_and_careful_softer()
        {
            float bal = DamageDealt(Stance.Balanced);
            Assert.That(DamageDealt(Stance.Charge) / bal, Is.EqualTo(1.12f).Within(1e-3f));
            Assert.That(DamageDealt(Stance.Careful) / bal, Is.EqualTo(0.92f).Within(1e-3f));
        }

        [Test]
        public void Careful_hero_waits_to_recover_before_the_next_room_and_charge_does_not()
        {
            foreach (var stance in new[] { Stance.Careful, Stance.Charge })
            {
                var w = new SimWorld(42) { Stance = stance };
                w.ClearEnemies();
                w.Hero.Stats.Hp = w.Hero.Stats.MaxHp * 0.5f;
                var start = w.Hero.Pos;
                for (int i = 0; i < 20; i++) w.Tick();
                if (stance == Stance.Careful)
                {
                    Assert.That(w.Resting, Is.True);
                    Assert.That(Vec2.Dist(w.Hero.Pos, start), Is.LessThan(0.01f), "stays put while resting");
                }
                else Assert.That(Vec2.Dist(w.Hero.Pos, start), Is.GreaterThan(0.5f), "돌격 moves on");
            }
        }

        [Test]
        public void Stance_survives_a_save()
        {
            var w = new SimWorld(43) { Stance = Stance.Careful };
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            Assert.That(SimWorld.FromSave(d).Stance, Is.EqualTo(Stance.Careful));
        }
    }
}
