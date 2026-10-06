using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Stamps every <see cref="Detector"/> into <see cref="FogOfWar.Detected"/> for its team. Single-threaded because
    /// overlapping circles OR into the same cells.
    /// </summary>
    [BurstCompile]
    internal partial struct DetectionStampJob : IJobEntity
    {
        public FogOfWar Fog;
        public FactionRelations Relations;

        private void Execute(in LocalTransform transform, in Detector detector, in Faction faction)
        {
            if (detector.Radius <= 0f || !FogOfWar.TryGetTeamBit(Relations.TeamOf(faction.Value), out var bit))
            {
                return;
            }

            Fog.StampCircle(Fog.Detected, transform.Position, detector.Radius, bit);
        }
    }
}
