using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// Where the Map Editor puts a point: on the half-metre grid specs are written in, and for a shared single-point
    /// feature (placed once, not copied) on the symmetry's axis or centre so it stays symmetric.
    /// </summary>
    public static class MapSnap
    {
        public const float Step = 0.5f;

        public static float Grid(float value) => Mathf.Round(value / Step) * Step;

        public static Vector2 Grid(Vector2 value) => new(Grid(value.x), Grid(value.y));

        /// <summary>
        /// A feature position: on the grid, then onto the symmetry's fixed points when the feature is shared. With no
        /// valid symmetry (the spec names an unknown one) shared features only snap to the grid.
        /// </summary>
        public static Vector2 Position(Vector2 world, bool shared, MapSymmetry symmetry)
        {
            var onGrid = Grid(world);
            if (!shared || symmetry == null)
            {
                return onGrid;
            }

            return Grid(symmetry.ToFixed(onGrid));
        }
    }
}
