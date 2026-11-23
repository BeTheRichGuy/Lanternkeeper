using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>서약기사 (spec §9, M2 Task 6).</summary>
    public class OathTests
    {
        static EnemyDef Hitter(float dmg) => new EnemyDef { Id = "test", MaxHp = 1e6f, Damage = dmg, MoveSpeed = 0f, AttackSpeed = 10f, Xp = 0 };

        static SimWorld Knight(bool enemiesThink = true)
        {
            var w = new SimWorld(51, "oathknight") { HeroBrainEnabled = false, EnemyBrainEnabled = enemiesThink };
            w.UseOpenFloor();
            return w;
        }

        [Test]
        public void Oathknight_gets_its_own_ring()
        {
            var w = new SimWorld(1, "oathknight");
            Assert.That(w.Hero.Ring.Slots[0].Id, Is.EqualTo("blessed_hammer"));
            var r8 = SkillBook.BuildRing("oathknight", 0, 8, null);
            Assert.That(r8.Slots.Select(s => s.Id), Is.EqualTo(new[] { "blessed_hammer", "hallowed_aura", "heaven_spears", "judgment" }));
        }

        [Test]
        public void Barrier_soaks_blows_then_breaks_into_a_holy_blast()
        {
            var w = Knight();
            var e = w.SpawnEnemy(Hitter(30f), w.Hero.Pos + new Vec2(0.6f, 0f));
            e.Aggro = true;
            w.Hero.Shield = 50f;
            w.Hero.ShieldUntil = 100.0;
            w.Hero.ShieldPower = 1.5f;
            w.Hero.ShieldRadius = 2.5f;
            float hp = w.Hero.Stats.Hp;
            w.Tick();
            Assert.That(w.Hero.Stats.Hp, Is.EqualTo(hp).Within(1e-3f), "first blow fully absorbed");
            bool broke = false;
            for (int i = 0; i < 20 && !broke; i++) { w.Tick(); broke = w.Events.Any(ev => ev.Type == SimEventType.Cast && ev.SkillId == "oath_barrier_break"); }
            Assert.That(broke, Is.True);
        }

        [Test]
        public void Vow_keeps_the_knight_standing_and_pays_it_back()
        {
            var w = Knight();
            var e = w.SpawnEnemy(Hitter(500f), w.Hero.Pos + new Vec2(0.6f, 0f));
            e.Aggro = true;
            w.Hero.VowActive = true;
            w.Hero.VowUntil = w.Time + 2.0;
            w.Hero.VowRadius = 3f;
            for (int i = 0; i < 15; i++) w.Tick();
            Assert.That(w.Deaths, Is.EqualTo(0));
            Assert.That(w.Hero.Stats.Hp, Is.GreaterThanOrEqualTo(1f));
            bool burst = false;
            for (int i = 0; i < 15 && !burst; i++) { w.Tick(); burst = w.Events.Any(ev => ev.Type == SimEventType.Cast && ev.SkillId == "unbroken_vow_burst"); }
            Assert.That(burst, Is.True);
        }

        [Test]
        public void Martyr_chains_pass_part_of_the_blow_to_the_bound()
        {
            var w = Knight();
            var hitter = w.SpawnEnemy(Hitter(100f), w.Hero.Pos + new Vec2(0.6f, 0f));
            hitter.Aggro = true;
            var bound = w.SpawnEnemy(Hitter(0f), w.Hero.Pos + new Vec2(-1.5f, 0f));
            bound.Link = 99;
            w.Hero.MartyrLink = 99;
            w.Hero.MartyrPct = 0.3f;
            w.Hero.MartyrUntil = 100.0;
            float hp = w.Hero.Stats.Hp;
            w.Tick();
            float taken = hp - w.Hero.Stats.Hp;
            Assert.That(taken, Is.LessThan(100f * 0.75f), "30% passed on");
            Assert.That(bound.Stats.Hp, Is.LessThan(1e6f));
        }

        [Test]
        public void Judgment_bell_sets_off_every_mark_at_once()
        {
            var w = Knight(enemiesThink: false);
            var marks = new[] { Element.Ember, Element.Frost, Element.Shock, Element.Rot, Element.Void };
            for (int i = 0; i < marks.Length; i++)
            {
                var e = w.SpawnEnemy(Hitter(0f), w.Hero.Pos + new Vec2(1f + i * 0.5f, 0.3f));
                e.Mark = marks[i];
                e.Aggro = true;
            }
            var slots = new SkillDef[4];
            slots[0] = SkillBook.Get("bell_of_judgment");
            w.Hero.Ring = new SkillRing(SkillBook.Basic, slots);
            w.HeroBrainEnabled = true;
            bool rang = false;
            int forward = 0;
            for (int i = 0; i < 20 && !rang; i++)
            {
                w.Tick();
                rang = w.Events.Any(ev => ev.Type == SimEventType.Cast && ev.SkillId == "bell_of_judgment");
                if (rang) forward = w.Events.Count(ev => ev.Type == SimEventType.Resonance && Elements.IsForward(ev.Res));
            }
            Assert.That(rang, Is.True);
            Assert.That(forward, Is.GreaterThanOrEqualTo(5), "one forward resonance per mark, whatever its element");
        }

        [Test]
        public void Rallying_cry_pulls_the_pack_in_and_braces()
        {
            var w = Knight(enemiesThink: false);
            var e = w.SpawnEnemy(Hitter(0f), w.Hero.Pos + new Vec2(2.5f, 0f));
            e.Aggro = true;
            var slots = new SkillDef[4];
            slots[0] = SkillBook.Get("rallying_cry");
            w.Hero.Ring = new SkillRing(SkillBook.Basic, slots);
            w.HeroBrainEnabled = true;
            float d0 = Vec2.Dist(e.Pos, w.Hero.Pos);
            for (int i = 0; i < 20 && w.Hero.GuardUntil <= 0.0; i++) w.Tick();
            Assert.That(w.Hero.GuardUntil, Is.GreaterThan(0.0));
            Assert.That(Vec2.Dist(e.Pos, w.Hero.Pos), Is.LessThan(d0));
        }

        [Test]
        public void Every_oathknight_skill_casts_without_errors()
        {
            foreach (var skill in SkillBook.Oathknight)
            {
                var w = new SimWorld(7, "oathknight") { EnemyBrainEnabled = false };
                w.UseOpenFloor();
                for (int i = 0; i < 6; i++) w.SpawnEnemy(Hitter(0f), w.Hero.Pos + new Vec2(1f + i * 0.4f, (i % 2) * 0.5f)).Aggro = true;
                w.Hero.Level = 30;
                var slots = new SkillDef[4];
                if (skill.Role == SkillRole.Ultimate) w.Hero.Ring = new SkillRing(SkillBook.Basic, slots) { Ultimate = skill };
                else { slots[0] = skill; w.Hero.Ring = new SkillRing(SkillBook.Basic, slots); }
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
        public void Oathknight_save_keeps_the_class()
        {
            var a = new SimWorld(9, "oathknight");
            for (int i = 0; i < 600; i++) a.Tick();
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(a.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.Hero.ClassId, Is.EqualTo("oathknight"));
            Assert.That(b.Hero.Ring.Slots[0].Id, Is.EqualTo(a.Hero.Ring.Slots[0].Id)); // an auto-picked 증강 card may have turned the set
        }
    }
}
