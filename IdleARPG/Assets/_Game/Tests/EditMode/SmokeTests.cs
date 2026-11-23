using Lanternkeeper.Sim;
using NUnit.Framework;

namespace Lanternkeeper.Tests
{
    public class SmokeTests
    {
        [Test]
        public void Sim_assembly_loads()
        {
            Assert.That(typeof(SimRng).Assembly.GetName().Name, Is.EqualTo("Game.Sim"));
        }
    }
}
