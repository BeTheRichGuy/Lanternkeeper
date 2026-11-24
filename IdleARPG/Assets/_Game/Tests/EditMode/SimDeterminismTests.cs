using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class SimDeterminismTests
    {
        // 2026-11-21: 기억의 나무 (skills unlock in the tree, 변형, evolution at 숙련 10) (was 4311340449841075782 for elite
        // prefixes; earlier 1110536597557234290 for runes, 3550800341123455142, …).
        // 11-24 고유·전설 확장: the run's one legendary (t891) now rolls from the 잿불기사's own powers (폭발하는, was 산산조각 내는);
        // putting that one power back gives the old 14917481850882282425 exactly.
        // 11-24 F2 부패·공허 옵션: two more element lines in the affix pool and the class's main elements weighted ×3 change
        // the drops; without both (same code otherwise) the run gives the old 2265059884929124546 exactly.
        const ulong GoldenHash = 14909465061752900787UL;
        [Test]
        public void SimRng_same_seed_same_sequence()
        {
            var a = new SimRng(1234);
            var b = new SimRng(1234);
            for (int i = 0; i < 1000; i++)
                Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
        }

        /// <summary>
        /// Pinned result of a 5-minute run. A change here means the simulation's behaviour changed:
        /// fine when you changed a rule on purpose (update the number in the same commit), a bug otherwise.
        /// </summary>
        [Test]
        public void Golden_hash_seed_2026_after_3000_ticks()
        {
            var w = new SimWorld(2026);
            for (int i = 0; i < 3000; i++) w.Tick();
            // 2026-11-19: updated for the 6–12 room floor graph (was 6851948719818537710).
            Assert.That(w.StateHash(), Is.EqualTo(GoldenHash));
        }

        [Test]
        public void SimRng_range_stays_in_bounds()
        {
            var rng = new SimRng(9);
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.Range(3, 8);
                Assert.That(v, Is.InRange(3, 7));
                float f = rng.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }
    }
}
