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
            using var seen = Read();
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                segment.Valid = segment.Ground && !seen.Block(segment.Spot);
                segments[i] = segment;
            }
        }

        /// <summary>Whether a seen unit hostile to the local player stands on <paramref name="spot"/>.</summary>
        public readonly bool Block(in BuildingSpot spot)
        {
            using var seen = Read();
            return seen.Block(spot);
        }

        private readonly Snapshot Read() => new()
        {
            Relations = _relations.GetSingleton<FactionRelations>(),
            Faction = _local.GetSingleton<Player>().Faction,
            Agents = _units.ToComponentDataArray<NavAgent>(Allocator.Temp),
            Factions = _units.ToComponentDataArray<Faction>(Allocator.Temp),
            Transforms = _units.ToComponentDataArray<LocalTransform>(Allocator.Temp),
        };

        /// <summary>The seen units copied once, so a line of spots is checked against one read.</summary>
        private struct Snapshot : System.IDisposable
        {
            public FactionRelations Relations;
            public byte Faction;
            public NativeArray<NavAgent> Agents;
            public NativeArray<Faction> Factions;
            public NativeArray<LocalTransform> Transforms;

            public readonly bool Block(in BuildingSpot spot)
            {
                for (var i = 0; i < Agents.Length; i++)
                {
                    if (Agents[i].Layer == NavLayer.Air || !Relations.IsHostile(Faction, Factions[i].Value))
                    {
                        continue;
                    }

                    var axis = Agents[i].SegmentAxis(Transforms[i].Rotation);
                    if (spot.Overlaps(Transforms[i].Position.xz, axis, Agents[i].Radius))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Dispose()
            {
                Agents.Dispose();
                Factions.Dispose();
                Transforms.Dispose();
            }
        }
    }
}
