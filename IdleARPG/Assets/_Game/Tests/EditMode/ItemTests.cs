using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class ItemTests
    {
        [Test]
        public void Same_seed_same_items()
        {
            var a = new SimRng(5);
            var b = new SimRng(5);
            for (int i = 0; i < 200; i++)
            {
                var ra = ItemGen.RollRarity(a, 20, 0f);
                var rb = ItemGen.RollRarity(b, 20, 0f);
                var ia = ItemGen.Roll(a, i, 12, ra);
                var ib = ItemGen.Roll(b, i, 12, rb);
                Assert.That(ia.Name, Is.EqualTo(ib.Name));
                Assert.That(ia.Affixes.Select(x => x.Value), Is.EqualTo(ib.Affixes.Select(x => x.Value)));
            }
        }

        [TestCase(Rarity.Common, 0, 0)]
        [TestCase(Rarity.Magic, 1, 2)]
        [TestCase(Rarity.Rare, 3, 4)]
        [TestCase(Rarity.Legendary, 3, 3)]
        public void Affix_count_matches_rarity(Rarity r, int min, int max)
        {
            var rng = new SimRng(9);
            for (int i = 0; i < 100; i++)
            {
                var it = ItemGen.Roll(rng, i, 10, r);
                Assert.That(it.Affixes.Count, Is.InRange(min, max), it.Name);
                Assert.That(it.Affixes.Select(a => a.Stat).Distinct().Count(), Is.EqualTo(it.Affixes.Count), "no duplicate affixes");
            }
        }

        [Test]
        public void Tier_never_exceeds_ilvl_tier()
        {
            var rng = new SimRng(3);
            foreach (int ilvl in new[] { 1, 9, 10, 35, 64, 65, 100 })
                for (int i = 0; i < 50; i++)
                    foreach (var a in ItemGen.Roll(rng, i, ilvl, Rarity.Rare).Affixes)
                        Assert.That(a.Tier, Is.InRange(1, ItemData.MaxTier(ilvl)), $"ilvl {ilvl}");
            Assert.That(ItemData.MaxTier(1), Is.EqualTo(1));
            Assert.That(ItemData.MaxTier(65), Is.EqualTo(7));
            Assert.That(ItemData.MaxTier(80), Is.EqualTo(8));
        }

        [Test]
        public void Legendary_has_an_essence_and_unique_has_fixed_stats()
        {
            var rng = new SimRng(4);
            var leg = ItemGen.Roll(rng, 1, 5, Rarity.Legendary);
            Assert.That(ItemData.Essence(leg.EssenceId), Is.Not.Null);
            var uni = ItemGen.Roll(rng, 2, 20, Rarity.Unique);
            Assert.That(uni.Affixes.Count, Is.EqualTo(4));
            Assert.That(uni.EssenceId, Is.Not.Empty);
        }

        [TestCase(1, 1)]
        [TestCase(5, 1)]
        [TestCase(6, 1)]
        [TestCase(12, 7)]
        public void Required_level_is_ilvl_minus_5(int ilvl, int req)
        {
            Assert.That(ItemGen.Roll(new SimRng(1), 1, ilvl, Rarity.Magic).ReqLevel, Is.EqualTo(req));
        }

        [Test]
        public void Rarity_weights_follow_gdd()
        {
            var rng = new SimRng(77);
            int n = 100000, rare = 0, leg = 0, uni = 0;
            for (int i = 0; i < n; i++)
            {
                var r = ItemGen.RollRarity(rng, 10, 0f);
                if (r == Rarity.Rare) rare++;
                if (r == Rarity.Legendary) leg++;
                if (r == Rarity.Unique) uni++;
            }
            float total = 60f + 28f + 10f + ItemGen.LegendaryWeight;
            Assert.That(rare / (float)n, Is.EqualTo(10f / total).Within(0.01f));
            Assert.That(leg / (float)n, Is.EqualTo(ItemGen.LegendaryWeight / total).Within(0.002f));
            Assert.That(uni, Is.EqualTo(0), "no uniques below Lv15");
        }
    }
}
