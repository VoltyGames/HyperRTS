using System.Collections.Generic;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>A river in map space: its channel line and a width that swells and narrows along it.</summary>
    public sealed class MapRiver
    {
        private const float WidthWavelength = 70f;
        private const float PinReach = 30f;
        private readonly MapNoise _width;
        private readonly List<Vector2> _pins = new();

        public MapRiver(MapLine line, MapRiverSpec spec, MapNoise width)
        {
            Line = line;
            Spec = spec;
            _width = width;
        }

        public MapLine Line { get; }

        public MapRiverSpec Spec { get; }

        /// <summary>Keeps the authored width round a point (a crossing), so bridges and fords fit the river.</summary>
        public void Pin(Vector2 point) => _pins.Add(point);

        /// <summary>
        /// Half the channel width near a point. The noise is symmetric, so every copy of the river varies alike.
        /// </summary>
        public float HalfWidth(Vector2 point)
        {
            var variation = Spec.widthVariation;
            foreach (var pin in _pins)
            {
                variation *= MapGeometry.Smooth((pin - point).magnitude / PinReach);
            }

            var swell = 1f + variation * _width.Sample(point, WidthWavelength);
            return Spec.width * 0.5f * swell;
        }

        /// <summary>Distance to the water's edge (negative over the water), or infinity outside the river's reach.</summary>
        public float EdgeDistance(Vector2 point)
        {
            var distance = Line.Distance(point);
            return float.IsPositiveInfinity(distance) ? distance : distance - HalfWidth(point);
        }
    }
}
