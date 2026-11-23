using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §12.5 도전 과제 and lantern colours.</summary>
    public class AchievementTests
    {
        [Test]
        public void A_goal_met_is_awarded_once_and_announced()
        {
            var w = new SimWorld(91) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.Hero.DeepestActCleared = 0; // 석관 군주 down
            bool said = false;
            for (int i = 0; i < 20; i++) { w.Tick(); said |= w.Events.Any(e => e.Type == SimEventType.Achievement && e.SkillId == "모래 왕의 안식"); }
            Assert.That(said, Is.True);
            Assert.That(w.Hero.Achieved.Contains("act1"), Is.True);
            int n = 0;
            for (int i = 0; i < 20; i++) { w.Tick(); n += w.Events.Count(e => e.Type == SimEventType.Achievement && e.SkillId == "모래 왕의 안식"); }
            Assert.That(n, Is.Zero);
        }

        [Test]
        public void Lantern_colours_cycle_through_the_unlocked_ones_only()
        {
            var w = new SimWorld(92);
            w.CycleLantern();
            Assert.That(w.Hero.LanternColor, Is.EqualTo(0), "nothing unlocked yet");
            w.Hero.Achieved.Add("act1"); // 성스러운 금 (2)
            w.CycleLantern();
            Assert.That(w.Hero.LanternColor, Is.EqualTo(2));
            w.CycleLantern();
            Assert.That(w.Hero.LanternColor, Is.EqualTo(0));
        }

        [Test]
        public void Goals_and_lantern_survive_a_save()
        {
            var w = new SimWorld(93);
            w.Hero.Achieved.Add("chain_5");
            w.Hero.LanternColor = 1;
            w.Hero.GrandCount = 42;
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.Hero.Achieved.Contains("chain_5"), Is.True);
            Assert.That(b.Hero.LanternColor, Is.EqualTo(1));
            Assert.That(b.Hero.GrandCount, Is.EqualTo(42));
        }
    }
}
