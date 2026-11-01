using HyperRTS.Core;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
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
        private BufferLookup<StatModifier> _modifierLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state) => _modifierLookup = state.GetBufferLookup<StatModifier>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _modifierLookup.Update(ref state);
            new PromoteJob { ModifierLookup = _modifierLookup }.ScheduleParallel();
        }

        /// <summary>Writes modifiers through a lookup so only promotions wake the stat systems' change filters.</summary>
        [BurstCompile]
        [WithChangeFilter(typeof(Experience))]
        private partial struct PromoteJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public BufferLookup<StatModifier> ModifierLookup;

            private void Execute(Entity entity, ref Experience experience, in DynamicBuffer<VeterancyRank> ranks,
                in DynamicBuffer<VeterancyBonus> bonuses)
            {
                var rank = RankFor(experience.Points, ranks);
                if (rank == experience.Rank)
                {
                    return;
                }

                experience.Rank = rank;
                if (!ModifierLookup.TryGetBuffer(entity, out var modifiers))
                {
                    return;
                }

                StatMath.RemoveSource(modifiers, StatSource.Veterancy);
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
