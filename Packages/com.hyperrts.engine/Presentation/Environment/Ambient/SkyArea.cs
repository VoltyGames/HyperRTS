using System;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>The ground under the sky ambient life roams: the baked heightfield's extent and heights.</summary>
    public readonly struct SkyArea
    {
        public readonly TerrainHeight Terrain;
        public readonly float2 Min;
        public readonly float2 Max;

        public SkyArea(in TerrainHeight terrain)
        {
            if (!terrain.IsCreated)
            {
                throw new ArgumentException("SkyArea needs a baked TerrainHeight.", nameof(terrain));
            }

            ref var field = ref terrain.Blob.Value;
            Terrain = terrain;
            Min = field.Min;
            Max = field.Min + (float2)(field.Size - 1) * field.Spacing;
        }

        public float Ground(float2 position) => Terrain.Height(position);

        /// <summary>A uniformly random point on the ground inside the area.</summary>
        public float3 RandomPoint(ref Random random)
        {
            var xz = random.NextFloat2(Min, Max);
            return new float3(xz.x, Ground(xz), xz.y);
        }
    }
}
