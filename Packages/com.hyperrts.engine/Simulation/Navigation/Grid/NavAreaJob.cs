using HyperRTS.Simulation.Navigation.Areas;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>Single-threaded: overlapping areas write the same cells. Decks go last so they span water areas.</summary>
    [BurstCompile]
    internal partial struct NavAreaJob : IJobEntity
    {
        public NavGrid Grid;
        public bool Decks;
        [ReadOnly] public BufferLookup<NavAreaPart> Parts;

        private void Execute(Entity entity, in NavArea area, in LocalTransform transform)
        {
            using var marker = NavGridSystem.RestampMarker.Auto();
            if ((area.Kind == NavAreaKind.Deck) != Decks)
            {
                return;
            }

            if (!Parts.TryGetBuffer(entity, out var parts))
            {
                Grid.StampArea(new NavBox(transform.Position, transform.Rotation, area.Size), area.Kind,
                    transform.Position.y);
                return;
            }

            foreach (var part in parts)
            {
                if (part.Open)
                {
                    Grid.StampArea(part.Box(transform), area.Kind, transform.Position.y);
                }
            }
        }
    }
}
