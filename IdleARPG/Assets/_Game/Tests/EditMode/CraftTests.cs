using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §9 재굴림 and 안정도.</summary>
    public class CraftTests
    {
        static SimWorld Rich()
        {
            var w = new SimWorld(51) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.CampUnlocked = true;
            w.Bag.AshShards = 100000;
            var sword = new Item { Uid = 6000, BaseId = "rusted_greatsword", Rarity = Rarity.Rare, Ilvl = 20, ReqLevel = 1, Name = "test sword" };
            sword.Affixes.Add(new AffixRoll { Stat = Stat.FlatDamage, Value = 30f, Tier = 1 });
            sword.Affixes.Add(new AffixRoll { Stat = Stat.CritChancePct, Value = 3f, Tier = 1 });
            w.Hero.Gear.Equip(sword, EquipSpot.Weapon);
            HeroStats.Recompute(w.Hero);
            return w;
        }

        [Test]
        public void Reroll_spends_stability_and_ash_and_keeps_the_other_line()
        {
            var w = Rich();
            var item = w.Hero.Gear[EquipSpot.Weapon];
            int ash = w.Bag.AshShards;
            Assert.That(w.Reroll(EquipSpot.Weapon, 1), Is.True);
            Assert.That(item.StabilityLeft, Is.EqualTo(20 - SimWorld.RerollStability));
            Assert.That(w.Bag.AshShards, Is.EqualTo(ash - SimWorld.RerollCost(0).ash));
            Assert.That(item.Affixes[0].Stat, Is.EqualTo(Stat.FlatDamage), "the other line stays");
            Assert.That(item.Affixes[1].Stat, Is.Not.EqualTo(Stat.FlatDamage), "no duplicate stat");
        }

        [Test]
        public void Once_a_line_is_picked_only_that_line_rerolls()
        {
            var w = Rich();
            Assert.That(w.Reroll(EquipSpot.Weapon, 1), Is.True);
            Assert.That(w.Reroll(EquipSpot.Weapon, 0), Is.False);
            Assert.That(w.Reroll(EquipSpot.Weapon, 1), Is.True);
        }

        [Test]
        public void An_empty_budget_stops_crafting_but_the_item_stays()
        {
            var w = Rich();
            int n = 0;
            while (w.Reroll(EquipSpot.Weapon, 1)) n++;
            Assert.That(n, Is.EqualTo(20 / SimWorld.RerollStability));
            Assert.That(w.Hero.Gear[EquipSpot.Weapon], Is.Not.Null);
            Assert.That(w.Hero.Gear[EquipSpot.Weapon].StabilityLeft, Is.EqualTo(0));
        }

        [Test]
        public void Gamble_buys_an_item_of_the_chosen_slot_at_least_magic()
        {
            var w = Rich();
            foreach (var slot in new[] { Slot.Boots, Slot.Ring, Slot.Amulet, Slot.Weapon })
            {
                int ash = w.Bag.AshShards;
                var item = w.Gamble(slot);
                Assert.That(item, Is.Not.Null);
                Assert.That(item.Slot, Is.EqualTo(slot));
                Assert.That(item.Rarity, Is.GreaterThanOrEqualTo(Rarity.Magic));
                Assert.That(w.Bag.AshShards, Is.EqualTo(ash - w.GambleCost));
            }
            w.Bag.AshShards = 0;
            Assert.That(w.Gamble(Slot.Helm), Is.Null, "no shards, no gamble");
        }

        [Test]
        public void Temper_adds_a_line_for_six_stability_up_to_the_rarity_cap()
        {
            var w = Rich();
            w.Bag.LanternOil = 100;
            var item = w.Hero.Gear[EquipSpot.Weapon];
            Assert.That(w.Temper(EquipSpot.Weapon), Is.True);
            Assert.That(item.Affixes.Count, Is.EqualTo(3));
            Assert.That(item.StabilityLeft, Is.EqualTo(20 - SimWorld.TemperStability));
            Assert.That(item.Affixes.Select(a => a.Stat).Distinct().Count(), Is.EqualTo(3), "no duplicate stat");
            Assert.That(w.Temper(EquipSpot.Weapon), Is.True);
            Assert.That(w.Temper(EquipSpot.Weapon), Is.False, "rare caps at four lines");
        }

        [Test]
        public void The_altar_turns_three_of_the_most_plentiful_rune_into_the_next()
        {
            var w = Rich();
            w.Bag.RuneCounts[(int)Rune.Ash] = 5;
            w.Bag.RuneCounts[(int)Rune.Frost] = 1;
            Assert.That(w.TransmuteFrom, Is.EqualTo(Rune.Ash));
            Assert.That(w.TransmuteRune(), Is.True);
            Assert.That(w.Bag.RuneCounts[(int)Rune.Ash], Is.EqualTo(2));
            Assert.That(w.Bag.RuneCounts[(int)Rune.Frost], Is.EqualTo(2));
            Assert.That(w.TransmuteRune(), Is.False, "no rune has three left");
        }

        [Test]
        public void Spots_map_to_their_slots()
        {
            Assert.That(Equipment.SlotOf(EquipSpot.Ring2), Is.EqualTo(Slot.Ring));
            Assert.That(Equipment.SlotOf(EquipSpot.Amulet), Is.EqualTo(Slot.Amulet));
            Assert.That(Equipment.SlotOf(EquipSpot.Belt), Is.EqualTo(Slot.Belt));
        }

        [Test]
        public void Crafting_state_survives_a_save()
        {
            var w = Rich();
            w.Reroll(EquipSpot.Weapon, 1);
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            var it = SimWorld.FromSave(d).Hero.Gear[EquipSpot.Weapon];
            Assert.That(it.StabilityLeft, Is.EqualTo(16));
            Assert.That(it.RerollLine, Is.EqualTo(1));
            Assert.That(it.Rerolls, Is.EqualTo(1));
        }
    }
}
