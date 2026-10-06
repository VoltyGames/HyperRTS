using HyperRTS.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Overrides a box of the pathfinding grid: water, a blocked area or a walkable deck (bridge). Destroying the
    /// entity restores the cells and units re-path.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Navigation + "Nav Area")]
    [Icon(HyperRTSIcons.Navigation)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class NavAreaAuthoring : MonoBehaviour
    {
        [Tooltip("Area (X by Z) centred on this transform.")]
        public Vector2 size = new(4f, 4f);

        [Tooltip("Water, Blocked, or Deck: walkable at this transform's height with any water below kept for ships.")]
        public NavAreaKind kind = NavAreaKind.Deck;

        public class Baker : Baker<NavAreaAuthoring>
        {
            public override void Bake(NavAreaAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new NavArea { Size = authoring.size, Kind = authoring.kind });
            }
        }
    }

    /// <summary>Axis-aligned XZ box stamped over the grid's terrain surfaces, before <see cref="NavObstacle"/>s.</summary>
    public struct NavArea : IComponentData
    {
        public float2 Size;
        public NavAreaKind Kind;
    }
}
