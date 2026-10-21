using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using Unity.Burst;
using Unity.Burst.Intrinsics;
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
        private BufferTypeHandle<WeaponMount> _mounts;
        private ComponentTypeHandle<Faction> _factions;
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _researched = state.GetBufferLookup<CompletedResearch>(true);
            _mounts = state.GetBufferTypeHandle<WeaponMount>();
            _factions = state.GetComponentTypeHandle<Faction>(true);
            _query = SystemAPI.QueryBuilder().WithAllRW<WeaponMount>().WithAll<Faction>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _researched.Update(ref state);
            _mounts.Update(ref state);
            _factions.Update(ref state);
            state.Dependency = new UnlockJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Researched = _researched,
                Mounts = _mounts,
                Factions = _factions,
            }.ScheduleParallel(_query, state.Dependency);
        }

        /// <summary>Writes a chunk only when a lock flips, so its change version bumps only then.</summary>
        [BurstCompile]
        private struct UnlockJob : IJobChunk
        {
            public PlayerRegistry Players;
            [ReadOnly] public BufferLookup<CompletedResearch> Researched;
            [ReadOnly] public ComponentTypeHandle<Faction> Factions;
            public BufferTypeHandle<WeaponMount> Mounts;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var factions = chunk.GetNativeArray(ref Factions);
                var accessor = chunk.GetBufferAccessorRO(ref Mounts);
                var writable = false;
                for (var e = 0; e < chunk.Count; e++)
                {
                    var known = Researched.TryGetBuffer(Players[factions[e].Value], out var researched);
                    var mounts = accessor[e];
                    for (var i = 0; i < mounts.Length; i++)
                    {
                        var mount = mounts[i];
                        if (mount.RequiredResearchTypeId == 0)
                        {
                            continue;
                        }

                        var unlocked = known && ResearchRules.IsDone(researched, mount.RequiredResearchTypeId);
                        if (mount.Unlocked == unlocked)
                        {
                            continue;
                        }

                        if (!writable)
                        {
                            accessor = chunk.GetBufferAccessorRW(ref Mounts);
                            mounts = accessor[e];
                            writable = true;
                        }

                        mount.Unlocked = unlocked;
                        mounts[i] = mount;
                    }
                }
            }
        }
    }
}
