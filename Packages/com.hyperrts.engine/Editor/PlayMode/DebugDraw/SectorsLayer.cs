using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Sectors;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Connected regions of the first movement class, one colour each, with white where a region ends.</summary>
    public sealed class SectorsLayer : DebugLayer
    {
        public override string Label => "Sectors";

        public override string Detail => "Connected regions of the first movement class, one colour each, white where a region ends.";

        public override string Group => "Movement";

        public override string Glyph => "layout-grid";

        public override void Draw(EntityManager entityManager, DebugShapes shapes)
        {
            if (!TryGetSingleton(entityManager, out NavGrid grid) || !grid.IsCreated || grid.Sectors.ClassCount == 0)
            {
                return;
            }

            shapes.Cells(grid.Grid, new Colors { Sectors = grid.Sectors, Grid = grid.Grid });
        }

        private struct Colors : ICellColors
        {
            public NavSectors Sectors;
            public CellGrid Grid;

            public Color32 At(int index)
            {
                var cell = Grid.Cell(index);
                var region = Sectors.Region(0, cell);
                var edge = region != Sectors.Region(0, cell + new int2(1, 0)) || region != Sectors.Region(0, cell + new int2(0, 1));
                return edge && region != 0 ? new Color32(255, 255, 255, 160) : DebugShapes.Hashed(region);
            }
        }
    }
}
