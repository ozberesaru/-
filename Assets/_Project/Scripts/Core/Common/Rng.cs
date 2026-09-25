using System;

namespace Srpg.Core
{
    [Serializable]
    public sealed class Rng
    {
        public ulong State;

        public Rng(ulong seed)
        {
            State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public ulong NextULong()
        {
            ulong x = State;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            State = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong span = (ulong)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextULong() % span);
        }

        public bool Chance(int percent)
        {
            return Range(0, 100) < percent;
        }
    }
}
