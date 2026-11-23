using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class SimDeterminismTests
    {
        // 2026-11-21: 기억의 나무 (skills unlock in the tree, 변형, evolution at 숙련 10) (was 4311340449841075782 for elite
        // prefixes; earlier 1110536597557234290 for runes, 3550800341123455142, …).
        const ulong GoldenHash = 16873000450390764041UL; // x2 walk between fights (11-23)
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
