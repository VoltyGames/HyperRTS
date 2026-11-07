using HyperRTS.Simulation.Common;
using Unity.Burst;
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
            new ReloadJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct ReloadJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(DynamicBuffer<WeaponMount> mounts) => WeaponMount.Reload(mounts, DeltaTime, docked: false);
        }
    }
}
