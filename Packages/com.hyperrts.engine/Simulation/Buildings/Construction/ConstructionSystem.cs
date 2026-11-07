using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Reach;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings.Construction
{
    /// <summary>
    /// Runs Build orders: builders walk to an allied site and add <c>Rate / BuildTime</c> progress per second while
    /// in reach; a squad adds it for each living member and sends them to dig (<see cref="SquadWork"/>). Any site
    /// at full progress (built, or raised by a cheat or game effect) then completes: it disables
    /// <see cref="ConstructionProgress"/> and raises <see cref="BuildingCompleted"/>. Sites never progress on their own.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct ConstructionSystem : ISystem
    {
        private TargetReach _reach;
        private EventWriter<BuildingCompleted> _completed;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _reach = new TargetReach(ref state);
            state.RequireForUpdate<MapSettings>();
            _completed = new EventWriter<BuildingCompleted>(ref state);
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _reach.Update(ref state, SystemAPI.GetSingleton<MapSettings>());
            _completed.Update(ref state);

            new BuildJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                SiteLookup = SystemAPI.GetComponentLookup<ConstructionProgress>(false),
                ProducibleLookup = SystemAPI.GetComponentLookup<Producible>(true),
                Reach = _reach,
                FactionLookup = SystemAPI.GetComponentLookup<Faction>(true),
                CrewLookup = SystemAPI.GetBufferLookup<SquadMember>(true),
                WorkLookup = SystemAPI.GetComponentLookup<SquadWork>(false),
                ObstacleLookup = SystemAPI.GetComponentLookup<NavObstacle>(true),
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            }.Schedule();

            new FinishJob
            {
                Completed = _completed,
            }.Schedule();
        }

        /// <summary>Single-threaded so several builders can add to one site.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(ActiveOrder), typeof(MoveDestination), typeof(Arrival))]
        private partial struct BuildJob : IJobEntity
        {
            public float DeltaTime;
            public FactionRelations Relations;
            public ComponentLookup<ConstructionProgress> SiteLookup;
            [ReadOnly] public ComponentLookup<Producible> ProducibleLookup;
            public TargetReach Reach;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public BufferLookup<SquadMember> CrewLookup;
            public ComponentLookup<SquadWork> WorkLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            private void Execute(Entity builderEntity, in Builder builder, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                RefRW<MoveDestination> destination, EnabledRefRW<MoveDestination> moving, EnabledRefRW<Arrival> arrived,
                in LocalTransform transform, in NavAgent agent, in Faction faction)
            {
                WorkLookup.DisableIfPresent(builderEntity);
                if (!busy.ValueRO || order.Value.Type != OrderType.Build)
                {
                    return;
                }

                var site = order.Value.Target;
                if (!IsAlliedSite(site, faction.Value))
                {
                    ActiveOrder.Finish(busy, moving);
                    return;
                }

                var walk = Reach.Approach(site, transform, agent, new MoveControl(destination, moving, arrived));
                if (ActiveOrder.StopsShort(walk, busy, moving))
                {
                    return;
                }

                Dig(builderEntity, site);
                if (Advance(site, builder.Rate * Crew(builderEntity)))
                {
                    busy.ValueRW = false;
                }
            }

            /// <summary>A squad's work is its living members, each adding the builder's rate.</summary>
            private int Crew(Entity builder) =>
                CrewLookup.TryGetBuffer(builder, out var members) ? SquadMember.CountLiving(members) : 1;

            /// <summary>Sends a squad's members to dig along the site's long side.</summary>
            private void Dig(Entity builder, Entity site)
            {
                if (!WorkLookup.HasComponent(builder) || !ObstacleLookup.TryGetComponent(site, out var obstacle))
                {
                    return;
                }

                var place = TransformLookup[site];
                var along = math.rotate(place.Rotation, new float3(1f, 0f, 0f)).xz;
                WorkLookup[builder] = new SquadWork
                {
                    Center = place.Position.xz,
                    Along = math.normalizesafe(along, new float2(1f, 0f)),
                    Length = obstacle.Size.x,
                    HalfWidth = obstacle.Size.y * 0.5f,
                    Progress = SiteLookup[site].Value,
                };
                WorkLookup.SetComponentEnabled(builder, true);
            }

            private bool IsAlliedSite(Entity site, byte faction)
            {
                if (!Reach.Exists(site))
                {
                    return false;
                }

                return BuildingRules.IsAlliedSite(SiteLookup, FactionLookup, Relations, site, faction);
            }

            /// <summary>Adds this frame's work and returns true once the site is at full progress.</summary>
            private bool Advance(Entity site, float rate)
            {
                ref var progress = ref SiteLookup.GetRefRW(site).ValueRW;
                progress.Value += DeltaTime * rate / Producible.GetBuildTime(ProducibleLookup, site);
                if (progress.Value < 1f)
                {
                    return false;
                }

                progress.Value = 1f;
                return true;
            }
        }

        /// <summary>Completes every site at full progress, however it got there.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct FinishJob : IJobEntity
        {
            public EventWriter<BuildingCompleted> Completed;

            private void Execute(Entity site, ref ConstructionProgress progress,
                EnabledRefRW<ConstructionProgress> underConstruction, in Faction faction)
            {
                if (progress.Value < 1f)
                {
                    return;
                }

                underConstruction.ValueRW = false;
                Completed.Add(new BuildingCompleted { Building = site, Faction = faction.Value });
            }
        }
    }
}
