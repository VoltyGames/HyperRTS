using HyperRTS.Simulation.Common;
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

        public override string Label => "Flow field (latest group)";

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

            texture.Paint(grid.Frame, new Colors { Field = fields[fields.Length - 1].Field, Frame = grid.Frame });
            return true;
        }

        private struct Colors : ICellColors
        {
            public FlowField Field;
            public GridFrame Frame;

            public Color32 At(int index)
            {
                var cost = Field.Cost(Frame.Cell(index));
                return cost < float.PositiveInfinity ? CellTexture.Heat(math.frac(cost / Band)) : default;
            }
        }
    }
}
