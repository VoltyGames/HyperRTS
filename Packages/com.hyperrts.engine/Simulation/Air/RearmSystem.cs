using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// The rearm cycle: aircraft out of <see cref="Ammo"/> get a Return to Base order, and docked aircraft reload one
    /// round per <see cref="Ammo.ReloadTime"/>. Games refuel and repair docked aircraft the same way.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(OrderDispatchSystem))]
    [UpdateBefore(typeof(PadSystem))]
    public partial struct RearmSystem : ISystem
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
            _writer.Update(ref state);
            new ReturnJob { Writer = _writer }.Schedule();
            new ReloadJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        /// <summary>Single-threaded: the order writer's lookups aren't safe to share.</summary>
        [BurstCompile]
        [WithDisabled(typeof(Docked))]
        [WithNone(typeof(Dead))]
        private partial struct ReturnJob : IJobEntity
        {
            public OrderWriter Writer;

            private void Execute(Entity entity, in Ammo ammo, in HomePad home)
            {
                if (ammo.Current > 0 || home.Airfield == Entity.Null)
                {
                    return;
                }

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

            private void Execute(ref Ammo ammo)
            {
                if (ammo.Current >= ammo.Max)
                {
                    ammo.ReloadElapsed = 0f;
                    return;
                }

                ammo.ReloadElapsed += DeltaTime;
                if (ammo.ReloadElapsed >= ammo.ReloadTime)
                {
                    ammo.ReloadElapsed -= ammo.ReloadTime;
                    ammo.Current++;
                }
            }
        }
    }
}
