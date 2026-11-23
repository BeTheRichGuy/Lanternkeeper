using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>별점술사 (spec §9-2).</summary>
    public class StarTests
    {
        static EnemyDef Dummy(float hp = 1e6f) => new EnemyDef { Id = "test", MaxHp = hp, Damage = 0f, MoveSpeed = 0f, Xp = 0 };

        static SimWorld Seer(params string[] ring)
        {
            var w = new SimWorld(71, "starseer") { EnemyBrainEnabled = false };
            w.UseOpenFloor();
            w.Hero.Level = 30;
            var slots = new SkillDef[4];
            for (int i = 0; i < ring.Length; i++) slots[i] = SkillBook.Get(ring[i]);
            w.Hero.Ring = new SkillRing(SkillBook.OrbBasic, slots);
            return w;
        }

        [Test]
        public void Time_stop_holds_the_damage_and_lands_it_at_once()
        {
            var w = Seer();
            w.HeroBrainEnabled = false; // only the stored blow, no orbs on top
            var e = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(2f, 0f));
            e.FrozenUntil = w.Time + 1.0;
            e.StoredMul = 1.5f;
            w.ApplyHit(e, 100f, Element.None);
            Assert.That(e.Stats.Hp, Is.EqualTo(1e6f), "frozen: nothing lands yet");
            for (int i = 0; i < 12; i++) w.Tick();
            Assert.That(1e6f - e.Stats.Hp, Is.EqualTo(150f).Within(0.5f));
        }

        [Test]
        public void Constellation_hits_harder_the_more_stars_it_links()
        {
            var w = Seer("constellation");
            var stars = Enumerable.Range(0, 4).Select(i => w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(1.5f + i * 0.7f, (i % 2) * 1f))).ToList();
            foreach (var s in stars) { s.Mark = Element.Frost; s.Aggro = true; }
            var plain = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(-2f, 0f));
            plain.Aggro = true;
            bool cast = false;
            for (int i = 0; i < 30 && !cast; i++) { w.Tick(); cast = w.Events.Any(ev => ev.Type == SimEventType.Cast && ev.SkillId == "constellation"); }
            Assert.That(cast, Is.True);
            Assert.That(w.Events.Count(ev => ev.Type == SimEventType.Pull && ev.SkillId == "constellation"), Is.EqualTo(stars.Count - 1));
            Assert.That(stars.All(s => s.Stats.Hp < 1e6f), Is.True);
        }

        [Test]
        public void Blessing_makes_the_last_slot_call_the_first_again()
        {
            var w = Seer("star_bolt", "meteor");
            var e = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(3f, 0f));
            e.Aggro = true;
            w.Hero.BlessUntil = 100.0;
            bool looped = false;
            for (int i = 0; i < 60 && !looped; i++)
            {
                w.Tick();
                looped = w.Events.Any(ev => ev.Type == SimEventType.Cast && ev.SkillId == "meteor")
                         && w.Events.Any(ev => ev.Type == SimEventType.Cast && ev.SkillId == "star_bolt");
            }
            Assert.That(looped, Is.True, "meteor (last slot) brings star bolt (first slot) along in the same cast");
        }

        [Test]
        public void Rewind_pulls_the_other_slots_back()
        {
            var w = Seer("meteor", "rewind");
            var e = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(3f, 0f));
            e.Aggro = true;
            bool rewound = false;
            double before = 0;
            for (int i = 0; i < 40 && !rewound; i++)
            {
                before = w.Hero.Ring.CooldownLeft(0, w.Time);
                w.Tick();
                rewound = w.Events.Any(ev => ev.Type == SimEventType.Cast && ev.SkillId == "rewind");
            }
            Assert.That(rewound, Is.True);
            Assert.That(w.Hero.Ring.CooldownLeft(0, w.Time), Is.LessThan(before));
        }

        [Test]
        public void Every_starseer_skill_casts_without_errors()
        {
            foreach (var skill in SkillBook.Starseer)
            {
                var w = new SimWorld(7, "starseer") { EnemyBrainEnabled = false };
                w.UseOpenFloor();
                for (int i = 0; i < 6; i++)
                {
                    var e = w.SpawnEnemy(Dummy(), w.Hero.Pos + new Vec2(1.5f + i * 0.4f, (i % 2) * 0.5f));
                    e.Aggro = true;
                    e.Mark = Element.Frost;
                }
                w.Hero.Level = 30;
                var slots = new SkillDef[4];
                if (skill.Role == SkillRole.Ultimate) w.Hero.Ring = new SkillRing(SkillBook.OrbBasic, slots) { Ultimate = skill };
                else { slots[0] = skill; w.Hero.Ring = new SkillRing(SkillBook.OrbBasic, slots); }
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
        public void All_four_classes_are_playable_and_survive_a_save()
        {
            Assert.That(SkillBook.PlayableClasses, Is.EquivalentTo(new[] { "ashknight", "oathknight", "windhunter", "starseer" }));
            var a = new SimWorld(9, "starseer");
            for (int i = 0; i < 600; i++) a.Tick();
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(a.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.Hero.ClassId, Is.EqualTo("starseer"));
            Assert.That(b.Hero.Ring.Basic.Id, Is.EqualTo("star_orb"));
        }
    }
}
