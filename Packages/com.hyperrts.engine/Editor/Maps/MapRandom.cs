using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HyperRTS.Editor.Maps
{
    /// <summary>Seeded random streams for map features, and the draws every planner makes from them.</summary>
    public static class MapRandom
    {
        /// <summary>The stream of one authored feature, the same on every build.</summary>
        public static System.Random Stream(int seed, MapRandomChannel channel, int index) =>
            new(unchecked(seed * 486187739 + (int)channel * 7919 + index * 104729));

        /// <summary>0 to 1.</summary>
        public static float Value(this System.Random random) => (float)random.NextDouble();

        public static float Range(this System.Random random, float min, float max) => Mathf.Lerp(min, max, random.Value());

        /// <summary>-1 to 1.</summary>
        public static float Signed(this System.Random random) => random.Value() * 2f - 1f;

        /// <summary>
        /// A whole count averaging <paramref name="expected"/>: the fraction is a chance of one more, so many small
        /// regions still get their share where rounding each would give none.
        /// </summary>
        public static int Count(this System.Random random, float expected)
        {
            var whole = Mathf.FloorToInt(expected);
            return random.Value() < expected - whole ? whole + 1 : whole;
        }

        /// <summary>A random facing, 0 to 360 degrees.</summary>
        public static float Yaw(this System.Random random) => random.Value() * 360f;

        /// <summary>One entry of a weighted mix.</summary>
        public static T Pick<T>(this System.Random random, IReadOnlyList<T> items, Func<T, float> weight)
        {
            var roll = random.Value() * items.Sum(weight);
            foreach (var item in items)
            {
                roll -= weight(item);
                if (roll <= 0f)
                {
                    return item;
                }
            }

            return items[items.Count - 1];
        }
    }
}
