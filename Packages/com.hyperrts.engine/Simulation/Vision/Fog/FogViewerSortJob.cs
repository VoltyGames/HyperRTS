using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace HyperRTS.Simulation.Vision.Fog
{
    /// <summary>Groups the viewers by team, so a merge batch visits only the viewers of the teams it writes.</summary>
    [BurstCompile]
    internal struct FogViewerSortJob : IJob
    {
        [ReadOnly] public NativeArray<Entity> Viewers;
        [ReadOnly] public ComponentLookup<SightArea> Areas;
        public NativeList<Entity> Sorted;

        /// <summary>Where each team's viewers start in <see cref="Sorted"/>, with the total as the last entry.</summary>
        public NativeArray<int> TeamStarts;

        public void Execute()
        {
            for (var i = 0; i < TeamStarts.Length; i++)
            {
                TeamStarts[i] = 0;
            }

            foreach (var viewer in Viewers)
            {
                TeamStarts[Areas[viewer].Team + 1]++;
            }

            for (var team = 1; team < TeamStarts.Length; team++)
            {
                TeamStarts[team] += TeamStarts[team - 1];
            }

            Sorted.Resize(Viewers.Length, NativeArrayOptions.UninitializedMemory);
            var placed = new NativeArray<int>(OwnerRelations.MaxTeams, Allocator.Temp);
            foreach (var viewer in Viewers)
            {
                var team = Areas[viewer].Team;
                Sorted[TeamStarts[team] + placed[team]++] = viewer;
            }

            placed.Dispose();
        }
    }
}
