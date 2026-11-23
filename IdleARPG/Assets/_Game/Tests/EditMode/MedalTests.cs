using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §12.5 적 도감 메달.</summary>
    public class MedalTests
    {
        static EnemyDef Def(string id) => new EnemyDef { Id = id, Name = id, MaxHp = 1f, Damage = 0f, MoveSpeed = 0f, Xp = 0 };

        [Test]
        public void The_hundredth_kill_of_a_kind_earns_bronze_and_one_percent()
        {
            var w = new SimWorld(71) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.UseOpenFloor();
            var def = Def("medal_test");
            bool medal = false;
            for (int i = 0; i < 100; i++)
            {
                var e = w.SpawnEnemy(def, new Vec2(50f, 50f));
                w.Events.Clear();
                w.ApplyHit(e, 10f, Element.None);
                medal |= w.Events.Any(ev => ev.Type == SimEventType.Medal);
            }
            Assert.That(medal, Is.True);
            Assert.That(w.KillsOf("medal_test"), Is.EqualTo(100));

            var tough = w.SpawnEnemy(new EnemyDef { Id = "medal_test", MaxHp = 1000f }, new Vec2(60f, 50f));
            w.ApplyHit(tough, 100f, Element.None);
            Assert.That(1000f - tough.Stats.Hp, Is.EqualTo(101f).Within(1e-3f));
        }

        [Test]
        public void Kills_by_kind_survive_a_save()
        {
            var w = new SimWorld(72);
            w.Hero.KillsById["skeleton"] = 1234;
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            Assert.That(SimWorld.FromSave(d).KillsOf("skeleton"), Is.EqualTo(1234));
        }

        [Test]
        public void Codex_first_holds_the_floor_until_every_kind_here_has_bronze()
        {
            var w = new SimWorld(73) { CodexFirst = true };
            w.GoToFloor(1); // zone 1, floor 2: villagers and grave hounds
            Assert.That(w.FloorCodexDone(), Is.False);
            foreach (int r in w.ActNow.PatternFor(w.Zone, w.FloorInZone)) w.Hero.KillsById[w.ActNow.Ranks[r].Id] = 100;
            Assert.That(w.FloorCodexDone(), Is.True);
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            Assert.That(SimWorld.FromSave(d).CodexFirst, Is.True);
        }

        [Test]
        public void Top_loop_sends_the_last_floor_of_a_zone_back_to_its_start()
        {
            var w = new SimWorld(74) { TopLoop = true, EnemyBrainEnabled = false };
            w.GoToFloor(SimWorld.FloorsPerZone - 1); // last floor of zone 1
            w.ClearEnemies();                         // an empty floor: the hero walks it to the end
            for (int i = 0; i < 6000 && w.FloorIndex == SimWorld.FloorsPerZone - 1; i++) w.Tick();
            Assert.That(w.FloorIndex, Is.EqualTo(0));
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            Assert.That(SimWorld.FromSave(d).TopLoop, Is.True);
        }

        [Test]
        public void Every_enemy_has_a_line_of_who_it_was()
        {
            foreach (var act in EnemyRoster.Acts)
                foreach (var d in act.Ranks.Concat(new[] { act.Elite, act.Boss }))
                    Assert.That(d.Lore, Is.Not.Empty, d.Id);
        }
    }
}
