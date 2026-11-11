using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// Sends aircraft with all limited weapons empty back to base, and reloads docked aircraft.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(OrderDispatchSystem))]
    [UpdateBefore(typeof(PadSystem))]
    public partial struct AircraftReloadSystem : ISystem
    {
        private OrderWriter _writer;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _writer = new OrderWriter(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _writer.Update(ref state, SystemAPI.GetSingleton<OrderTraitTable>());
            new ReturnJob { Writer = _writer, Mounts = SystemAPI.GetBufferLookup<WeaponMount>(true) }.Schedule();
            new ReloadJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        /// <summary>Single-threaded: the order writer's lookups aren't safe to share.</summary>
        [BurstCompile]
        [WithDisabled(typeof(Docked))]
        [WithNone(typeof(Dead))]
        private partial struct ReturnJob : IJobEntity
        {
            public OrderWriter Writer;
            [ReadOnly] public BufferLookup<WeaponMount> Mounts;

            private void Execute(Entity entity, ref HomePad home)
            {
                var spent = Mounts.TryGetBuffer(entity, out var mounts) && WeaponMount.IsSpent(mounts);
                if (!spent)
                {
                    home.Recalled = false;
                    return;
                }

                if (home.Airfield == Entity.Null || home.Recalled)
                {
                    return;
                }

                home.Recalled = true;
                if (!Writer.IsExecuting(entity, OrderType.ReturnToBase))
                {
                    Writer.Issue(entity, new Order { Type = OrderType.ReturnToBase }, false);
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(Docked))]
        private partial struct ReloadJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(DynamicBuffer<WeaponMount> mounts) => WeaponMount.Reload(mounts, DeltaTime, docked: true);
        }
    }
}
