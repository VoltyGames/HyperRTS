using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Scatters trees of a per-hectare mix over an area of the authored sector, apart from each other and from
    /// everything placed, and plants them in every copy.
    /// </summary>
    public static class MapTreeScatter
    {
        private const int Attempts = 6;

        /// <summary>
        /// <paramref name="candidate"/> draws a sector point; <paramref name="inside"/> says whether a tree may grow
        /// there (in the area and in the sector).
        /// </summary>
        public static int Scatter(MapPlanContext context, IReadOnlyList<MapTreeDensity> mix, float hectares,
            float spacing, System.Random random, Func<Vector2> candidate, Func<Vector2, bool> inside)
        {
            var planted = 0;
            foreach (var density in mix)
            {
                var tree = context.Climate.TreeIndex(density.tree);
                var wanted = Mathf.RoundToInt(density.perHectare * hectares);
                planted += Plant(context, tree, wanted, spacing, random, candidate, inside);
            }

            return planted;
        }

        private static int Plant(MapPlanContext context, int tree, int wanted, float spacing, System.Random random,
            Func<Vector2> candidate, Func<Vector2, bool> inside)
        {
            var planted = 0;
            var misses = 0;
            while (planted < wanted && misses < wanted * Attempts + Attempts)
            {
                var point = candidate();
                if (!inside(point) || !context.Spacing.Fits(point, spacing))
                {
                    misses++;
                    continue;
                }

                context.PlantCopies(tree, point, context.TreeScale(tree, random), random.Yaw());
                planted++;
            }

            return planted;
        }
    }
}
