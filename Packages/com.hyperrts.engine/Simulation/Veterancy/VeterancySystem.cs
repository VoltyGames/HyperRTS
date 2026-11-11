using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Veterancy
{
    /// <summary>Promotes units whose experience reached a new rank and swaps in that rank's stat bonuses.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(KillCreditSystem))]
    public partial struct VeterancySystem : ISystem
    {
        private EntityQuery _earning;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _earning = new EntityQueryBuilder(Allocator.Temp).WithAllRW<Experience>()
                .WithAll<VeterancyRank, VeterancyBonus>().Build(ref state);
            _earning.SetChangedVersionFilter(ComponentType.ReadWrite<Experience>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new PromoteJob
            {
                Experiences = SystemAPI.GetComponentTypeHandle<Experience>(),
                Ranks = SystemAPI.GetBufferTypeHandle<VeterancyRank>(true),
                Bonuses = SystemAPI.GetBufferTypeHandle<VeterancyBonus>(true),
                Modifiers = SystemAPI.GetBufferTypeHandle<StatModifier>(),
            }.ScheduleParallel(_earning, state.Dependency);
        }

        /// <summary>Takes modifiers writable only on a promotion, so only promotions wake the stat systems' change filters.</summary>
        [BurstCompile]
        private struct PromoteJob : IJobChunk
        {
            public ComponentTypeHandle<Experience> Experiences;
            [ReadOnly] public BufferTypeHandle<VeterancyRank> Ranks;
            [ReadOnly] public BufferTypeHandle<VeterancyBonus> Bonuses;
            public BufferTypeHandle<StatModifier> Modifiers;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var experiences = chunk.GetNativeArray(ref Experiences);
                var ranks = chunk.GetBufferAccessorRO(ref Ranks);
                var bonuses = chunk.GetBufferAccessorRO(ref Bonuses);
                var entities = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (entities.NextEntityIndex(out var i))
                {
                    var experience = experiences[i];
                    var rank = RankFor(experience.Points, ranks[i]);
                    if (rank == experience.Rank)
                    {
                        continue;
                    }

                    experience.Rank = rank;
                    experiences[i] = experience;
                    if (chunk.Has(ref Modifiers))
                    {
                        Promote(chunk.GetBufferAccessorRW(ref Modifiers)[i], bonuses[i], rank);
                    }
                }
            }

            private static void Promote(DynamicBuffer<StatModifier> modifiers, in DynamicBuffer<VeterancyBonus> bonuses,
                byte rank)
            {
                new StatModifiers(modifiers).RemoveSource(StatSource.Veterancy);
                foreach (var bonus in bonuses)
                {
                    if (bonus.Rank <= rank)
                    {
                        modifiers.Add(bonus.Modifier);
                    }
                }
            }

            private static byte RankFor(float points, DynamicBuffer<VeterancyRank> ranks)
            {
                byte rank = 0;
                for (var i = 0; i < ranks.Length && points >= ranks[i].Experience; i++)
                {
                    rank = (byte)(i + 1);
                }

                return rank;
            }
        }
    }
}
