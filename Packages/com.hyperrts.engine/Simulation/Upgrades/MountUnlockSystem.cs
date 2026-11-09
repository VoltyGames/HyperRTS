using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>
    /// Unlocks each research-gated <see cref="WeaponMount"/> while its owner has the research, and locks it again under
    /// an owner without it. Lives here because Combat sits below Production.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    [UpdateAfter(typeof(UpgradeSystem))]
    public partial struct MountUnlockSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<PlayerRegistry>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new UnlockJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Researched = SystemAPI.GetBufferLookup<CompletedResearch>(true),
                Mounts = SystemAPI.GetBufferLookup<WeaponMount>(),
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct UnlockJob : IJobEntity
        {
            public PlayerRegistry Players;
            [ReadOnly] public BufferLookup<CompletedResearch> Researched;

            // Written only for the unit being run, and only when a lock flips, so its change version bumps only then.
            [NativeDisableParallelForRestriction]
            [NativeDisableContainerSafetyRestriction]
            public BufferLookup<WeaponMount> Mounts;

            private void Execute(Entity entity, in DynamicBuffer<WeaponMount> mounts, in Owner owner)
            {
                var known = Researched.TryGetBuffer(Players[owner.Value], out var researched);
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    if (mount.RequiredResearchTypeId == 0)
                    {
                        continue;
                    }

                    var unlocked = known && ResearchRules.IsDone(researched, mount.RequiredResearchTypeId);
                    if (mount.Unlocked != unlocked)
                    {
                        mount.Unlocked = unlocked;
                        Mounts[entity].ElementAt(i) = mount;
                    }
                }
            }
        }
    }
}
