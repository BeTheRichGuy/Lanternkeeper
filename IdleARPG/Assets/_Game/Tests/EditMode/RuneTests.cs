using System.Collections.Generic;
using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §8.7 sockets, runes and runewords.</summary>
    public class RuneTests
    {
        static Item Socketed(int sockets) =>
            new Item { Uid = 9000 + sockets, BaseId = "rusted_greatsword", Rarity = Rarity.Rare, Ilvl = 5, ReqLevel = 1, Name = "socketed", Sockets = sockets };

        [Test]
        public void Runewords_need_the_right_runes_in_the_right_order()
        {
            Assert.That(Runes.Match(new List<int> { (int)Rune.Ash, (int)Rune.Frost, (int)Rune.Thunder })?.Id, Is.EqualTo("rw_cycle"));
            Assert.That(Runes.Match(new List<int> { (int)Rune.Thunder, (int)Rune.Frost, (int)Rune.Ash }), Is.Null);
            Assert.That(Runes.Match(new List<int> { (int)Rune.Ash }), Is.Null);
        }

        [Test]
        public void Runes_add_their_stat_to_the_item()
        {
            var it = Socketed(2);
            it.RuneSlots.Add((int)Rune.Bone);
            Assert.That(it.Sum(Stat.MaxHp), Is.EqualTo(15f).Within(1e-3f));
        }

        [Test]
        public void A_rune_goes_into_a_free_socket_by_hand_and_stops_when_full()
        {
            var w = new SimWorld(92) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            var helm = Socketed(1);
            w.Bag.RuneCounts[(int)Rune.Bone] = 2;
            Assert.That(w.SocketRune(helm, Rune.Bone), Is.True);
            Assert.That(helm.RuneSlots, Is.EqualTo(new[] { (int)Rune.Bone }));
            Assert.That(w.Bag.RuneCounts[(int)Rune.Bone], Is.EqualTo(1));
            Assert.That(w.SocketRune(helm, Rune.Bone), Is.False, "one socket, already filled");
            Assert.That(w.SocketRune(Socketed(2), Rune.Ash), Is.False, "no ash rune in the hoard");
            Assert.That(w.Bag.RuneCounts[(int)Rune.Bone], Is.EqualTo(1));
        }

        [Test]
        public void Auto_socketing_completes_a_runeword_and_its_effect_turns_on()
        {
            var w = new SimWorld(91) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            var sword = Socketed(3);
            w.Hero.Gear.Equip(sword, EquipSpot.Weapon);
            HeroStats.Recompute(w.Hero);
            float dr0 = w.Hero.Derived.DamageReduction;
            w.GainRune(Rune.Bone, w.Hero.Pos);
            w.GainRune(Rune.Oath, w.Hero.Pos);
            Assert.That(sword.RuneSlots.Count, Is.EqualTo(0), "never a partial fill");
            w.GainRune(Rune.Bone, w.Hero.Pos);
            Assert.That(sword.Runeword?.Id, Is.EqualTo("rw_bonecitadel"));
            Assert.That(w.Bag.RuneCounts.Sum(), Is.EqualTo(0));
            Assert.That(w.Events.Any(e => e.Type == SimEventType.Runeword), Is.True);
            // +10% before the melee guard, which multiplies on top (the default hero is melee).
            Assert.That(w.Hero.Derived.DamageReduction, Is.GreaterThanOrEqualTo(dr0 + 0.1f * (1f - HeroStats.MeleeGuardPct / 100f) - 1e-4f));
        }

        [Test]
        public void Lanternkeeper_runeword_adds_a_chain_hop()
        {
            var w = new SimWorld(92) { HeroBrainEnabled = false };
            int cap0 = w.EffectiveChainCap;
            var sword = Socketed(3);
            sword.RuneSlots.AddRange(new[] { (int)Rune.Lantern, (int)Rune.Oath, (int)Rune.Lantern });
            w.Hero.Gear.Equip(sword, EquipSpot.Weapon);
            HeroStats.Recompute(w.Hero);
            Assert.That(w.EffectiveChainCap, Is.EqualTo(cap0 + 1));
        }

        [Test]
        public void Some_gear_rolls_sockets()
        {
            var rng = new SimRng(3);
            int socketed = 0;
            for (int i = 0; i < 400; i++) if (ItemGen.Roll(rng, i, 10, Rarity.Rare).Sockets > 0) socketed++;
            Assert.That(socketed, Is.GreaterThan(20));
        }

        [Test]
        public void Runes_and_sockets_survive_a_save()
        {
            var w = new SimWorld(93) { HeroBrainEnabled = false };
            var sword = Socketed(2);
            sword.RuneSlots.AddRange(new[] { (int)Rune.Ash, (int)Rune.Ash });
            w.Hero.Gear.Equip(sword, EquipSpot.Weapon);
            w.Bag.RuneCounts[(int)Rune.Star] = 3;
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.Hero.Gear[EquipSpot.Weapon].Runeword?.Id, Is.EqualTo("rw_firering"));
            Assert.That(b.Bag.RuneCounts[(int)Rune.Star], Is.EqualTo(3));
        }
    }
}
