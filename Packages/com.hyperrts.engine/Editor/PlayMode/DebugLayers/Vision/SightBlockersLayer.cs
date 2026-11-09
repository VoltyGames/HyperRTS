using HyperRTS.Editor.PlayMode.DebugLayers;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugLayers.Vision
{
    /// <summary>What blocks sight: solid in red, porous or crop cover in green, other concealment in yellow.</summary>
    public sealed class SightBlockersLayer : DebugLayer
    {
        public override string Label => "Sight blockers";

        public override string Detail => "Red solid blockers, green porous or crop cover, yellow other concealment.";

        public override string Group => "Vision";

        public override string Glyph => "brick-wall";

        public override void Draw(EntityManager entityManager, DebugShapes shapes)
        {
            if (!TryGetSingleton(entityManager, out SightBlockers blockers) || !blockers.IsCreated)
            {
                return;
            }

            shapes.Cells(blockers.Grid, new Colors { Cells = blockers.Cells });
        }

        private struct Colors : ICellColors
        {
            [ReadOnly] public NativeArray<SightCell> Cells;

            public Color32 At(int index)
            {
                var cell = Cells[index];
                if (cell.Solid > 0)
                {
                    return new Color32(255, 50, 50, 140);
                }

                if (cell.Porous > 0 || cell.Crop > 0)
                {
                    return new Color32(60, 200, 80, 110);
                }

                return cell.Conceals != 0 ? new Color32(255, 220, 60, 110) : default;
            }
        }
    }
}
