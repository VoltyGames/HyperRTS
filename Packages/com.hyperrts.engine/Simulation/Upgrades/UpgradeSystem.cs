using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>
    /// Gives every owned entity the stat modifiers of its owner's researched upgrades, catching up new spawns and
    /// swapping the set when ownership changes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    [UpdateAfter(typeof(ProductionSystem))]
    public partial struct UpgradeSystem : ISystem
    {
        private EntityQuery _owned;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _owned = new EntityQueryBuilder(Allocator.Temp).WithAllRW<AppliedUpgrades>().WithAll<EntityInfo, Owner>()
                .Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ApplyJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Researched = SystemAPI.GetBufferLookup<CompletedResearch>(true),
                Effects = SystemAPI.GetBufferLookup<UpgradeEffect>(true),
                Applied = SystemAPI.GetComponentTypeHandle<AppliedUpgrades>(),
                Infos = SystemAPI.GetComponentTypeHandle<EntityInfo>(true),
                Owners = SystemAPI.GetComponentTypeHandle<Owner>(true),
                Modifiers = SystemAPI.GetBufferTypeHandle<StatModifier>(),
            }.ScheduleParallel(_owned, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyJob : IJobChunk
        {
            public PlayerRegistry Players;
            [ReadOnly] public BufferLookup<CompletedResearch> Researched;
            [ReadOnly] public BufferLookup<UpgradeEffect> Effects;
            public ComponentTypeHandle<AppliedUpgrades> Applied;
            [ReadOnly] public ComponentTypeHandle<EntityInfo> Infos;
            [ReadOnly] public ComponentTypeHandle<Owner> Owners;
            public BufferTypeHandle<StatModifier> Modifiers;

            // The writable modifiers are taken only for an entity whose set changes, so stat change filters stay quiet.
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var applied = chunk.GetNativeArray(ref Applied);
                var infos = chunk.GetNativeArray(ref Infos);
                var owners = chunk.GetNativeArray(ref Owners);
                var entities = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (entities.NextEntityIndex(out var i))
                {
                    var current = applied[i];
                    Apply(chunk, i, ref current, infos[i].TypeId, owners[i].Value);
                    applied[i] = current;
                }
            }

            private void Apply(in ArchetypeChunk chunk, int index, ref AppliedUpgrades applied, int typeId, byte owner)
            {
                var hasModifiers = chunk.Has(ref Modifiers);
                if (applied.Owner != owner)
                {
                    if (applied.Count > 0 && hasModifiers)
                    {
                        new StatModifiers(chunk.GetBufferAccessorRW(ref Modifiers)[index]).RemoveKind(StatSourceKind.Upgrade);
                    }

                    applied = new AppliedUpgrades { Owner = owner };
                }

                if (!Researched.TryGetBuffer(Players[owner], out var researched))
                {
                    return;
                }

                if (applied.Count >= researched.Length || !hasModifiers)
                {
                    return;
                }

                var modifiers = chunk.GetBufferAccessorRW(ref Modifiers)[index];
                for (var i = applied.Count; i < researched.Length; i++)
                {
                    // Research that only unlocks something has no effects.
                    if (!Effects.TryGetBuffer(researched[i].Prefab, out var effects))
                    {
                        continue;
                    }

                    foreach (var effect in effects)
                    {
                        if (effect.AppliesTo == 0 || effect.AppliesTo == typeId)
                        {
                            modifiers.Add(effect.Modifier);
                        }
                    }
                }

                applied.Count = researched.Length;
            }
        }
    }
}
