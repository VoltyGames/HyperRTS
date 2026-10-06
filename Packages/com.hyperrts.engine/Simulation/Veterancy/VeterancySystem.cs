using HyperRTS.Core;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Veterancy
{
    /// <summary>Promotes units whose experience reached a new rank and swaps in that rank's stat bonuses.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(KillCreditSystem))]
    public partial struct VeterancySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) => new PromoteJob().ScheduleParallel();

        [BurstCompile]
        [WithChangeFilter(typeof(Experience))]
        private partial struct PromoteJob : IJobEntity
        {
            private void Execute(ref Experience experience, DynamicBuffer<VeterancyRank> ranks,
                DynamicBuffer<VeterancyBonus> bonuses, DynamicBuffer<StatModifier> modifiers)
            {
                var rank = RankFor(experience.Points, ranks);
                if (rank == experience.Rank)
                {
                    return;
                }

                experience.Rank = rank;
                StatMath.RemoveSource(modifiers, StatMath.VeterancySource);
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
