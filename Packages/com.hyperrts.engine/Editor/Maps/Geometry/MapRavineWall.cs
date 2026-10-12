using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>
    /// Where a ravine's wall stands: its distance from the line plus noise, so the height shaper's carving and the
    /// land planner's claims follow the same wobbly edge.
    /// </summary>
    public static class MapRavineWall
    {
        /// <summary>The noise channel the wobble is sampled from; the height shaper reuses it for river banks.</summary>
        public const int WobbleChannel = 1;

        private const float Wobble = 1.5f;
        private const float WobbleWavelength = 15f;

        /// <summary>Wobbled distance to the ravine line, infinity outside its reach.</summary>
        public static float Distance(MapLine line, MapNoise wobble, Vector2 point) => Distance(line, wobble, point, out _);

        /// <summary>Wobbled distance to the ravine line and the closest point's distance along it.</summary>
        public static float Distance(MapLine line, MapNoise wobble, Vector2 point, out float along)
        {
            var distance = line.Distance(point, out along);
            return float.IsPositiveInfinity(distance) ? distance : distance + wobble.Sample(point, WobbleWavelength) * Wobble;
        }
    }
}
