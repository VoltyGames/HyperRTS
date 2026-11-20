using System;
using HyperRTS.Simulation.Cues.Surfaces;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>Finds the nearest ground of a material on the map's ground material grid.</summary>
    internal readonly struct GroundSearch
    {
        private readonly GroundMaterials _ground;

        public GroundSearch(EntityManager entityManager)
        {
            if (!PlayWorld.TryGetSingleton(entityManager, out _ground))
            {
                throw new InvalidOperationException("HyperRTS: the map has no ground materials to search.");
            }
        }

        /// <summary>The cell centre of <paramref name="material"/> nearest <paramref name="around"/> within <paramref name="radius"/> metres.</summary>
        public float2 Nearest(float2 around, SurfaceMaterial material, float radius)
        {
            var cell = _ground.Blob.Value.Grid.CellSize;
            var best = float.MaxValue;
            var found = around;
            for (var x = -radius; x <= radius; x += cell)
            {
                for (var z = -radius; z <= radius; z += cell)
                {
                    var spot = around + new float2(x, z);
                    var distance = math.length(spot - around);
                    if (distance < best && distance <= radius && _ground.At(spot) == material)
                    {
                        best = distance;
                        found = spot;
                    }
                }
            }

            return best < float.MaxValue
                ? found
                : throw new InvalidOperationException($"HyperRTS: no {material} ground within {radius} m of ({around.x}, {around.y}).");
        }
    }
}
