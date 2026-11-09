using HyperRTS.Editor.PlayMode.DebugLayers;
using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugLayers.Movement
{
    /// <summary>The cost to the goal over the most recently built group field, repeating blue to red every <see cref="Band"/> cost.</summary>
    public sealed class FlowFieldLayer : DebugLayer
    {
        private const float Band = 32f;

        public override string Label => "Flow field";

        public override string Detail => "Cost to the goal of the latest ordered group, repeating blue to red.";

        public override string Group => "Movement";

        public override string Glyph => "wind";

        public override void Draw(EntityManager entityManager, DebugShapes shapes)
        {
            if (!TryGetSingleton(entityManager, out NavGrid grid) || !grid.IsCreated ||
                !PathsLayer.TryGetPathfinding(entityManager, out var pathfinding))
            {
                return;
            }

            var fields = pathfinding.Fields.Fields;
            if (fields.Length == 0)
            {
                return;
            }

            shapes.Cells(grid.Grid, new Colors { Field = fields[fields.Length - 1].Field, Grid = grid.Grid });
        }

        private struct Colors : ICellColors
        {
            public FlowField Field;
            public CellGrid Grid;

            public Color32 At(int index)
            {
                var cost = Field.Cost(Grid.Cell(index));
                return cost < float.PositiveInfinity ? DebugShapes.Heat(math.frac(cost / Band)) : default;
            }
        }
    }
}
