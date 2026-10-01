using System;

namespace SyntheticData.Generation
{
    public sealed class SeededRandomSource : IRandomSource
    {
        private readonly Random random;

        public SeededRandomSource(int seed)
        {
            random = new Random(seed);
        }

        public int Range(int minimum, int maximumExclusive)
        {
            return random.Next(minimum, maximumExclusive);
        }

        public float Range(float minimum, float maximum)
        {
            if (maximum < minimum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximum),
                    "Maximum must be greater than or equal to minimum.");
            }

            return minimum + ((float)random.NextDouble() * (maximum - minimum));
        }
    }
}
