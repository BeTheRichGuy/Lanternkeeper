using Lanternkeeper.Sim;
using Lanternkeeper.View;
using NUnit.Framework;
using UnityEngine;

namespace Lanternkeeper.Tests
{
    /// <summary>GDD §18: sound follows Sim events and stays silent (never throws) for clips not made yet.</summary>
    public class AudioTests
    {
        [Test]
        public void Every_event_of_a_busy_fight_goes_through_the_sound_map()
        {
            var host = new GameObject("audio test");
            try
            {
                var w = new SimWorld(61);
                var audio = new AudioView(host.transform, w);
                for (int i = 0; i < 600; i++)
                {
                    w.Tick();
                    Assert.DoesNotThrow(() => audio.OnTick(w.Events));
                }
                audio.SetAct(1);
                audio.SetAct(0);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
