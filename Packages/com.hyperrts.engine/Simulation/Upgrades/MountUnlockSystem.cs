using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using Unity.Burst;
using Unity.Collections;
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
        private BufferLookup<CompletedResearch> _researched;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _researched = state.GetBufferLookup<CompletedResearch>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _researched.Update(ref state);
            new UnlockJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Researched = _researched,
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct UnlockJob : IJobEntity
        {
            public PlayerRegistry Players;
            [ReadOnly] public BufferLookup<CompletedResearch> Researched;

            private void Execute(DynamicBuffer<WeaponMount> mounts, in Faction faction)
            {
                var known = Researched.TryGetBuffer(Players[faction.Value], out var researched);
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    if (mount.RequiredResearchTypeId == 0)
                    {
                        continue;
                    }

                    mount.Unlocked = known && ResearchRules.IsDone(researched, mount.RequiredResearchTypeId);
                    mounts[i] = mount;
                }
            }
        }
    }
}
