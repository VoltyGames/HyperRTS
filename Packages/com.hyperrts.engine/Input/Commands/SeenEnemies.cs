using HyperRTS.Client.Interaction;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Input.Commands
{
    /// <summary>
    /// The hostile ground and naval units the local player can see, which make a placement spot invalid. Hidden ones
    /// are left to the server, so the ghost never gives them away.
    /// </summary>
    public struct SeenEnemies
    {
        private EntityQuery _units;
        private EntityQuery _relations;
        private EntityQuery _local;

        public SeenEnemies(ref SystemState state)
        {
            _units = new EntityQueryBuilder(Allocator.Temp).WithAll<NavAgent, Faction, LocalTransform, Health>()
                .WithNone<Dead, Inside, FogHidden>().Build(ref state);
            _relations = new EntityQueryBuilder(Allocator.Temp).WithAll<FactionRelations>().Build(ref state);
            _local = new EntityQueryBuilder(Allocator.Temp).WithAll<LocalPlayer, Player>().Build(ref state);
        }

        /// <summary>Units move every frame, so each segment's cached cell check is joined with them again.</summary>
        public readonly void Mark(DynamicBuffer<PlacementSegment> segments)
        {
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                segment.Valid = segment.Ground && !Block(segment.Spot);
                segments[i] = segment;
            }
        }

        /// <summary>Whether a seen unit hostile to the local player stands on <paramref name="spot"/>.</summary>
        public readonly bool Block(in BuildingSpot spot)
        {
            var relations = _relations.GetSingleton<FactionRelations>();
            var faction = _local.GetSingleton<Player>().Faction;
            using var agents = _units.ToComponentDataArray<NavAgent>(Allocator.Temp);
            using var factions = _units.ToComponentDataArray<Faction>(Allocator.Temp);
            using var transforms = _units.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (var i = 0; i < agents.Length; i++)
            {
                if (agents[i].Layer == NavLayer.Air || !relations.IsHostile(faction, factions[i].Value))
                {
                    continue;
                }

                var axis = agents[i].SegmentAxis(transforms[i].Rotation);
                if (spot.Overlaps(transforms[i].Position.xz, axis, agents[i].Radius))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
