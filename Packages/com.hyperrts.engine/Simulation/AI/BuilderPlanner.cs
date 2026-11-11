using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Picks the AI's builders for new work and sends idle ones to unfinished sites.</summary>
    internal struct BuilderPlanner
    {
        private EntityQuery _builderQuery;
        private EntityQuery _siteQuery;
        private AIGroup _sites;

        /// <summary>Every builder this frame, whether idle or not.</summary>
        public AIGroup Builders;

        public static BuilderPlanner Create(ref SystemState state) => new()
        {
            _builderQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Builder, BuildOption, Owner, LocalTransform>()
                .WithPresent<ActiveOrder>().WithNone<Dead>().Build(ref state),
            _siteQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<BuildingTag, ConstructionProgress, Owner, LocalTransform>().WithNone<Dead>().Build(ref state),
        };

        public void Update()
        {
            Builders = AIGroup.From(_builderQuery);
            _sites = AIGroup.From(_siteQuery);
        }

        /// <summary>An owned builder offering the prefab, idle ones first, then nearest home. Never one mid-build.</summary>
        public readonly Entity PickBuilder(ref SystemState state, in AITurn turn, Entity prefab)
        {
            var entities = state.EntityManager;
            var best = new Candidate { Entity = Entity.Null };
            for (var i = 0; i < Builders.Length; i++)
            {
                var builder = Builders.Entities[i];
                if (!Builders.IsOwnedBy(i, turn.Owner) || IsBuilding(entities, builder))
                {
                    continue;
                }

                if (!PrefabOptions.Has(entities.GetBuffer<BuildOption>(builder), prefab))
                {
                    continue;
                }

                var candidate = new Candidate
                {
                    Entity = builder,
                    Idle = !entities.IsComponentEnabled<ActiveOrder>(builder),
                    Distance = Builders.DistanceSq(i, turn.Home),
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

        private static bool IsBuilding(EntityManager entities, Entity builder)
        {
            if (!entities.IsComponentEnabled<ActiveOrder>(builder))
            {
                return false;
            }

            var order = entities.GetComponentData<ActiveOrder>(builder).Value.Type;
            return order == OrderType.Build || order == OrderType.Repair;
        }

        /// <summary>Idle builders finish the AI's unfinished sites, e.g. after the builder that placed one died.</summary>
        public readonly void ResumeSites(ref SystemState state, in AITurn turn)
        {
            var entities = state.EntityManager;
            for (var i = 0; i < Builders.Length; i++)
            {
                var builder = Builders.Entities[i];
                if (!Builders.IsOwnedBy(i, turn.Owner) || entities.IsComponentEnabled<ActiveOrder>(builder))
                {
                    continue;
                }

                var site = turn.Busy.Contains(builder)
                    ? -1
                    : _sites.NearestOwned(turn.Owner, Builders.Position(i));
                if (site < 0)
                {
                    continue;
                }

                turn.Commands.Add(new PlayerCommand
                {
                    Type = CommandType.Build, Unit = builder, Target = _sites.Entities[site],
                });
                turn.Busy.Add(builder);
            }
        }
    }
}
