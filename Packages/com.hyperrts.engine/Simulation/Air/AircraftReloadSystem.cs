using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// The reload cycle: aircraft whose limited weapons (<see cref="Ammo"/>, mounts with rounds) run empty get one
    /// Return to Base order, which the player may override, and docked aircraft reload their
    /// <see cref="AmmoRefill.Docked"/> weapons, each <see cref="WeaponMount"/> at its own pace. Games refuel and
    /// repair docked aircraft the same way.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(OrderDispatchSystem))]
    [UpdateBefore(typeof(PadSystem))]
    public partial struct AircraftReloadSystem : ISystem
    {
        private OrderWriter _writer;
        private ComponentLookup<Ammo> _ammo;
        private BufferLookup<WeaponMount> _mounts;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _writer = new OrderWriter(ref state);
            _ammo = state.GetComponentLookup<Ammo>(true);
            _mounts = state.GetBufferLookup<WeaponMount>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _writer.Update(ref state);
            _ammo.Update(ref state);
            _mounts.Update(ref state);
            new ReturnJob { Writer = _writer, Ammos = _ammo, Mounts = _mounts }.Schedule();
            new ReloadJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
            new MountReloadJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        /// <summary>Single-threaded: the order writer's lookups aren't safe to share.</summary>
        [BurstCompile]
        [WithDisabled(typeof(Docked))]
        [WithNone(typeof(Dead))]
        private partial struct ReturnJob : IJobEntity
        {
            public OrderWriter Writer;
            [ReadOnly] public ComponentLookup<Ammo> Ammos;
            [ReadOnly] public BufferLookup<WeaponMount> Mounts;

            private void Execute(Entity entity, ref HomePad home)
            {
                if (!IsSpent(entity))
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

            /// <summary>Every weapon with limited rounds is empty, and it has at least one.</summary>
            private bool IsSpent(Entity entity)
            {
                var limited = Ammos.TryGetComponent(entity, out var ammo);
                if (limited && ammo.Current > 0)
                {
                    return false;
                }

                if (!Mounts.TryGetBuffer(entity, out var mounts))
                {
                    return limited;
                }

                foreach (var mount in mounts)
                {
                    if (!mount.IsLimited)
                    {
                        continue;
                    }

                    if (mount.HasRounds)
                    {
                        return false;
                    }

                    limited = true;
                }

                return limited;
            }
        }

        [BurstCompile]
        [WithAll(typeof(Docked))]
        private partial struct ReloadJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(ref Ammo ammo)
            {
                if (ammo.Refill == AmmoRefill.Docked)
                {
                    ammo.Reload(DeltaTime);
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(Docked))]
        private partial struct MountReloadJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(DynamicBuffer<WeaponMount> mounts)
            {
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    if (mount.Refill == AmmoRefill.Docked)
                    {
                        mount.Reload(DeltaTime);
                        mounts[i] = mount;
                    }
                }
            }
        }
    }
}
