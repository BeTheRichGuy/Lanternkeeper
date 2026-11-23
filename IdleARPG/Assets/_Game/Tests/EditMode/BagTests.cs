using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>2026-11-21 재기획 §5.4: the gear panel equips, locks and salvages bag items by hand.</summary>
    public class BagTests
    {
        static Item Make(SimWorld w, Rarity r, Slot slot, int ilvl = 5)
        {
            var it = ItemGen.Roll(new SimRng((ulong)(w.NextItemUid * 977 + 13)), w.NextItemUid++, ilvl, r, slot, 0f);
            it.ReqLevel = 1;
            return it;
        }

        static SimWorld World()
        {
            var w = new SimWorld(5, "ashknight") { EnemyBrainEnabled = false, AutoEquip = false };
            w.UseOpenFloor();
            return w;
        }

        [Test]
        public void Equipping_from_the_bag_swaps_with_what_was_worn()
        {
            var w = World();
            var first = Make(w, Rarity.Magic, Slot.Helm);
            var second = Make(w, Rarity.Rare, Slot.Helm);
            w.Bag.Items.Add(first);
            w.Bag.Items.Add(second);

            Assert.That(w.EquipFromBag(first.Uid), Is.True);
            Assert.That(w.Hero.Gear.Spots[(int)EquipSpot.Helm], Is.SameAs(first));
            Assert.That(w.Bag.Items, Has.No.Member(first));

            Assert.That(w.EquipFromBag(second.Uid), Is.True);
            Assert.That(w.Hero.Gear.Spots[(int)EquipSpot.Helm], Is.SameAs(second));
            Assert.That(w.Bag.Items, Has.Member(first), "the replaced helm goes back to the bag");
        }

        [Test]
        public void Items_above_the_integrated_level_stay_in_the_bag()
        {
            var w = World();
            var it = Make(w, Rarity.Rare, Slot.Boots);
            it.ReqLevel = w.Hero.IntegratedLevel + 5;
            w.Bag.Items.Add(it);
            Assert.That(w.EquipFromBag(it.Uid), Is.False);
            Assert.That(w.Bag.Items, Has.Member(it));
        }

        [Test]
        public void A_ring_goes_to_an_empty_ring_spot_first()
        {
            var w = World();
            var a = Make(w, Rarity.Rare, Slot.Ring);
            var b = Make(w, Rarity.Rare, Slot.Ring);
            w.Bag.Items.Add(a);
            w.Bag.Items.Add(b);
            w.EquipFromBag(a.Uid);
            w.EquipFromBag(b.Uid);
            Assert.That(w.Hero.Gear.Spots[(int)EquipSpot.Ring1], Is.Not.Null);
            Assert.That(w.Hero.Gear.Spots[(int)EquipSpot.Ring2], Is.Not.Null);
        }

        [Test]
        public void Salvaging_pays_ash_but_never_takes_a_locked_item()
        {
            var w = World();
            var it = Make(w, Rarity.Rare, Slot.Gloves);
            w.Bag.Items.Add(it);
            w.ToggleLock(it.Uid);
            Assert.That(it.Locked, Is.True);
            Assert.That(w.SalvageFromBag(it.Uid), Is.False);

            w.ToggleLock(it.Uid);
            int ash = w.Bag.AshShards;
            Assert.That(w.SalvageFromBag(it.Uid), Is.True);
            Assert.That(w.Bag.Items, Has.No.Member(it));
            Assert.That(w.Bag.AshShards, Is.EqualTo(ash + LootFilter.SalvageValue(it)));
        }

        [Test]
        public void Salvage_all_keeps_locked_star_and_legendary_pieces()
        {
            var w = World();
            var plain = Make(w, Rarity.Magic, Slot.Belt);
            var locked = Make(w, Rarity.Rare, Slot.Belt);
            locked.Locked = true;
            var legendary = Make(w, Rarity.Legendary, Slot.Belt);
            var star = Make(w, Rarity.Rare, Slot.Belt);
            var line = star.Affixes[0];
            line.Greater = true;
            star.Affixes[0] = line;
            w.Bag.Items.AddRange(new[] { plain, locked, legendary, star });

            Assert.That(w.SalvageAll(), Is.EqualTo(1));
            Assert.That(w.Bag.Items, Is.EquivalentTo(new[] { locked, legendary, star }));
        }

        [Test]
        public void A_full_bag_never_salvages_a_locked_item()
        {
            var w = World();
            var bag = new Bag();
            for (int i = 0; i < Bag.Capacity; i++) bag.Items.Add(Make(w, Rarity.Rare, Slot.Amulet, ilvl: 50));
            var weakest = Make(w, Rarity.Common, Slot.Amulet, ilvl: 1);
            weakest.Locked = true;
            bag.Items.Insert(0, weakest);
            var gone = bag.Add(Make(w, Rarity.Rare, Slot.Amulet, ilvl: 50));
            Assert.That(gone, Is.Not.SameAs(weakest));
            Assert.That(bag.Items, Has.Member(weakest));
        }

        [Test]
        public void Auto_equip_leaves_a_locked_worn_piece_on()
        {
            var w = World();
            var weak = Make(w, Rarity.Common, Slot.Helm, ilvl: 1);
            w.Bag.Items.Add(weak);
            w.EquipFromBag(weak.Uid);
            w.ToggleLock(weak.Uid);
            var strong = Make(w, Rarity.Legendary, Slot.Helm, ilvl: 40);
            w.TryAutoEquip(strong, out bool equipped);
            Assert.That(equipped, Is.False);
            Assert.That(w.Hero.Gear.Spots[(int)EquipSpot.Helm], Is.SameAs(weak));
        }

        [Test]
        public void Lock_survives_a_save()
        {
            var w = World();
            var it = Make(w, Rarity.Rare, Slot.Armor);
            w.Bag.Items.Add(it);
            w.ToggleLock(it.Uid);
            var back = SimWorld.FromSave(w.ToSave());
            Assert.That(back.Bag.Items.Single(x => x.Uid == it.Uid).Locked, Is.True);
        }
    }
}
