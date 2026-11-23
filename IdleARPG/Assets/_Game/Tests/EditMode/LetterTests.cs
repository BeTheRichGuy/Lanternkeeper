using System.Linq;
using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §26.6 이야기 우편함.</summary>
    public class LetterTests
    {
        [Test]
        public void A_record_opens_at_its_milestone_once()
        {
            var w = new SimWorld(95) { HeroBrainEnabled = false, EnemyBrainEnabled = false };
            w.Hero.DeepestActCleared = 0;
            int said = 0;
            for (int i = 0; i < 40; i++) { w.Tick(); said += w.Events.Count(e => e.Type == SimEventType.Letter && e.SkillId == "모래에 묻힌 왕"); }
            Assert.That(said, Is.EqualTo(1));
            Assert.That(w.UnreadLetters, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Class_fragments_go_only_to_their_class()
        {
            var w = new SimWorld(96, "windhunter");
            var ids = w.LettersFor().Select(l => l.Id).ToList();
            Assert.That(ids, Does.Contain("frag_wind"));
            Assert.That(ids, Does.Not.Contain("frag_ash"));
        }

        [Test]
        public void Records_survive_a_save()
        {
            var w = new SimWorld(97);
            w.Hero.Letters.Add("act1");
            w.MarkRead("act1");
            Assert.That(SaveSystem.TryDecode(SaveSystem.Encode(w.ToSave()), out var d), Is.True);
            var b = SimWorld.FromSave(d);
            Assert.That(b.Hero.Letters.Contains("act1") && b.Hero.LettersRead.Contains("act1"), Is.True);
        }
    }
}
