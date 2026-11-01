using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Stamps the cells within the reveal distance of every ground viewer into <see cref="FogOfWar.Close"/> for its
    /// team, where concealed enemies show. Aircraft and passengers spot nothing up close. Single-threaded because
    /// overlapping circles OR into the same cells.
    /// </summary>
    [BurstCompile]
    internal partial struct CloseStampJob : IJobEntity
    {
        public FogOfWar Fog;
        public FactionRelations Relations;
        public float Radius;
        [ReadOnly] public ComponentLookup<NavAgent> Agents;
        [ReadOnly] public ComponentLookup<Inside> Inside;

        private void Execute(Entity entity, in LocalTransform transform, in VisionRange vision, in Faction faction)
        {
            if (Radius <= 0f || vision.Value <= 0f || !FogOfWar.TryGetTeamBit(Relations.GetTeam(faction.Value), out var bit))
            {
                return;
            }

            var airborne = Agents.TryGetComponent(entity, out var agent) && agent.Layer == NavLayer.Air;
            if (airborne || Inside.HasEnabled(entity))
            {
                return;
            }

            Fog.StampCircle(Fog.Close, transform.Position, Radius, bit);
        }
    }
}
