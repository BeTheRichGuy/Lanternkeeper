using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>환생 증강 카드 (희승 11-22).</summary>
    public class AugmentTests
    {
        static SimWorld Fresh(string cls = "windhunter")
        {
            var w = new SimWorld(5, cls) { AutoPickAugments = false };
            w.Tick();
            return w;
        }

        [Test]
        public void A_new_life_deals_three_cards_and_one_slot()
        {
            var w = Fresh();
            Assert.That(w.Hero.AugOffer, Is.Not.Null);
            Assert.That(w.Hero.AugOffer.Length, Is.EqualTo(3));
            Assert.That(w.Hero.AugOffer.Distinct().Count(), Is.EqualTo(3));
            Assert.That(w.ChooseAugment(1), Is.True);
            Assert.That(w.Hero.Augments.Count, Is.EqualTo(1));
            for (int i = 0; i < 20; i++) w.Tick();
            Assert.That(w.Hero.AugOffer, Is.Null, "the second slot opens at Lv20");
            Assert.That(Augments.SlotsAt(1), Is.EqualTo(1));
            Assert.That(Augments.SlotsAt(20), Is.EqualTo(2));
            Assert.That(Augments.SlotsAt(40), Is.EqualTo(3));
        }

        [Test]
        public void The_second_card_comes_from_another_branch()
        {
            var w = Fresh();
            var a = Augments.Get(w.Hero.AugOffer[0]);
            var b = Augments.Get(w.Hero.AugOffer[1]);
            Assert.That(b.Branch, Is.Not.EqualTo(a.Branch));
        }

        [Test]
        public void A_slot_is_re_dealt_once_only()
        {
            var w = Fresh();
            var first = w.Hero.AugOffer;
            Assert.That(w.RerollAugments(), Is.True);
            Assert.That(w.Hero.AugOffer, Is.Not.EqualTo(first));
            Assert.That(w.RerollAugments(), Is.False);
        }

        [Test]
        public void Nobody_choosing_picks_one_after_thirty_seconds()
        {
            var w = new SimWorld(5, "starseer");
            for (int i = 0; i < 310; i++) w.Tick();
            Assert.That(w.Hero.Augments.Count, Is.EqualTo(1));
        }

        [Test]
        public void An_element_card_raises_that_element()
        {
            var w = Fresh("starseer");
            var card = Augments.For("starseer").First(x => x.Kind == AugKind.Element);
            w.Hero.AugOffer = new[] { card.Id };
            w.ChooseAugment(0);
            Assert.That(w.Hero.AugElem[(int)card.Elem], Is.EqualTo(card.Value).Within(1e-4f));
        }

        [Test]
        public void Rebirth_clears_the_cards_and_deals_again()
        {
            var w = Fresh();
            w.ChooseAugment(0);
            w.Hero.Level = SimWorld.LevelCap;
            Assert.That(w.Rebirth(), Is.True);
            Assert.That(w.Hero.Augments.Count, Is.EqualTo(0));
            w.Tick();
            Assert.That(w.Hero.AugOffer, Is.Not.Null);
        }

        [Test]
        public void Cards_and_the_branch_sets_survive_a_save()
        {
            var w = Fresh();
            w.ChooseAugment(2);
            w.SaveBranchSet();
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.Hero.Augments, Is.EqualTo(w.Hero.Augments));
            Assert.That(b.HasBranchSet(w.BranchInUse()), Is.True);
        }

        [Test]
        public void Every_class_has_cards_for_each_branch()
        {
            foreach (var cls in SkillBook.PlayableClasses)
                for (int br = 0; br < 3; br++)
                    Assert.That(Augments.For(cls).Any(a => a.Branch == br), Is.True, cls + " branch " + br);
        }
    }
}
