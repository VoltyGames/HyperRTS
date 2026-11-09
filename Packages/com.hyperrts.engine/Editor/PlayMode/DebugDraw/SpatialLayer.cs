using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Spatial index cells that hold at least one entity.</summary>
    public sealed class SpatialLayer : DebugLayer
    {
        public override string Label => "Spatial cells";

        public override string Detail => "Blue spatial index cells that hold at least one entity.";

        public override string Group => "Movement";

        public override string Glyph => "scan";

        public override void Draw(EntityManager entityManager, DebugShapes shapes)
        {
            if (!TryGetSingleton(entityManager, out SpatialIndex index) || index.CellStarts.Length <= index.Grid.Count)
            {
                return;
            }

            shapes.Cells(index.Grid, new Colors { Starts = index.CellStarts.AsArray() });
        }

        private struct Colors : ICellColors
        {
            [ReadOnly] public NativeArray<int> Starts;

            public Color32 At(int index) => Starts[index + 1] > Starts[index] ? new Color32(77, 153, 255, 64) : default;
        }
    }
}
