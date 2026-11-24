using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>11-24 F2: five element damage lines (부패·공허 added) and drops that favour the class's own elements.</summary>
    public class ElementAffixTests
    {
        static Item Amulet(params (Stat s, float v)[] affixes)
        {
            var it = new Item { Uid = 500, BaseId = "lantern_pendant", Rarity = Rarity.Rare, Ilvl = 6, ReqLevel = 1, Name = "test" };
            it.Implicit = new AffixRoll { Stat = ItemData.Base("lantern_pendant").ImplicitStat, Value = 0f };
            foreach (var (s, v) in affixes) it.Affixes.Add(new AffixRoll { Stat = s, Value = v, Tier = 1 });
            return it;
        }

        [Test]
        public void Rot_and_void_affixes_exist_like_the_other_elements()
        {
            var ember = ItemData.Affix(Stat.EmberPct);
            foreach (var s in new[] { Stat.RotPct, Stat.VoidPct })
            {
                var a = ItemData.Affix(s);
                Assert.That(a, Is.Not.Null, s.ToString());
                Assert.That(a.Min, Is.EqualTo(ember.Min));
                Assert.That(a.Max, Is.EqualTo(ember.Max));
                Assert.That(a.Slots, Is.EquivalentTo(ember.Slots));
            }
        }

        [Test]
        public void Every_class_finds_its_main_element_line_and_finds_it_more_often()
        {
            foreach (var cls in SkillBook.PlayableClasses)
            {
                var main = ItemGen.ClassElements(cls);
                Assert.That(main.Length, Is.GreaterThan(0), cls);
                var rng = new SimRng(4242);
                var counts = new int[6];
                for (int i = 0; i < 400; i++)
                    foreach (var a in ItemGen.Roll(rng, i, 30, Rarity.Rare, null, 0f, cls).Affixes)
                        counts[(int)ItemGen.ElementOf(a.Stat)]++;
                Assert.That(counts[(int)main[0]], Is.GreaterThan(0), cls + " never rolled its main element " + main[0]);
                for (int e = 1; e < counts.Length; e++)
                    if (!main.Contains((Element)e))
                        Assert.That(counts[(int)main[0]], Is.GreaterThan(counts[e] * 3 / 2), cls + ": " + main[0] + " vs " + (Element)e);
            }
        }

        [Test]
        public void Class_elements_come_from_the_class_skills()
        {
            Assert.That(ItemGen.ClassElements("ashknight")[0], Is.EqualTo(Element.Ember));
            Assert.That(ItemGen.ClassElements("oathknight")[0], Is.EqualTo(Element.Shock));
            Assert.That(ItemGen.ClassElements("windhunter"), Is.EqualTo(new[] { Element.Shock, Element.Void }));
            Assert.That(ItemGen.ClassElements(null), Is.Empty);
        }

        /// <summary>Damage of the first hit of <paramref name="skillId"/> on a dummy, with an amulet of <paramref name="stat"/> +50% or none.</summary>
        static float FirstHit(string skillId, Stat? stat)
        {
            var w = new SimWorld(7) { EnemyBrainEnabled = false };
            w.ClearEnemies();
            w.SpawnEnemy(new EnemyDef { Id = "test", MaxHp = 1e6f, Damage = 0f, MoveSpeed = 0f, Xp = 0 }, w.Hero.Pos + new Vec2(2f, 0f)).Aggro = true;
            w.Hero.Level = 30;
            w.Hero.Gear.Equip(stat != null ? Amulet((stat.Value, 50f)) : Amulet(), EquipSpot.Amulet);
            HeroStats.Recompute(w.Hero); // both runs: level 30 numbers, only the amulet differs
            var slots = new SkillDef[4];
            slots[0] = SkillBook.Get(skillId);
            w.Hero.Ring = new SkillRing(SkillBook.Basic, slots);
            for (int i = 0; i < 120; i++)
            {
                w.Tick();
                foreach (var ev in w.Events)
                    // void_lance lands as void_lance_burst
                    if (ev.Type == SimEventType.Hit && ev.SkillId != null && ev.SkillId.StartsWith(skillId) && !ev.FromChain && ev.Value > 0f) return ev.Value;
            }
            Assert.Fail(skillId + " never hit");
            return 0f;
        }

        [TestCase("venom_arrow", Stat.RotPct)]
        [TestCase("void_lance", Stat.VoidPct)]
        public void Rot_and_void_damage_raise_hits_of_their_element(string skillId, Stat stat)
        {
            float plain = FirstHit(skillId, null);
            float boosted = FirstHit(skillId, stat);
            Assert.That(boosted / plain, Is.EqualTo(1.5f).Within(0.01f), "+50% on an unbuffed hero");
            // and only their own element: ember damage does nothing for them
            Assert.That(FirstHit(skillId, Stat.EmberPct), Is.EqualTo(plain).Within(1e-3f));
        }

        [Test]
        public void Rot_and_void_lines_survive_a_save()
        {
            var w = new SimWorld(5);
            w.Hero.Gear.Equip(Amulet((Stat.RotPct, 9f), (Stat.VoidPct, 7f)), EquipSpot.Amulet);
            HeroStats.Recompute(w.Hero);
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            var h = SimWorld.FromSave(d).Hero;
            var it = h.Gear[EquipSpot.Amulet];
            Assert.That(it.Affixes.Select(a => a.Stat), Is.EqualTo(new[] { Stat.RotPct, Stat.VoidPct }));
            Assert.That(h.Derived.ElementPct(Element.Rot), Is.EqualTo(0.09f).Within(1e-4f));
            Assert.That(h.Derived.ElementPct(Element.Void), Is.EqualTo(0.07f).Within(1e-4f));
        }
    }
}
