using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Spatial;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    public partial struct SkirmishAISystem
    {
        /// <summary>An owned builder offering the prefab, idle ones first, then nearest home. Never one mid-build.</summary>
        private Entity PickBuilder(ref SystemState state, in Turn turn, in Snapshot snapshot, Entity prefab)
        {
            var builders = snapshot.Builders;
            var best = new Candidate { Entity = Entity.Null };
            for (var i = 0; i < builders.Length; i++)
            {
                var builder = builders.Entities[i];
                if (!builders.IsOwnedBy(i, turn.Faction) || IsBuilding(ref state, builder))
                {
                    continue;
                }

                if (!ProductionRules.Offers(SystemAPI.GetBuffer<BuildOption>(builder), prefab))
                {
                    continue;
                }

                var candidate = new Candidate
                {
                    Entity = builder,
                    Idle = !SystemAPI.IsComponentEnabled<ActiveOrder>(builder),
                    Distance = builders.DistanceSq(i, turn.Home),
                };
                if (candidate.Beats(best))
                {
                    best = candidate;
                }
            }

            return best.Entity;
        }

        /// <summary>A builder ranked idle first, then by distance, then by entity index.</summary>
        private struct Candidate
        {
            public Entity Entity;
            public bool Idle;
            public float Distance;

            public readonly bool Beats(in Candidate other)
            {
                if (other.Entity == Entity.Null)
                {
                    return true;
                }

                if (Idle != other.Idle)
                {
                    return Idle;
                }

                return Closest.IsCloser(Distance, Entity, other.Distance, other.Entity);
            }
        }

        private bool IsBuilding(ref SystemState state, Entity builder)
        {
            if (!SystemAPI.IsComponentEnabled<ActiveOrder>(builder))
            {
                return false;
            }

            var order = SystemAPI.GetComponent<ActiveOrder>(builder).Value.Type;
            return order == OrderType.Build || order == OrderType.Repair;
        }

        /// <summary>Idle builders finish the AI's unfinished sites, e.g. after the builder that placed one died.</summary>
        private void ResumeSites(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            var builders = snapshot.Builders;
            for (var i = 0; i < builders.Length; i++)
            {
                var builder = builders.Entities[i];
                if (!builders.IsOwnedBy(i, turn.Faction) || SystemAPI.IsComponentEnabled<ActiveOrder>(builder))
                {
                    continue;
                }

                var site = turn.Busy.Contains(builder)
                    ? -1
                    : snapshot.Sites.NearestOwned(turn.Faction, builders.Position(i));
                if (site < 0)
                {
                    continue;
                }

                turn.Commands.Add(new PlayerCommand
                {
                    Type = CommandType.Build, Unit = builder, Target = snapshot.Sites.Entities[site],
                });
                turn.Busy.Add(builder);
            }
        }
    }
}
