using System.Collections.Generic;
using System.Linq;

namespace HyperRTS.Presentation.Common
{
    /// <summary>Turns 32-bit hashes into the random numbers effects, fires and plants draw from.</summary>
    public static class Hashes
    {
        /// <summary>A hash as a number from 0 up to but not including 1, from its top 24 bits.</summary>
        public static float Fraction(uint hash) => (hash >> 8) * (1f / (1 << 24));

        /// <summary>An index into <paramref name="weights"/> picked by weight: the first whose running sum passes the hash.</summary>
        public static int Pick(uint hash, IReadOnlyList<float> weights)
        {
            var roll = Fraction(hash) * weights.Sum();
            for (var i = 0; i < weights.Count - 1; i++)
            {
                roll -= weights[i];
                if (roll < 0f)
                {
                    return i;
                }
            }

            return weights.Count - 1;
        }
    }
}
