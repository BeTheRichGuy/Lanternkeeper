using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class RingTrialTests
    {
        [Test]
        public void Trial_ranks_every_distinct_order_and_leaves_the_world_alone()
        {
            var w = new SimWorld(2027, "windhunter");
            for (int i = 0; i < 3000; i++) w.Tick(); // a few levels in: more than one ring slot open
            ulong before = w.StateHash();
            var results = RingTrial.Run(w, 100);
            Assert.That(w.StateHash(), Is.EqualTo(before), "the trial runs on copies");
            Assert.That(results.Count, Is.GreaterThan(0));
            Assert.That(results.Count, Is.LessThanOrEqualTo(24));
            Assert.That(results.Select(r => string.Join("|", r.Order)).Distinct().Count(), Is.EqualTo(results.Count), "no order twice");
            for (int i = 1; i < results.Count; i++) Assert.That(results[i - 1].Damage, Is.GreaterThanOrEqualTo(results[i].Damage), "best first");
        }

        [Test]
        public void Same_world_same_trial()
        {
            var a = new SimWorld(5, "ashknight");
            var b = new SimWorld(5, "ashknight");
            for (int i = 0; i < 2000; i++) { a.Tick(); b.Tick(); }
            var ra = RingTrial.Run(a, 60);
            var rb = RingTrial.Run(b, 60);
            Assert.That(ra.Select(r => r.Damage), Is.EqualTo(rb.Select(r => r.Damage)));
        }
    }
}
