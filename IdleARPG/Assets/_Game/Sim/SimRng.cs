namespace Lanternkeeper.Sim
{
    /// <summary>Deterministic xorshift128+ RNG. Same seed, same sequence on every machine.</summary>
    public sealed class SimRng
    {
        ulong _s0, _s1;

        public SimRng(ulong seed)
        {
            _s0 = SplitMix(ref seed);
            _s1 = SplitMix(ref seed);
            if (_s0 == 0 && _s1 == 0) _s1 = 1;
        }

        static ulong SplitMix(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong NextULong()
        {
            ulong s1 = _s0, s0 = _s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return _s1 + s0;
        }

        public uint NextUInt() => (uint)(NextULong() >> 32);

        /// <summary>[0, 1)</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>[minIncl, maxExcl)</summary>
        public int Range(int minIncl, int maxExcl)
        {
            if (maxExcl <= minIncl) return minIncl;
            return minIncl + (int)(NextUInt() % (uint)(maxExcl - minIncl));
        }

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        public bool Chance(float p) => NextFloat() < p;

        /// <summary>Raw state for save/load ("s0:s1" in hex), so a loaded game continues the same stream.</summary>
        public string State
        {
            get => _s0.ToString("x16") + ":" + _s1.ToString("x16");
            set
            {
                var parts = value.Split(':');
                _s0 = System.Convert.ToUInt64(parts[0], 16);
                _s1 = System.Convert.ToUInt64(parts[1], 16);
                if (_s0 == 0 && _s1 == 0) _s1 = 1;
            }
        }
    }
}
