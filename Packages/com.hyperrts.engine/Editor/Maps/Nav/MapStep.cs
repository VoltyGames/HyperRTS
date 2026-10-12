using Unity.Mathematics;

namespace HyperRTS.Editor.Maps.Nav
{
    /// <summary>The 8-way step rules shared by <see cref="MapPathfinder"/> and <see cref="MapWalkField"/>.</summary>
    internal static class MapStep
    {
        public const float Diagonal = 1.41421356f;

        /// <summary>The runtime's octile distance to a goal, slightly inflated so A* prefers fewer expansions.</summary>
        public static float Heuristic(int2 cell, int2 goal)
        {
            var d = math.abs(goal - cell);
            return (math.cmax(d) + (Diagonal - 1f) * math.cmin(d)) * 1.001f;
        }

        /// <summary>
        /// Whether <paramref name="to"/> is a walkable neighbour of <paramref name="from"/> and not in
        /// <paramref name="closed"/> (by cell index, may be null). A diagonal step needs both side cells open too.
        /// </summary>
        public static bool CanStep(MapNavGrid nav, int2 from, int2 to, bool[] closed)
        {
            if (!Open(nav, to, closed))
            {
                return false;
            }

            var diagonal = from.x != to.x && from.y != to.y;
            if (!diagonal)
            {
                return true;
            }

            return Open(nav, new int2(from.x, to.y), closed) && Open(nav, new int2(to.x, from.y), closed);
        }

        public static float Cost(int2 from, int2 to) => from.x != to.x && from.y != to.y ? Diagonal : 1f;

        private static bool Open(MapNavGrid nav, int2 cell, bool[] closed)
        {
            if (!nav.IsWalkable(cell))
            {
                return false;
            }

            return closed == null || !closed[nav.Grid.Index(cell)];
        }
    }
}
