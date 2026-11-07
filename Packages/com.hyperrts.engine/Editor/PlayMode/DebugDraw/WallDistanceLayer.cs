using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>The distance to the nearest wall for the first field key, red at a wall to blue at <see cref="NavField.MaxDistance"/>.</summary>
    public sealed class WallDistanceLayer : CellTextureLayer
    {
        public override string Label => "Wall distance";

        public override string Detail => "Distance to the nearest wall for the first field key";

        public override string Group => "Movement";

        public override string Glyph => "ruler";

        protected override bool Paint(EntityManager entityManager, CellTexture texture)
        {
            if (!TryGetSingleton(entityManager, out NavGrid grid) || !grid.IsCreated || grid.Field.SlotCount == 0)
            {
                return false;
            }

            texture.Paint(grid.Grid, new Colors { Distances = grid.Field.Distances });
            return true;
        }

        private struct Colors : ICellColors
        {
            [ReadOnly] public NativeArray<float> Distances;

            public Color32 At(int index) => CellTexture.Heat(1f - Distances[index] / NavField.MaxDistance);
        }
    }
}
