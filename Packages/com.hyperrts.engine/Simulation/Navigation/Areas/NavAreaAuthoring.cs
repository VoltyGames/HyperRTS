using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Navigation.Areas
{
    /// <summary>
    /// Overrides a box of the pathfinding grid: water, a blocked area or a walkable deck (bridge). Destroying the
    /// entity restores the cells and units re-path.
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

        public class Baker : Baker<NavAreaAuthoring>
        {
            public override void Bake(NavAreaAuthoring authoring)
            {
                var writer = this.Writer();
                NavSetup.AddArea(writer, authoring.size, authoring.kind);
            }
        }
    }
}
