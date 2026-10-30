using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>The burning cells in one <see cref="FieldFires.ClusterCells"/> square, played as one fire effect.</summary>
    public struct FieldFireCluster
    {
        /// <summary>The square's index on the cluster grid, stable while it burns, so its effect stream keeps one look.</summary>
        public int Key;

        /// <summary>The middle of its burning cells.</summary>
        public float2 Centre;

        public int Cells;
    }
}
