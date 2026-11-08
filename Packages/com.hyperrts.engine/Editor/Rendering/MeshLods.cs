using UnityEngine;

namespace HyperRTS.Editor.Rendering
{
    /// <summary>
    /// The one rule every Mesh LOD mesh switches by: a level per halving of screen size, so models of any size and source
    /// drop detail alike.
    /// </summary>
    public static class MeshLods
    {
        /// <summary>
        /// Pixels the bounds' diameter spans when level 1 takes over, at a Mesh LOD threshold of 1; level 2 takes over at
        /// half that, and so on. A quality level's threshold scales it.
        /// </summary>
        public const float FirstLevelPixels = 150f;

        /// <summary>
        /// Replaces the curve Unity fits from simplification error, which keeps a level until its error is under a pixel,
        /// with the size rule. Does nothing to a mesh with one level.
        /// </summary>
        public static void UseSizeRule(Mesh mesh)
        {
            if (mesh.lodCount < 2)
            {
                return;
            }

            // Entities Graphics picks level slope * log2(threshold / diameter pixels) + bias.
            mesh.lodSelectionCurve = new Mesh.LodSelectionCurve { lodSlope = 1f, lodBias = 1f + Mathf.Log(FirstLevelPixels, 2f) };
        }
    }
}
