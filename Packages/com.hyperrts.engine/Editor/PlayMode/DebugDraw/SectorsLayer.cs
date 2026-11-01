using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Sectors;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Connected regions of the first movement class, one colour each, with white where a region ends.</summary>
    public sealed class SectorsLayer : CellTextureLayer
    {
        public override string Label => "Sectors (regions of first class)";

        protected override bool Paint(EntityManager entityManager, CellTexture texture)
        {
            if (!TryGetSingleton(entityManager, out NavGrid grid) || !grid.IsCreated || grid.Sectors.ClassCount == 0)
            {
                return false;
            }

            texture.Paint(grid.Frame, new Colors { Sectors = grid.Sectors, Frame = grid.Frame });
            return true;
        }

        private struct Colors : ICellColors
        {
            public NavSectors Sectors;
            public GridFrame Frame;

            public Color32 At(int index)
            {
                var cell = Frame.Cell(index);
                var region = Sectors.Region(0, cell);
                var edge = region != Sectors.Region(0, cell + new int2(1, 0)) || region != Sectors.Region(0, cell + new int2(0, 1));
                return edge && region != 0 ? new Color32(255, 255, 255, 160) : CellTexture.Hashed(region);
            }
        }
    }
}
