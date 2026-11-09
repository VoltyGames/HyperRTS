using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>Reloads the <see cref="WeaponMount"/>s that refill anywhere, by the round or by the magazine. Docked refills are the air module's.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(WeaponFireSystem))]
    public partial struct AmmoRefillSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ReloadJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Mounts = SystemAPI.GetBufferLookup<WeaponMount>(),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct ReloadJob : IJobEntity
        {
            public float DeltaTime;

            // Written only for the unit being run, and only when a reload moves, so idle chunks keep their version.
            [NativeDisableParallelForRestriction]
            [NativeDisableContainerSafetyRestriction]
            public BufferLookup<WeaponMount> Mounts;

            private void Execute(Entity entity, in DynamicBuffer<WeaponMount> mounts)
            {
                if (WeaponMount.NeedsReload(mounts, docked: false))
                {
                    WeaponMount.Reload(Mounts[entity], DeltaTime, docked: false);
                }
            }
        }
    }
}
