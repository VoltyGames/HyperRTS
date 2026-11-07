using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Reach;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>
    /// Runs Capture orders: capturers walk to the building and add progress. A different player starting over resets it.
    /// On completion the building changes owner at the end of the frame (<see cref="OwnerChanged.Transfer"/>).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct CaptureSystem : ISystem
    {
        private CaptureLookup _targets;
        private TargetReach _reach;
        private EventWriter<OwnerChanged> _ownerChanges;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new CaptureLookup(ref state);
            _reach = new TargetReach(ref state);
            state.RequireForUpdate<MapSettings>();
            _ownerChanges = new EventWriter<OwnerChanged>(ref state);
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state);
            _reach.Update(ref state, SystemAPI.GetSingleton<MapSettings>());
            _ownerChanges.Update(ref state);

            new CaptureJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Targets = _targets,
                CapturableLookup = SystemAPI.GetComponentLookup<Capturable>(true),
                ProgressLookup = SystemAPI.GetComponentLookup<CaptureProgress>(false),
                OwnerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                Reach = _reach,
                OwnerChanges = _ownerChanges,
            }.Schedule();
        }

        /// <summary>Single-threaded so several capturers can work on one building.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        [WithPresent(typeof(MoveDestination), typeof(Arrival))]
        private partial struct CaptureJob : IJobEntity
        {
            public float DeltaTime;
            public OwnerRelations Relations;
            public EntityCommandBuffer Ecb;
            public CaptureLookup Targets;
            [ReadOnly] public ComponentLookup<Capturable> CapturableLookup;
            public ComponentLookup<CaptureProgress> ProgressLookup;
            [ReadOnly] public ComponentLookup<Owner> OwnerLookup;
            public TargetReach Reach;
            public EventWriter<OwnerChanged> OwnerChanges;

            private void Execute(Entity entity, in Capturer capturer, ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy,
                RefRW<MoveDestination> destination, EnabledRefRW<MoveDestination> moving, EnabledRefRW<Arrival> arrived,
                in LocalTransform transform, in NavAgent agent)
            {
                if (!busy.ValueRO || order.Value.Type != OrderType.Capture)
                {
                    return;
                }

                var target = order.Value.Target;
                var owner = OwnerLookup[entity].Value;
                if (!Targets.CanCapture(target, owner, Relations))
                {
                    ActiveOrder.Finish(busy, moving);
                    return;
                }

                var walk = Reach.Approach(target, transform, agent, new MoveControl(destination, moving, arrived));
                if (ActiveOrder.StopsShort(walk, busy, moving))
                {
                    return;
                }

                if (!Advance(target, owner, capturer.Rate))
                {
                    return;
                }

                OwnerChanged.Transfer(Ecb, ref OwnerChanges, target, OwnerLookup[target].Value, owner);
                busy.ValueRW = false;
                if (capturer.ConsumedOnCapture)
                {
                    Ecb.DestroyEntity(entity);
                }
            }

            /// <summary>Adds this frame's work and returns true once the capture completes.</summary>
            private bool Advance(Entity target, byte owner, float rate)
            {
                ref var progress = ref ProgressLookup.GetRefRW(target).ValueRW;
                if (progress.Owner != owner)
                {
                    progress = new CaptureProgress { Owner = owner };
                }

                progress.Value += DeltaTime * rate / CapturableLookup[target].CaptureTime;
                if (progress.Value < 1f)
                {
                    return false;
                }

                progress = default;
                return true;
            }
        }
    }
}
