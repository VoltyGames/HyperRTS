using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>The distance to the nearest wall for the first field key, red at a wall to blue at <see cref="NavField.MaxDistance"/>.</summary>
    public sealed class WallDistanceLayer : DebugLayer
    {
        public override string Label => "Wall distance";

        public override string Detail => "Distance to the nearest wall for the first field key, red at a wall to blue far from one.";

        public override string Group => "Movement";

        public override string Glyph => "ruler";

        public override void Draw(EntityManager entityManager, DebugShapes shapes)
        {
            if (!TryGetSingleton(entityManager, out NavGrid grid) || !grid.IsCreated || grid.Field.SlotCount == 0)
            {
                return;
            }

            shapes.Cells(grid.Grid, new Colors { Distances = grid.Field.Distances });
        }

        private struct Colors : ICellColors
        {
            [ReadOnly] public NativeArray<float> Distances;

            public Color32 At(int index) => DebugShapes.Heat(1f - Distances[index] / NavField.MaxDistance);
        }
    }
}
