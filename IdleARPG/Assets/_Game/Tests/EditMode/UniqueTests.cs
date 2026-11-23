using System;
using System.Collections.Generic;
using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>
    /// 희승 11-24 ("전설이나 고유 아이템들도, 디아블로에서 먹었을 때는 기분 좋았는데, 이건 그렇지 않아"): every class has
    /// uniques of its own with a power of their own and a line of lore; legendaries roll only the hero's class's powers;
    /// every new power does something the screen shows.
    /// </summary>
    public class UniqueTests
    {
        static readonly Dictionary<string, string[]> Expected = new Dictionary<string, string[]>
        {
            { "ashknight",  new[] { "unquenched_wick", "dead_kings_seal", "gravekeepers_oath", "furnace_heart", "crown_of_cinders" } },
            { "oathknight", new[] { "unquenched_wick", "dead_kings_seal", "judges_vow", "pilgrims_grasp", "silent_vow" } },
            { "windhunter", new[] { "unquenched_wick", "dead_kings_seal", "stormstring", "galewalkers", "hawkeye_hood" } },
            { "starseer",   new[] { "unquenched_wick", "dead_kings_seal", "fallen_star_staff", "constellation_robe", "timeeater_sash" } },
        };

        [Test]
        public void Every_unique_has_lore_its_own_power_and_its_own_stats()
        {
            Assert.That(ItemData.Uniques.Length, Is.EqualTo(14));
            var powers = new HashSet<string>();
            var statSets = new HashSet<string>();
            foreach (var u in ItemData.Uniques)
            {
                Assert.That(u.Flavor, Is.Not.Null.And.Not.Empty, u.Id);
                Assert.That(u.Flavor.Length, Is.LessThanOrEqualTo(40), u.Id);
                var ess = ItemData.Essence(u.EssenceId);
                Assert.That(ess, Is.Not.Null, u.Id);
                Assert.That(ess.UniqueOnly, Is.True, u.Id + " has a power of its own");
                Assert.That(ess.Text, Is.Not.Empty, u.Id);
                Assert.That(powers.Add(u.EssenceId), Is.True, u.Id + " shares its power");
                Assert.That(u.Fixed.Select(f => f.stat).Distinct().Count(), Is.EqualTo(u.Fixed.Length), u.Id);
                Assert.That(statSets.Add(string.Join(",", u.Fixed.Select(f => f.stat + "=" + f.value))), Is.True, u.Id + " copies another unique's stats");
            }
        }

        [Test]
        public void Each_class_rolls_its_own_uniques_and_only_those()
        {
            foreach (var cls in SkillBook.PlayableClasses)
            {
                var rng = new SimRng(31);
                var seen = new HashSet<string>();
                for (int i = 0; i < 600; i++)
                {
                    var it = ItemGen.Roll(rng, i, 30, Rarity.Unique, null, 0f, cls);
                    Assert.That(it.Rarity, Is.EqualTo(Rarity.Unique));
                    var u = ItemData.Unique(it.UniqueId);
                    Assert.That(u, Is.Not.Null, it.Name);
                    Assert.That(it.Name, Is.EqualTo(u.Name));
                    Assert.That(it.EssenceId, Is.EqualTo(u.EssenceId));
                    Assert.That(ItemData.Base(it.BaseId).Fits(cls), Is.True, cls + " · " + it.Name);
                    seen.Add(u.Id);
                }
                Assert.That(seen, Is.EquivalentTo(Expected[cls]), cls);
            }
        }

        [Test]
        public void Legendaries_roll_only_their_class_powers_never_a_uniques()
        {
            var ashOnly = new[] { "ember_echo", "wide_whirl", "whirl_finale", "brand_burst" };
            foreach (var cls in SkillBook.PlayableClasses)
            {
                var rng = new SimRng(17);
                var seen = new HashSet<string>();
                for (int i = 0; i < 2000; i++)
                {
                    var it = ItemGen.Roll(rng, i, 30, Rarity.Legendary, null, 0f, cls);
                    var ess = ItemData.Essence(it.EssenceId);
                    Assert.That(ess, Is.Not.Null);
                    Assert.That(ess.UniqueOnly, Is.False, it.EssenceId);
                    Assert.That(ess.Fits(cls), Is.True, cls + " · " + it.EssenceId);
                    seen.Add(it.EssenceId);
                }
                if (cls == "starseer") Assert.That(seen.Intersect(ashOnly), Is.Empty, "a 별점술사 never finds a 휠윈드 power");
                // Two powers of its own per class, and they do drop.
                int own = ItemData.Essences.Count(e => !e.UniqueOnly && e.Classes != null && e.Classes.Length == 1 && e.Classes[0] == cls && !new[] { "ember_echo", "wide_whirl" }.Contains(e.Id));
                Assert.That(own, Is.EqualTo(2), cls);
                foreach (var e in ItemData.Essences.Where(e => !e.UniqueOnly && e.Fits(cls))) Assert.That(seen, Has.Member(e.Id), cls + " never rolled " + e.Id);
            }
        }

        /// <summary>The power's own mark on the event stream: what the view draws for it.</summary>
        [TestCase("whirl_finale", "ashknight", 0, SimEventType.Cast, "cinder_echo_burst")]
        [TestCase("brand_burst", "ashknight", 1, SimEventType.Cast, "frost_nova")]
        [TestCase("grave_burst", "ashknight", 1, SimEventType.Cast, "cinder_echo_burst")]
        [TestCase("furnace_trail", "ashknight", 0, SimEventType.Ground, "evo_meteor_burn")]
        [TestCase("cinder_crown", "ashknight", 0, SimEventType.Cast, "meteor")]
        [TestCase("kings_echo", "ashknight", 1, SimEventType.GrandResonance, null)]
        [TestCase("wick_flame", "ashknight", 2, SimEventType.Ground, "evo_meteor_burn")]
        [TestCase("zeal_arc", "oathknight", 1, SimEventType.Shot, "shock_arc")]
        [TestCase("bash_ricochet", "oathknight", 2, SimEventType.Shot, "shield_throw")]
        [TestCase("judges_verdict", "oathknight", 0, SimEventType.Cast, "thunderstorm_bolt")]
        [TestCase("pilgrim_hammers", "oathknight", 0, SimEventType.Cast, "blessed_hammer_echo")]
        [TestCase("silent_bastion", "oathknight", 4, SimEventType.Cast, "unbroken_vow_burst")]
        [TestCase("volley_pierce", "windhunter", 0, SimEventType.Shot, "gale_volley")]
        [TestCase("rain_rot", "windhunter", 1, SimEventType.Ground, "viper")]
        [TestCase("storm_forks", "windhunter", 0, SimEventType.Shot, "chain_arrow")]
        [TestCase("gale_ward", "windhunter", 0, SimEventType.Cast, "whirling_blades")]
        [TestCase("hawk_mark", "windhunter", 0, SimEventType.Cast, "hunters_mark")]
        [TestCase("meteor_scorch", "starseer", 0, SimEventType.Ground, "evo_meteor_burn")]
        [TestCase("ice_splinter", "starseer", 1, SimEventType.Shot, "ice_bolt")]
        [TestCase("star_fall", "starseer", 0, SimEventType.Cast, "meteor")]
        [TestCase("orbit_stars", "starseer", 2, SimEventType.Shot, "star_orb")]
        [TestCase("time_eater", "starseer", 0, SimEventType.Cast, "time_stop")]
        public void Each_new_power_shows_on_screen(string essence, string cls, int preset, SimEventType type, string skillId)
        {
            int without = Count(cls, preset, null, type, skillId, out _);
            int with = Count(cls, preset, essence, type, skillId, out var w);
            Assert.That(w.Hero.Has(essence), Is.True);
            Assert.That(with, Is.GreaterThan(without), essence + " left no mark of its own");
        }

        /// <summary>Lv30 with a set of rares in act III, the power (or none) on the belt, two minutes of play.</summary>
        static int Count(string cls, int preset, string essence, SimEventType type, string skillId, out SimWorld w)
        {
            w = new SimWorld(11, cls);
            w.Hero.Level = 30;
            w.AutoEquip = false;
            var rng = new SimRng(99);
            int uid = 1000;
            foreach (Slot slot in Enum.GetValues(typeof(Slot)))
                foreach (var spot in Equipment.SpotsFor(slot))
                {
                    var it = ItemGen.Roll(rng, uid++, 30, Rarity.Rare, slot, 0f, cls);
                    it.ReqLevel = 1;
                    w.Hero.Gear.Equip(it, spot);
                }
            if (essence != null) w.Hero.Gear.Spots[(int)EquipSpot.Belt].EssenceId = essence;
            w.SetPreset(preset);
            w.Hero.Stats.Hp = w.Hero.Stats.MaxHp;
            w.GoToFloor(SimWorld.ActLength * 2 + 2);
            int n = 0;
            for (int t = 0; t < 1200; t++)
            {
                w.Tick();
                foreach (var e in w.Events) if (e.Type == type && (skillId == null || e.SkillId == skillId)) n++;
            }
            return n;
        }

        [Test]
        public void Save_keeps_the_unique_id()
        {
            var a = new SimWorld(5, "windhunter");
            var it = ItemGen.Roll(new SimRng(3), 900, 30, Rarity.Unique, null, 0f, "windhunter");
            a.Bag.Add(it);
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(a.ToSave()), out var data), Is.True);
            var b = SimWorld.FromSave(data);
            var back = b.FindInBag(900);
            Assert.That(back, Is.Not.Null);
            Assert.That(back.UniqueId, Is.EqualTo(it.UniqueId).And.Not.Empty);
            Assert.That(back.EssenceId, Is.EqualTo(it.EssenceId));
        }

        [Test]
        public void An_old_unique_without_an_id_gets_its_id_and_its_own_power_on_load()
        {
            var a = new SimWorld(6, "oathknight");
            var it = ItemGen.Roll(new SimRng(4), 901, 30, Rarity.Unique, null, 0f, "oathknight");
            var def = ItemData.Unique(it.UniqueId);
            it.UniqueId = null;          // a save from before 11-24 had no such field
            it.EssenceId = "ash_feast";  // and four weapons shared 재를 먹는
            a.Bag.Add(it);
            var b = SimWorld.FromSave(a.ToSave());
            var back = b.FindInBag(901);
            Assert.That(back.UniqueId, Is.EqualTo(def.Id));
            Assert.That(back.EssenceId, Is.EqualTo(def.EssenceId));
        }
    }
}
