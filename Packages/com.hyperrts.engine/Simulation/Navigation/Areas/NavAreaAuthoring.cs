using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Geometry;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Navigation.Areas;

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

    [Tooltip("Deck only: the renderers of the bridge's piers standing in the water, or empty for a single span. Each makes foam where the current meets it.")]
    public Renderer[] piers = System.Array.Empty<Renderer>();

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

        var first = LocalBounds(abutments[0]);
        var second = LocalBounds(abutments[1]);
        return Mathf.Max(first.min.z, second.min.z) - Mathf.Min(first.max.z, second.max.z);
    }

    /// <summary>Each pier as (centre x, centre z, half-extent along Z) in this transform's space: the width it blocks across the flow.</summary>
    public Vector3[] PierPoints()
    {
        var points = new Vector3[piers.Length];
        for (var i = 0; i < piers.Length; i++)
        {
            if (piers[i] == null)
            {
                throw new System.InvalidOperationException($"{name}: pier {i} of the Nav Area is empty.");
            }

            var bounds = LocalBounds(piers[i]);
            points[i] = new Vector3(bounds.center.x, bounds.center.z, bounds.extents.z);
        }

        return points;
    }

    private Bounds LocalBounds(Renderer renderer) =>
        BoundsMath.Moved(renderer.localBounds, transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix);

    public class Baker : Baker<NavAreaAuthoring>
    {
        public override void Bake(NavAreaAuthoring authoring)
        {
            var writer = this.Writer(TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace);
            NavSetup.AddArea(writer, authoring.size, authoring.kind);
        }
    }
}
