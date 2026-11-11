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
        private EntityQuery _mounted;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _mounted = new EntityQueryBuilder(Allocator.Temp).WithAllRW<WeaponMount>().WithAll<Owner>()
                .Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new UnlockJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Researched = SystemAPI.GetBufferLookup<CompletedResearch>(true),
                Mounts = SystemAPI.GetBufferTypeHandle<WeaponMount>(),
                Owners = SystemAPI.GetComponentTypeHandle<Owner>(true),
            }.ScheduleParallel(_mounted, state.Dependency);
        }

        [BurstCompile]
        private struct UnlockJob : IJobChunk
        {
            public PlayerRegistry Players;
            [ReadOnly] public BufferLookup<CompletedResearch> Researched;
            public BufferTypeHandle<WeaponMount> Mounts;
            [ReadOnly] public ComponentTypeHandle<Owner> Owners;

            // The writable accessor is taken only when a lock flips, so its change version bumps only then.
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var read = chunk.GetBufferAccessorRO(ref Mounts);
                var owners = chunk.GetNativeArray(ref Owners);
                var entities = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (entities.NextEntityIndex(out var i))
                {
                    var known = Researched.TryGetBuffer(Players[owners[i].Value], out var researched);
                    var mounts = read[i];
                    for (var m = 0; m < mounts.Length; m++)
                    {
                        var mount = mounts[m];
                        if (mount.RequiredResearchTypeId == 0)
                        {
                            continue;
                        }

                        var unlocked = known && ResearchRules.IsDone(researched, mount.RequiredResearchTypeId);
                        if (mount.Unlocked != unlocked)
                        {
                            mount.Unlocked = unlocked;
                            chunk.GetBufferAccessorRW(ref Mounts)[i].ElementAt(m) = mount;
                        }
                    }
                }
            }
        }
    }
}
