using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Spatial index cells that hold at least one entity.</summary>
    public sealed class SpatialLayer : CellDebugLayer
    {
        public override string Label => "Spatial cells";

        public override void Draw(EntityManager entityManager)
        {
            if (!TryGetSingleton(entityManager, out SpatialIndex index) || index.CellStarts.Length <= index.Frame.Count)
            {
                return;
            }

            BeginCells(new Color(0.3f, 0.6f, 1f, 0.25f));
            for (var i = 0; i < index.Frame.Count; i++)
            {
                if (index.CountIn(i) > 0)
                {
                    Cell(index.Frame.Corner(index.Frame.Cell(i)), index.CellSize);
                }
            }

            EndCells();
        }
    }
}
