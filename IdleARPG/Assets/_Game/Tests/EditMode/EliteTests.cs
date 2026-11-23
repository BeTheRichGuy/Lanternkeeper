using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §5.4 elite prefixes.</summary>
    public class EliteTests
    {
        static SimWorld Arena(bool enemiesThink = false)
        {
            var w = new SimWorld(101) { HeroBrainEnabled = false, EnemyBrainEnabled = enemiesThink };
            w.UseOpenFloor();
            return w;
        }

        static Enemy Elite(SimWorld w, EliteAffix affixes, Vec2 at, float hp = 1000f, float dmg = 0f)
        {
            var e = w.SpawnEnemy(new EnemyDef { Id = "brute", MaxHp = hp, Damage = dmg, MoveSpeed = 0f, AttackSpeed = 10f }, at);
            e.Elite = true;
            e.Affixes = affixes;
            if ((affixes & EliteAffix.Shielded) != 0) e.AffixShield = hp * 0.3f;
            return e;
        }

        [Test]
        public void Shielded_elite_soaks_the_first_blows()
        {
            var w = Arena();
            var e = Elite(w, EliteAffix.Shielded, w.Hero.Pos + new Vec2(2f, 0f));
            w.ApplyHit(e, 200f, Element.None);
            Assert.That(e.Stats.Hp, Is.EqualTo(1000f - (200f - 300f > 0 ? 0 : 0)).Within(1f), "300 shield takes all 200");
            w.ApplyHit(e, 200f, Element.None);
            Assert.That(e.Stats.Hp, Is.EqualTo(900f).Within(1f), "100 left on the shield, 100 through");
        }

        [Test]
        public void Multiplying_elite_splits_in_two_once()
        {
            var w = Arena();
            var e = Elite(w, EliteAffix.Multiplying, w.Hero.Pos + new Vec2(2f, 0f));
            w.ApplyHit(e, 1e6f, Element.None);
            w.Tick();
            var copies = w.Enemies.Where(x => x.Alive && x.Copy).ToList();
            Assert.That(copies.Count, Is.EqualTo(2));
            foreach (var c in copies) w.ApplyHit(c, 1e6f, Element.None);
            w.Tick();
            Assert.That(w.Enemies.Count(x => x.Alive), Is.EqualTo(0), "copies never split again");
        }

        [Test]
        public void Frost_burst_freezes_a_hero_standing_close()
        {
            var w = Arena();
            w.HeroBrainEnabled = true;
            var e = Elite(w, EliteAffix.FrostBurst, w.Hero.Pos + new Vec2(1f, 0f), hp: 1f);
            w.ApplyHit(e, 10f, Element.None);
            Assert.That(w.Hero.StunnedUntil, Is.GreaterThan(w.Time));
        }

        [Test]
        public void Vampiric_elite_heals_when_it_hits()
        {
            var w = Arena(enemiesThink: true);
            var e = Elite(w, EliteAffix.Vampiric, w.Hero.Pos + new Vec2(0.6f, 0f), hp: 1000f, dmg: 20f);
            e.Aggro = true;
            e.Stats.Hp = 500f;
            w.Tick();
            Assert.That(e.Stats.Hp, Is.GreaterThan(500f));
        }

        [Test]
        public void Ember_aura_burns_the_hero_nearby()
        {
            var w = Arena();
            var e = Elite(w, EliteAffix.EmberAura, w.Hero.Pos + new Vec2(1f, 0f), dmg: 50f);
            e.Aggro = true;
            float hp = w.Hero.Stats.Hp;
            for (int i = 0; i < 12; i++) w.Tick();
            Assert.That(w.Hero.Stats.Hp, Is.LessThan(hp));
        }

        [Test]
        public void Deeper_zones_roll_more_prefixes()
        {
            int shallow = 0, deep = 0;
            foreach (int seed in Enumerable.Range(0, 30))
            {
                var a = new SimWorld(seed);
                shallow += a.Enemies.Where(x => x.Elite && !x.Boss).Sum(x => System.Linq.Enumerable.Count(EliteAffixes.All, f => (x.Affixes & f) != 0));
                a.GoToFloor(9);
                deep += a.Enemies.Where(x => x.Elite && !x.Boss).Sum(x => System.Linq.Enumerable.Count(EliteAffixes.All, f => (x.Affixes & f) != 0));
            }
            Assert.That(shallow, Is.GreaterThan(0));
            Assert.That(deep, Is.GreaterThan(shallow));
        }
    }
}
