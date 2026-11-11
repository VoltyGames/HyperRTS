using HyperRTS.Simulation.Navigation.Areas;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>Single-threaded: overlapping footprints write the same cells.</summary>
    [BurstCompile]
    internal partial struct NavObstacleJob : IJobEntity
    {
        public NavGrid Grid;
        [ReadOnly] public BufferLookup<NavObstaclePart> Parts;

        private void Execute(Entity entity, in NavObstacle obstacle, in LocalTransform transform)
        {
            if (obstacle.Open)
            {
                return;
            }

            using var marker = NavGridSystem.RestampMarker.Auto();
            if (!Parts.TryGetBuffer(entity, out var parts))
            {
                Grid.StampArea(new NavBox(transform.Position, transform.Rotation, obstacle.Size), NavAreaKind.Blocked, 0f);
                return;
            }

            foreach (var part in parts)
            {
                if (part.Closed)
                {
                    Grid.StampArea(part.Box(transform), NavAreaKind.Blocked, 0f);
                }
            }
        }
    }
}
