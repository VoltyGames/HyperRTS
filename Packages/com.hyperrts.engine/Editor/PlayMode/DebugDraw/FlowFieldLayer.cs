using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>The cost to the goal over the most recently built group field, repeating blue to red every <see cref="Band"/> cost.</summary>
    public sealed class FlowFieldLayer : CellTextureLayer
    {
        private const float Band = 32f;

        public override string Label => "Flow field";

        public override string Detail => "The flow field of the latest ordered group";

        public override string Group => "Movement";

        public override string Glyph => "wind";

        protected override bool Paint(EntityManager entityManager, CellTexture texture)
        {
            if (!TryGetSingleton(entityManager, out NavGrid grid) || !grid.IsCreated ||
                !PathsLayer.TryGetPathfinding(entityManager, out var pathfinding))
            {
                return false;
            }

            var fields = pathfinding.Fields.Fields;
            if (fields.Length == 0)
            {
                return false;
            }

            texture.Paint(grid.Grid, new Colors { Field = fields[fields.Length - 1].Field, Grid = grid.Grid });
            return true;
        }

        private struct Colors : ICellColors
        {
            public FlowField Field;
            public CellGrid Grid;

            public Color32 At(int index)
            {
                var cost = Field.Cost(Grid.Cell(index));
                return cost < float.PositiveInfinity ? CellTexture.Heat(math.frac(cost / Band)) : default;
            }
        }
    }
}
