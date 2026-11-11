using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>Single-threaded: an open obstacle with a crush rating fills every cell under its box.</summary>
    [BurstCompile]
    internal partial struct NavOpenSceneryJob : IJobEntity
    {
        public NavGrid Grid;

        private void Execute(in Destructible destructible, in NavObstacle obstacle, in LocalTransform transform)
        {
            if (obstacle.Open && destructible.Crush > 0)
            {
                var rating = (byte)math.min(destructible.Crush, NavAccess.AllScenery - 1);
                Grid.StampScenery(new NavBox(transform.Position, transform.Rotation, obstacle.Size), rating);
            }
        }
    }
}
