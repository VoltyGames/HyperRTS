using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Singleton on the <see cref="FogOfWar"/> grid: how far solid <see cref="SightBlock"/>s rise above each cell's
    /// ground, in <see cref="Step"/> metre steps.
    /// </summary>
    public struct SightBlockers : IComponentData
    {
        public const float Step = 0.25f;

        public NativeArray<byte> Solid;
        public int2 Size;
        public float2 Min;
        public float CellSize;

        /// <summary>Bumped whenever the blockers are restamped.</summary>
        public int Version;

        public readonly bool IsCreated => Solid.IsCreated;

        public readonly float SolidHeight(int index) => Solid[index] * Step;

        /// <summary>Raises every cell the box touches to <paramref name="top"/>, rounded up to a step.</summary>
        public void Stamp(in NavBox box, float top, in TerrainHeight terrain)
        {
            var extents = box.Extents;
            var min = math.max((int2)math.floor((box.Center - extents - Min) / CellSize), 0);
            var max = math.min((int2)math.floor((box.Center + extents - Min) / CellSize), Size - 1);
            var half = new float2(CellSize * 0.5f);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var center = Min + (new float2(x, y) + 0.5f) * CellSize;
                    // Thin walls and fences block every cell they cross, not only the ones whose centre they cover.
                    if (!box.Overlaps(new NavBox(new float3(center.x, 0f, center.y), quaternion.identity, half * 2f)))
                    {
                        continue;
                    }

                    var steps = (byte)math.clamp(math.ceil((top - terrain.Height(center)) / Step), 0f, byte.MaxValue);
                    var index = y * Size.x + x;
                    Solid[index] = (byte)math.max((int)Solid[index], steps);
                }
            }
        }
    }
}
