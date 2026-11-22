using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Navigation.Areas
{
    /// <summary>
    /// Overrides a box of the pathfinding grid: water, a blocked area or a walkable deck (bridge). Destroying the
    /// entity restores the cells and units re-path. On a child it keeps its place in the world, so it never moves with
    /// its parent.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Scene + "Navigation/Nav Area")]
    [Icon(HyperRTSIcons.NavAreaAuthoring)]
    [HelpURL(HyperRTSDocs.Navigation)]
    [DisallowMultipleComponent]
    public class NavAreaAuthoring : AuthoringBehaviour
    {
        [Tooltip("Area (X by Z) centred on this transform.")]
        public Vector2 size = new(4f, 4f);

        [Tooltip("Water, Blocked, or Deck: walkable at this transform's height with any water below kept for ships.")]
        public NavAreaKind kind = NavAreaKind.Deck;

        [Tooltip("Deck only: the renderers of the bridge's two abutments, or empty for a deck with none. The gap between their inner faces along this transform's Z is the open length map building keeps for the river.")]
        public Renderer[] abutments = System.Array.Empty<Renderer>();

        /// <summary>The gap between the abutments' inner faces along Z in this transform's space; 0 with no abutments, negative when they overlap.</summary>
        public float ClearSpan()
        {
            if (abutments.Length == 0)
            {
                return 0f;
            }

            if (abutments.Length != 2 || abutments[0] == null || abutments[1] == null)
            {
                throw new System.InvalidOperationException($"{name}: a Nav Area lists no abutments or exactly two renderers.");
            }

            var first = ZRange(abutments[0]);
            var second = ZRange(abutments[1]);
            return Mathf.Max(first.x, second.x) - Mathf.Min(first.y, second.y);
        }

        private Vector2 ZRange(Renderer renderer)
        {
            var toLocal = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var bounds = renderer.localBounds;
            var range = new Vector2(float.MaxValue, float.MinValue);
            for (var corner = 0; corner < 8; corner++)
            {
                var offset = new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                var z = toLocal.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, offset)).z;
                range = new Vector2(Mathf.Min(range.x, z), Mathf.Max(range.y, z));
            }

            return range;
        }

        public class Baker : Baker<NavAreaAuthoring>
        {
            public override void Bake(NavAreaAuthoring authoring)
            {
                var writer = this.Writer(TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace);
                NavSetup.AddArea(writer, authoring.size, authoring.kind);
            }
        }
    }
}
