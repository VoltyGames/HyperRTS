using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Reloads <see cref="Ammo"/> and <see cref="WeaponMount"/>s that refill anywhere, by the round or by the
    /// magazine. Docked refills are the air module's.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(WeaponFireSystem))]
    public partial struct AmmoRefillSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            new AmmoJob { DeltaTime = deltaTime }.ScheduleParallel();
            new MountJob { DeltaTime = deltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct AmmoJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(ref Ammo ammo)
            {
                if (ammo.Refill != AmmoRefill.Docked)
                {
                    ammo.Reload(DeltaTime);
                }
            }
        }

        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct MountJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(DynamicBuffer<WeaponMount> mounts)
            {
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    if (mount.Refill != AmmoRefill.Docked)
                    {
                        mount.Reload(DeltaTime);
                        mounts[i] = mount;
                    }
                }
            }
        }
    }
}
