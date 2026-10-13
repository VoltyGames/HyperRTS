using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>Jitter for land-use border lookups, shared so grass ends where its ground texture does.</summary>
    public sealed class MapEdgeJitter
    {
        private const float Wavelength = 6f;
        private const float Amplitude = 1.4f;
        private static readonly Vector2 SecondAxis = new(311f, 173f);
        private readonly MapNoise _noise;

        public MapEdgeJitter(MapLayout layout)
        {
            _noise = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.LandEdges);
        }

        public Vector2 Offset(Vector2 point) =>
            new Vector2(_noise.Sample(point, Wavelength), _noise.Sample(point + SecondAxis, Wavelength)) * Amplitude;
    }
}
