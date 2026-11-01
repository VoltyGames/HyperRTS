using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Stamps every <see cref="Detector"/> into <see cref="FogOfWar.Detected"/> for its team. Single-threaded because
    /// overlapping circles OR into the same words. An <see cref="Offline"/> detector, such as a radar on low power,
    /// detects nothing.
    /// </summary>
    [BurstCompile]
    [WithNone(typeof(Offline))]
    internal partial struct DetectionStampJob : IJobEntity
    {
        public FogOfWar Fog;
        public FactionRelations Relations;

        private void Execute(in LocalTransform transform, in Detector detector, in Faction faction)
        {
            var team = Relations.GetTeam(faction.Value);
            if (detector.Radius > 0f && FogOfWar.Stamps(team))
            {
                Fog.StampCircle(Fog.Detected, transform.Position, detector.Radius, team);
            }
        }
    }
}
