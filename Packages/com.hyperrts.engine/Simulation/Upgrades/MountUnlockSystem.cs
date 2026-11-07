using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
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
        private BufferTypeHandle<WeaponMount> _mounts;
        private ComponentTypeHandle<Owner> _owners;
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _mounts = state.GetBufferTypeHandle<WeaponMount>();
            _owners = state.GetComponentTypeHandle<Owner>(true);
            _query = SystemAPI.QueryBuilder().WithAllRW<WeaponMount>().WithAll<Owner>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _mounts.Update(ref state);
            _owners.Update(ref state);
            state.Dependency = new UnlockJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Researched = SystemAPI.GetBufferLookup<CompletedResearch>(true),
                Mounts = _mounts,
                Owners = _owners,
            }.ScheduleParallel(_query, state.Dependency);
        }

        /// <summary>Writes a chunk only when a lock flips, so its change version bumps only then.</summary>
        [BurstCompile]
        private struct UnlockJob : IJobChunk
        {
            public PlayerRegistry Players;
            [ReadOnly] public BufferLookup<CompletedResearch> Researched;
            [ReadOnly] public ComponentTypeHandle<Owner> Owners;
            public BufferTypeHandle<WeaponMount> Mounts;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var owners = chunk.GetNativeArray(ref Owners);
                var accessor = chunk.GetBufferAccessorRO(ref Mounts);
                var writable = false;
                for (var e = 0; e < chunk.Count; e++)
                {
                    var known = Researched.TryGetBuffer(Players[owners[e].Value], out var researched);
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
