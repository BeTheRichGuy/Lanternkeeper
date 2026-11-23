using System.Linq;
using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class OfflineTests
    {
        static SimWorld WithRates(float kpm, float xpm, int seed = 61)
        {
            var save = new SimWorld(seed).ToSave();
            save.KillsPerMinute = kpm;
            save.XpPerMinute = xpm;
            return SimWorld.FromSave(save);
        }

        [Test]
        public void Caps_at_8_hours_and_80_percent()
        {
            var w = WithRates(10f, 100f);
            var r = w.ApplyOffline(20 * 3600);
            Assert.That(r.CappedSeconds, Is.EqualTo(8 * 3600));
            Assert.That(r.Kills, Is.EqualTo((int)System.Math.Round(10 * 480 * 0.8)));
            Assert.That(r.XpGained, Is.EqualTo((long)System.Math.Round(100 * 480 * 0.8)));
            Assert.That(r.LevelAfter, Is.GreaterThan(r.LevelBefore));
        }

        [Test]
        public void Short_absence_gives_nothing()
        {
            var w = WithRates(10f, 100f);
            Assert.That(w.ApplyOffline(30).Empty, Is.True);
        }

        [Test]
        public void Every_offline_legendary_is_kept()
        {
            var w = WithRates(60f, 50f);
            w.Filter = FilterPreset.Strict;
            var r = w.ApplyOffline(8 * 3600);
            Assert.That(r.Legendaries.Count, Is.GreaterThan(0), "≈23k kills should roll a legendary");
            foreach (var leg in r.Legendaries)
            {
                bool kept = w.Bag.Items.Contains(leg) || w.Hero.Gear.Spots.Contains(leg);
                Assert.That(kept, Is.True, leg.Name);
            }
        }

        [Test]
        public void Offline_is_deterministic()
        {
            var a = WithRates(20f, 80f, 62).ApplyOffline(3 * 3600);
            var b = WithRates(20f, 80f, 62).ApplyOffline(3 * 3600);
            Assert.That(a.Kills, Is.EqualTo(b.Kills));
            Assert.That(a.Legendaries.Select(i => i.Name), Is.EqualTo(b.Legendaries.Select(i => i.Name)));
            Assert.That(a.AshGained, Is.EqualTo(b.AshGained));
        }

        [Test]
        public void Rates_are_measured_while_playing()
        {
            var w = new SimWorld(63);
            for (int i = 0; i < 6000; i++) w.Tick(); // 10 minutes
            Assert.That(w.KillsPerMinute, Is.GreaterThan(0f));
            Assert.That(w.XpPerMinute, Is.GreaterThan(0f));
        }
    }
}
