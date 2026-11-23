using System;

namespace Lanternkeeper.Sim
{
    /// <summary>Accumulates real time and fires fixed 10 Hz ticks (GDD §19.1).</summary>
    public sealed class SimClock
    {
        public const float TickSeconds = 0.1f;
        const int MaxTicksPerStep = 50; // avoid a spiral after a long frame hitch

        float _acc;

        /// <summary>Fraction (0..1) of the way to the next tick; View uses it to interpolate.</summary>
        public float Alpha => _acc / TickSeconds;

        public int Step(float dt, Action tick)
        {
            _acc += dt;
            int n = 0;
            while (_acc >= TickSeconds && n < MaxTicksPerStep)
            {
                _acc -= TickSeconds;
                tick();
                n++;
            }
            if (n == MaxTicksPerStep) _acc = 0f;
            return n;
        }
    }
}
