using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Spatial index cells that hold at least one entity.</summary>
    public sealed class SpatialLayer : CellTextureLayer
    {
        public override string Label => "Spatial cells";

        protected override bool Paint(EntityManager entityManager, CellTexture texture)
        {
            if (!TryGetSingleton(entityManager, out SpatialIndex index) || index.CellStarts.Length <= index.Frame.Count)
            {
                return false;
            }

            texture.Paint(index.Frame, new Colors { Starts = index.CellStarts.AsArray() });
            return true;
        }

        private struct Colors : ICellColors
        {
            [ReadOnly] public NativeArray<int> Starts;

            public Color32 At(int index) => Starts[index + 1] > Starts[index] ? new Color32(77, 153, 255, 64) : default;
        }
    }
}
