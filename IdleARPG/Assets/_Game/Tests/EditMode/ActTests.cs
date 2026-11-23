using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>Act I structure (M2 Task 1, GDD §6.1, §6.3).</summary>
    public class ActTests
    {
        [Test]
        public void Floor_12_is_the_act_boss_floor_with_the_sarcophagus_king()
        {
            var w = new SimWorld(81);
            w.GoToFloor(12);
            Assert.That(w.IsActBossFloor, Is.True);
            Assert.That(w.Floor.Rooms.Count, Is.EqualTo(2));
            var boss = w.Enemies.Single(e => e.Boss);
            Assert.That(boss.Def.Id, Is.EqualTo("sarcophagus_king"));
        }

        [Test]
        public void A_second_death_in_the_same_zone_soon_steps_back_a_zone()
        {
            var w = new SimWorld(88) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.GoToFloor(9); // zone 3, floor 2
            w.Hero.Stats.Hp = 0f; w.Tick();
            Assert.That(w.FloorIndex, Is.EqualTo(8), "first death: zone start");
            w.Hero.Stats.Hp = 0f; w.Tick();
            Assert.That(w.FloorIndex, Is.EqualTo(4), "second death soon after: the zone before");
        }

        // 희승: 모래 → 수풀 → 핏덩이 → 얼음 → 용암 → 심연, each act its own world and deeper is darker.
        [TestCase(0, "I", "Sand", "bronze_veiled_general", "sarcophagus_king")]
        [TestCase(1, "II", "Jungle", "antler_knight", "crowned_great_tree")]
        [TestCase(2, "III", "Flesh", "fleshmail_warden", "pulse_lord")]
        [TestCase(3, "IV", "Ice", "frost_lord_knight", "giant_in_ice")]
        [TestCase(4, "V", "Forge", "guild_enforcer", "molten_giant")]
        [TestCase(5, "VI", "Abyss", "ash_herald", "dead_star_apostle")]
        public void Each_act_has_its_own_monsters_elite_and_boss(int act, string numeral, string roster, string elite, string boss)
        {
            var ranks = ((EnemyDef[])typeof(EnemyRoster).GetField(roster).GetValue(null)).Select(d => d.Id).ToList();
            var w = new SimWorld(86 + act);
            w.GoToFloor(SimWorld.ActLength * act + 1);
            Assert.That(w.ActNow.Numeral, Is.EqualTo(numeral));
            Assert.That(w.Enemies.Where(e => !e.Boss && !e.Elite).All(e => ranks.Contains(e.Def.Id)), Is.True);
            Assert.That(w.Enemies.Single(e => e.Boss).Def.Id, Is.EqualTo(elite));
            w.GoToFloor(SimWorld.ActLength * act + SimWorld.ActBossFloorInAct);
            Assert.That(w.Enemies.Single(e => e.Boss).Def.Id, Is.EqualTo(boss));
            Assert.That(w.AreaLevel, Is.GreaterThanOrEqualTo(9 + act * 10));
        }

        [Test]
        public void Six_acts_with_distinct_rosters()
        {
            Assert.That(EnemyRoster.Acts.Length, Is.EqualTo(6));
            var ids = EnemyRoster.Acts.SelectMany(a => a.Ranks.Select(r => r.Id).Concat(new[] { a.Elite.Id, a.Boss.Id })).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), "no monster is shared between acts");
            Assert.That(EnemyRoster.Acts.All(a => a.Boss.Kit != "" && a.Boss.SlamName != "" && a.Boss.SpecialName != ""), Is.True);
        }

        [TestCase(0)]
        [TestCase(5)]
        [TestCase(11)]
        public void Other_floors_end_on_the_act_elite(int floor)
        {
            var w = new SimWorld(82);
            w.GoToFloor(floor);
            var boss = w.Enemies.Single(e => e.Boss);
            Assert.That(boss.Def.Id, Is.EqualTo("bronze_veiled_general"));
        }

        [TestCase(0, 1)]
        [TestCase(4, 4)]
        [TestCase(12, 10)]
        [TestCase(13, 11)]
        public void Area_level_spans_1_to_10_across_act_one(int floor, int level)
        {
            var w = new SimWorld(83);
            w.GoToFloor(floor);
            Assert.That(w.AreaLevel, Is.EqualTo(level));
        }

        [TestCase(2, 0)]
        [TestCase(6, 4)]
        [TestCase(11, 8)]
        [TestCase(12, 8)]   // the act boss sends you back to farm zone 3 (no death loop on the boss floor)
        [TestCase(15, 13)]
        public void Death_returns_to_the_zone_checkpoint(int floor, int checkpoint)
        {
            var w = new SimWorld(84);
            w.GoToFloor(floor);
            w.Hero.Stats.Hp = 0f;
            w.Tick();
            Assert.That(w.FloorIndex, Is.EqualTo(checkpoint));
        }

        [Test]
        public void Entering_a_zone_announces_it()
        {
            var w = new SimWorld(85);
            w.Events.Clear();
            w.GoToFloor(4);
            Assert.That(w.Events.Any(e => e.Type == SimEventType.ZoneEntered && (int)e.Value == 1), Is.True);
        }

        [Test]
        public void Zone_rosters_bring_in_stronger_ranks()
        {
            Assert.That(EnemyRoster.PatternFor(0, 0), Is.EqualTo(new[] { 0 }));
            Assert.That(EnemyRoster.PatternFor(2, 1), Does.Contain(4), "안개 망령 in zone 3");
            Assert.That(EnemyRoster.PatternFor(0, 0).Max(), Is.LessThan(EnemyRoster.PatternFor(2, 3).Max()));
        }
    }
}
