using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>Reloads the <see cref="WeaponMount"/>s that refill anywhere, by the round or by the magazine. Docked refills are the air module's.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(WeaponFireSystem))]
    public partial struct AmmoRefillSystem : ISystem
    {
        private EntityQuery _mounted;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _mounted = new EntityQueryBuilder(Allocator.Temp).WithAllRW<WeaponMount>().WithNone<Dead>()
                .Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ReloadJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Mounts = SystemAPI.GetBufferTypeHandle<WeaponMount>(),
            }.ScheduleParallel(_mounted, state.Dependency);
        }

        [BurstCompile]
        private struct ReloadJob : IJobChunk
        {
            public float DeltaTime;
            public BufferTypeHandle<WeaponMount> Mounts;

            // The writable accessor is taken only for a unit that reloads, so idle chunks keep their version.
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var read = chunk.GetBufferAccessorRO(ref Mounts);
                var write = default(BufferAccessor<WeaponMount>);
                var writable = false;
                var entities = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (entities.NextEntityIndex(out var i))
                {
                    if (!WeaponMount.NeedsReload(read[i], docked: false))
                    {
                        continue;
                    }

                    if (!writable)
                    {
                        write = chunk.GetBufferAccessorRW(ref Mounts);
                        writable = true;
                    }

                    WeaponMount.Reload(write[i], DeltaTime, docked: false);
                }
            }
        }
    }
}
