using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
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
    /// Runs Build orders: builders walk to an allied site and add progress while in reach, and a squad adds it for each
    /// living member. A site at full progress completes and raises <see cref="BuildingCompleted"/>. A builder that
    /// finishes a line piece with nothing queued goes on to the nearest unfinished piece of the same wall.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct ConstructionSystem : ISystem
    {
        private TargetReach _reach;
        private EventWriter<BuildingCompleted> _completed;
        private EntityQuery _lines;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _reach = new TargetReach(ref state);
            // Dead pieces are skipped in the job: a gather filtering on Dead would read it untracked.
            _lines = SystemAPI.QueryBuilder().WithAll<LinePlacement, NavObstacle, LocalTransform, Owner>().Build();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<NavGrid>();
            _completed = new EventWriter<BuildingCompleted>(ref state);
            state.RequireForUpdate<OwnerRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _reach.Update(ref state, SystemAPI.GetSingleton<MapSettings>());
            _completed.Update(ref state);

            var lines = _lines.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var listed);
            state.Dependency = listed;
            new BuildJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
                SiteLookup = SystemAPI.GetComponentLookup<ConstructionProgress>(false),
                ProducibleLookup = SystemAPI.GetComponentLookup<Producible>(true),
                Reach = _reach,
                OwnerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                CrewLookup = SystemAPI.GetBufferLookup<SquadMember>(true),
                WorkLookup = SystemAPI.GetComponentLookup<SquadWork>(false),
                ObstacleLookup = SystemAPI.GetComponentLookup<NavObstacle>(true),
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                QueueLookup = SystemAPI.GetBufferLookup<QueuedOrder>(true),
                LineLookup = SystemAPI.GetComponentLookup<LinePlacement>(true),
                DeadLookup = SystemAPI.GetComponentLookup<Dead>(true),
                Lines = lines.AsDeferredJobArray(),
                Grid = SystemAPI.GetSingleton<NavGrid>(),
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
            public OwnerRelations Relations;
            public ComponentLookup<ConstructionProgress> SiteLookup;
            [ReadOnly] public ComponentLookup<Producible> ProducibleLookup;
            public TargetReach Reach;
            [ReadOnly] public NavGrid Grid;
            [ReadOnly] public ComponentLookup<Owner> OwnerLookup;
            [ReadOnly] public BufferLookup<SquadMember> CrewLookup;
            public ComponentLookup<SquadWork> WorkLookup;
            [ReadOnly] public ComponentLookup<NavObstacle> ObstacleLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public BufferLookup<QueuedOrder> QueueLookup;
            [ReadOnly] public ComponentLookup<LinePlacement> LineLookup;
            [ReadOnly] public ComponentLookup<Dead> DeadLookup;

            /// <summary>Every standing line piece, built or not.</summary>
            [ReadOnly] public NativeArray<Entity> Lines;

            private void Execute(Entity builderEntity, in Builder builder, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                RefRW<MoveDestination> destination, EnabledRefRW<MoveDestination> moving, EnabledRefRW<Arrival> arrived,
                in LocalTransform transform, in NavAgent agent, in Owner owner)
            {
                WorkLookup.DisableIfPresent(builderEntity);
                if (!busy.ValueRO || order.Value.Type != OrderType.Build)
                {
                    return;
                }

                var site = order.Value.Target;
                if (!IsAlliedSite(site, owner.Value))
                {
                    ActiveOrder.Finish(busy, moving);
                    return;
                }

                var walk = Reach.Approach(site, transform, agent,
                    new MoveControl(destination, moving, arrived), Grid);
                if (ActiveOrder.StopsShort(walk, busy, moving))
                {
                    return;
                }

                Dig(builderEntity, site);
                if (!Advance(site, builder.Rate * Crew(builderEntity)))
                {
                    return;
                }

                var next = QueueLookup[builderEntity].IsEmpty ? NextLinePiece(site, transform.Position) : Entity.Null;
                if (next == Entity.Null)
                {
                    busy.ValueRW = false;
                    return;
                }

                var position = TransformLookup[next].Position;
                order.Value = new Order { Type = OrderType.Build, Target = next, Position = position };
            }

            /// <summary>
            /// The unfinished piece nearest <paramref name="from"/> of the wall <paramref name="site"/> belongs to: the
            /// owner's line pieces joined to it edge to edge, built or not, so a resumed wall is built to its ends.
            /// </summary>
            private Entity NextLinePiece(Entity site, float3 from)
            {
                if (!LineLookup.HasComponent(site))
                {
                    return Entity.Null;
                }

                var owner = OwnerLookup[site].Value;
                var wall = new NativeList<Entity>(Allocator.Temp);
                var boxes = new NativeList<NavBox>(Allocator.Temp);
                foreach (var piece in Lines)
                {
                    if (piece == site || OwnerLookup[piece].Value != owner || DeadLookup.IsComponentEnabled(piece))
                    {
                        continue;
                    }

                    wall.Add(piece);
                    boxes.Add(Reach.Footprint(piece));
                }

                var joined = new NativeArray<bool>(wall.Length, Allocator.Temp);
                var reached = new NativeList<NavBox>(Allocator.Temp) { Reach.Footprint(site) };
                var best = Entity.Null;
                var bestDistance = float.MaxValue;
                for (var r = 0; r < reached.Length; r++)
                {
                    var box = reached[r];
                    for (var i = 0; i < wall.Length; i++)
                    {
                        if (joined[i] || !IsJoined(box, boxes[i]))
                        {
                            continue;
                        }

                        joined[i] = true;
                        reached.Add(boxes[i]);
                        var distance = math.distancesq(from.xz, boxes[i].Center);
                        if (IsUnfinished(wall[i]) && distance < bestDistance)
                        {
                            best = wall[i];
                            bestDistance = distance;
                        }
                    }
                }

                return best;
            }

            private static bool IsJoined(in NavBox a, in NavBox b)
            {
                var reach = math.length(a.Half) + math.length(b.Half) + BuildingSpot.FlushTolerance;
                return math.distancesq(a.Center, b.Center) <= reach * reach
                       && a.Gap(b) <= BuildingSpot.FlushTolerance;
            }

            private bool IsUnfinished(Entity piece) =>
                SiteLookup.IsComponentEnabled(piece) && SiteLookup[piece].Value < 1f;

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

            private bool IsAlliedSite(Entity site, byte owner)
            {
                if (!Reach.Exists(site))
                {
                    return false;
                }

                return BuildingRules.IsAlliedSite(SiteLookup, OwnerLookup, Relations, site, owner);
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
                EnabledRefRW<ConstructionProgress> underConstruction, in Owner owner)
            {
                if (progress.Value < 1f)
                {
                    return;
                }

                underConstruction.ValueRW = false;
                Completed.Add(new BuildingCompleted { Building = site, Owner = owner.Value });
            }
        }
    }
}
