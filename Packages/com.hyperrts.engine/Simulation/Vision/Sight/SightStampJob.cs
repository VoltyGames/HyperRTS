using HyperRTS.Simulation.Navigation.Heights;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>Single-threaded: overlapping blocks raise the same cells.</summary>
    [BurstCompile]
    internal partial struct SightStampJob : IJobEntity
    {
        public SightBlockers Blockers;
        public TerrainHeight Terrain;

        private void Execute(Entity entity, in SightBlock block, in LocalTransform transform)
        {
            using var marker = SightBlockSystem.StampMarker.Auto();
            Blockers.Stamp(entity, block, transform, Terrain);
        }
    }
}
