using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugLayers
{
    /// <summary>The colour of each cell of a grid, which <see cref="DebugShapes.Cells"/> reads for every cell index.</summary>
    public interface ICellColors
    {
        Color32 At(int index);
    }
}
