using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Vision.Detection;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision.Fog
{
    /// <summary>
    /// Stamps every <see cref="Detector"/> into <see cref="FogOfWar.Detected"/> for its team; single-threaded because
    /// overlapping circles OR into the same words. An <see cref="Offline"/> detector detects nothing.
    /// </summary>
    [BurstCompile]
    [WithNone(typeof(Offline))]
    internal partial struct DetectionStampJob : IJobEntity
    {
        public FogOfWar Fog;
        public OwnerRelations Relations;

        private void Execute(in LocalTransform transform, in Detector detector, in Owner owner)
        {
            var team = Relations.GetTeam(owner.Value);
            if (detector.Radius > 0f && FogOfWar.Stamps(team))
            {
                Fog.StampCircle(Fog.Detected, transform.Position, detector.Radius, team);
            }
        }
    }
}
