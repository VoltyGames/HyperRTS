using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Fields;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>Single-threaded: scenery sharing a cell keeps the sturdiest rating. Each piece fills its own cell.</summary>
    [BurstCompile]
    internal partial struct NavSceneryJob : IJobEntity
    {
        public NavGrid Grid;

        private void Execute(in Destructible destructible, in LocalTransform transform)
        {
            var cell = Grid.Grid.Cell(transform.Position);
            if (destructible.Crush <= 0 || !Grid.Grid.Contains(cell))
            {
                return;
            }

            var index = Grid.Grid.Index(cell);
            var rating = math.min(destructible.Crush, NavAccess.AllScenery - 1);
            Grid.Scenery[index] = (byte)math.max((int)Grid.Scenery[index], rating);
        }
    }
}
